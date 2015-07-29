using System;
using System.Collections.Generic;
using System.Threading;
using System.Diagnostics;
using System.IO;
using VitaDefiler.Modules;
using VitaDefiler.PSM;

namespace VitaDefiler
{
    class Program
    {
        static readonly Type[] Mods = {typeof(Code), typeof(General), typeof(Memory), typeof(FileIO), typeof(Scripting)};

        static void Main(string[] args)
        {
            int scriptIndex = 0;
            bool enablegui = true;
            string package = null;
            bool useUsb = false;

#if USE_UNITY
            foreach (string arg in args)
            {
                switch (arg)
                {
                    case "-nodisp":
                        ++scriptIndex;
                        enablegui = false;
                        break;

                    case "-install":
                        scriptIndex += 2;
                        package = args[0];
                        break;
                }
            }

            if (!string.IsNullOrEmpty(package) && !File.Exists(package))
            {
                Console.Error.WriteLine("cannot find package file");
                return;
            }

#else
            useUsb = true;
#endif

#if USE_APP_KEY
            if (!File.Exists(args[1]))
            {
                Console.Error.WriteLine("cannot find key file");
                return;
            }
#endif

            // kill PSM
            Process[] potential = Process.GetProcesses();
            foreach (Process process in potential)
            {
                if (process.ProcessName.StartsWith("PsmDevice") || process.ProcessName.StartsWith("PsmDeviceUnity"))
                {
                    Console.WriteLine("Killing PsmDevice process {0}", process.Id);
                    process.Kill();
                }
            }

            // set environment variables
            Environment.SetEnvironmentVariable("SCE_PSM_SDK", Path.Combine(Environment.CurrentDirectory, "support"));

            // initialize the modules
            List<IModule> mods = new List<IModule>();
            Scripting scripting = null;
            foreach (Type t in Mods)
            {
                if (typeof(IModule).IsAssignableFrom(t))
                {
                    IModule mod = (IModule)Activator.CreateInstance(t);
                    if (t == typeof(Scripting))
                    {
                        scripting = mod as Scripting;
                    }
                    mods.Add(mod);
                }
            }

            // set up usb
            Exploit exploit;
            string host;
            int port;
            
#if USE_UNITY
                ExploitFinder.CreateFromWireless(package, out exploit, out host, out port);
#else
                ExploitFinder.CreateFromUSB(package, out exploit, out host, out port);
#endif

#if !NO_EXPLOIT
            uint images_hash_ptr;
            uint[] funcs = new uint[5];
            uint logline_func;
            uint libkernel_anchor;
            Console.Error.WriteLine("Defeating ASLR...");
            exploit.DefeatASLR(out images_hash_ptr, out funcs[0], out funcs[1], out funcs[2], out funcs[3], out funcs[4], out libkernel_anchor);
            // exploit vita

            Console.Error.WriteLine("Escalating privileges...");
            exploit.EscalatePrivilege(images_hash_ptr);
#endif

#if USE_UNITY
            exploit.ResumeVM(); // The network listener is already listening in Unity.
#else
            exploit.StartNetworkListener();
            Console.Error.WriteLine("Vita exploited.");
#endif


            //Thread tt = new Thread(() =>
            //{
            //});
                //tt.Start();

            // set up network
            Network net = new Network();
            if (net.Connect(host, port))
            {
                Console.Error.WriteLine("Connected to Vita network");
            }
            else
            {
                Console.Error.WriteLine("Failed to create net listener. Exiting.");
                exploit.Disconnect();
                return;
            }

            byte[] resp;

            // enable gui
            if (enablegui)
            {
                Console.Error.WriteLine("Enabling display output");
                net.RunCommand(Command.EnableGUI, out resp);
            }
            
#if !NO_EXPLOIT
            // pass in function pointers
            if (net.RunCommand(Command.SetFuncPtrs, funcs, out resp) == Command.Error)
            {
                Console.Error.WriteLine("ERROR setting function pointers!");
            }
#endif

            // set up RPC context
            Device dev = new Device(exploit, net);
            
#if !NO_EXPLOIT
            // get logger
            net.RunCommand(Command.GetLogger, out resp);
            logline_func = BitConverter.ToUInt32(resp, 0);

            // pass in ASLR bypass as local variables for scripting use
            dev.CreateLocal("pss_code_mem_alloc", funcs[0]);
            dev.CreateLocal("pss_code_mem_free", funcs[1]);
            dev.CreateLocal("pss_code_mem_unlock", funcs[2]);
            dev.CreateLocal("pss_code_mem_lock", funcs[3]);
            dev.CreateLocal("pss_code_mem_flush_icache", funcs[4]);
            dev.CreateLocal("logline", logline_func);
            dev.CreateLocal("libkernel_anchor", libkernel_anchor);
#endif

            // run script if needed
            if (args.Length > scriptIndex)
            {
                string script = args[scriptIndex];
                string[] scriptargs = new string[args.Length - scriptIndex - 1];
                Array.Copy(args, scriptIndex + 1, scriptargs, 0, args.Length - scriptIndex - 1);

                scripting.ParseScript(dev, script, scriptargs);
            }

            // wait for commands
            Console.Error.WriteLine("Ready for commands. Type 'help' for a listing.");
            StringReader reader = null;
            string line = null;
            while (true)
            {
                if (dev.Script != null)
                {
                    Console.Error.WriteLine("Running script...");
                    reader = new StringReader(dev.Script);
                    dev.Script = null;
                }
                if (reader != null)
                {
                    line = reader.ReadLine();
#if DEBUG
                    Console.WriteLine("> {0}", line);
#endif
                }
                else
                {
                    Console.Write("> ");
                    line = Console.ReadLine();
                }
                if (String.IsNullOrEmpty(line))
                {
                    if (reader == null)
                    {
                        Console.Error.WriteLine("Enter a command, or 'help' for a list of commands.");
                    }
                    else
                    {
                        reader = null;
                    }
                }
                else if (line == "exit")
                {
                    net.RunCommand(Command.Exit);
                    break;
                }
                else
                {
                    string[] entry = line.Trim().Split(new char[]{' '}, 2);
                    bool handled = false;
                    string[] entryargs = entry.Length > 1 ? entry[1].Split(' ') : new string[] { };
                    int start = -1;
                    int idx = 0;
                    for (int i = 0; i < entryargs.Length; i++)
                    {
                        if (start > -1)
                        {
                            if (entryargs[i].EndsWith("\""))
                            {
                                entryargs[start] = entryargs[start] + ' ' + entryargs[i].Substring(0, entryargs[i].Length - 1);
                                start = -1;
                            }
                            else
                            {
                                entryargs[start] = entryargs[start] + ' ' + entryargs[i];
                            }
                        }
                        else if (entryargs[i].StartsWith("\""))
                        {
                            start = idx++;
                            if (entryargs[i].EndsWith("\""))
                            {
                                entryargs[start] = entryargs[i].Substring(1, entryargs[i].Length - 2);
                                start = -1;
                            }
                            else
                            {
                                entryargs[start] = entryargs[i].Substring(1);
                            }
                        }
                        else
                        {
                            entryargs[idx++] = entryargs[i];
                        }
                    }
                    Array.Resize<string>(ref entryargs, idx);
                    foreach (IModule mod in mods)
                    {
                        if (handled = mod.Run(dev, entry[0], entryargs))
                        {
#if DEBUG
                            Console.Error.WriteLine("Command handled by {0}", mod.GetType());
#endif
                            break;
                        }
                    }

                    if (!handled)
                    {
                        Console.Error.WriteLine("Invalid arguments or command '{0}'", entry[0]);
                    }
                }
            }

            // cleanup
            exploit.Disconnect();
        }
    }
}
