using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Threading;

namespace VitaDefilerClient
{
    public enum Command
    {
        Error = 0,
        AllocateData = 1,
        AllocateCode = 2,
        FreeData = 3,
        FreeCode = 4,
        WriteData = 5,
        WriteCode = 6,
        ReadData = 7,
        Execute = 8,
        Echo = 9,
		SetFuncPtrs = 10,
		Exit = 11,
		PushFile = 12,
		PullFile = 13,
		GetLogger = 14,
		EnableGUI = 15
    }
	
	public class CommandListener
	{
		
		private const int LISTEN_PORT = 4445;
		private static IntPtr pss_code_mem_alloc = IntPtr.Zero;
		private static IntPtr pss_code_mem_free = IntPtr.Zero;
		private static IntPtr pss_code_mem_unlock = IntPtr.Zero;
		private static IntPtr pss_code_mem_lock = IntPtr.Zero;
		private delegate int LoggerDelegate([MarshalAs(UnmanagedType.LPStr)]string line);
		private static LoggerDelegate log_delegate = LogLine;
		private static IntPtr log_delegate_ptr = IntPtr.Zero;
		private static TcpListener listener;
		
		private static bool alive = true;
		
		public CommandListener ()
		{
		}
		
		private static int LogLine([MarshalAs(UnmanagedType.LPStr)]string line)
		{
			AppMain.LogLine("{0}", line);
			Console.WriteLine("{0}", line);
			return 0;
		}
		
		public static void InitializeNetwork ()
		{
			IPHostEntry host = Dns.GetHostEntry(Dns.GetHostName());
			string ipaddr = string.Empty;
			foreach (IPAddress ip in host.AddressList)
			{
				if (ip.AddressFamily == AddressFamily.InterNetwork)
				{
					ipaddr = ip.ToString();
					break;
				}
			}
			listener = new TcpListener(IPAddress.Any, LISTEN_PORT);
			listener.Start();
			Console.WriteLine("XXVCMDXX:IP:{0}:{1}", ipaddr, LISTEN_PORT);
			AppMain.LogLine("Started listening at {0}:{1}", ipaddr, LISTEN_PORT);

			// This can be called right away because AcceptSocket will wait until VitaDefiler
			// connects to it, which is after privileges have been escalated.
			StartListener();
		}
		
		[SecuritySafeCritical]
		public static void StartListener()
		{
			Thread thread = new Thread(Listen);
			thread.Start();
		}
		
		[SecurityCritical]
		public static void Listen()
		{
			Socket sock = listener.AcceptSocket();
			IPEndPoint remote = sock.RemoteEndPoint as IPEndPoint ?? new IPEndPoint(IPAddress.Any, 0);
			AppMain.LogLine("Connection established with client {0}:{1}", remote.Address, remote.Port);
			AppMain.LogLine("Ready for commands.");
			while (alive)
			{
				HandleCommands (sock);
			}
			Environment.Exit(0);
		}
		
		[SecurityCritical]
		public static void HandleCommands (Socket sock)
		{
			byte[] header = new byte[8];
			int read = 0;
			int size = 8;
			while (read < size)
			{
				read += sock.Receive(header, read, size - read, SocketFlags.None);
			}
			// get data
			Command cmd = (Command)BitConverter.ToInt32(header, 0);
			int len = BitConverter.ToInt32(header, 4);
			AppMain.LogLine("Recieved command {0}, length 0x{1:X}", cmd, len);
			byte[] data = new byte[len];
			int recv = 0;
			while (recv < len)
			{
				recv += sock.Receive(data, recv, len - recv, SocketFlags.None);
			}
			// process command
			Command resp_cmd;
			byte[] resp;
			ProcessCommand (cmd, data, out resp_cmd, out resp);
            // create and send packet
#if DEBUG
			AppMain.LogLine("Sending response {0}, length 0x{1:X}", resp_cmd, resp.Length);
#endif
            byte[] packet = new byte[resp.Length + 8];
            Array.Copy(BitConverter.GetBytes((int)resp_cmd), 0, packet, 0, sizeof(int));
            Array.Copy(BitConverter.GetBytes(resp.Length), 0, packet, sizeof(int), sizeof(int));
            Array.Copy(resp, 0, packet, 8, resp.Length);
#if DEBUG
			AppMain.LogLine("Response: {0}", BitConverter.ToString(packet));
#endif
            sock.Send(packet);
		}
		
		[SecurityCritical]
		public static void ProcessCommand (Command cmd, byte[] data, out Command resp_cmd, out byte[] resp)
		{
			resp = new byte[0];
			resp_cmd = cmd;
			try
			{
				switch (cmd)
				{
					case Command.AllocateData:
					{
						int len = BitConverter.ToInt32(data, 0);
						IntPtr ret = NativeFunctions.AllocData(len);
						resp = BitConverter.GetBytes(ret.ToInt32());
						AppMain.LogLine("Allocated {0} bytes at 0x{1:x}", len, ret.ToInt32());
						break;
					}
					case Command.AllocateCode:
					{
						IntPtr lenp = NativeFunctions.AllocData(4);
						NativeFunctions.Write(lenp, data, 0, 4);
						int ret = NativeFunctions.Execute(pss_code_mem_alloc, lenp.ToInt32(), 0, 0, 0);
						resp = BitConverter.GetBytes(ret);
						NativeFunctions.FreeData(lenp);
						AppMain.LogLine("Allocated code at 0x{0:x}", ret);
						break;
					}
					case Command.FreeData:
					{
						IntPtr ptr = new IntPtr(BitConverter.ToInt32(data, 0));
						NativeFunctions.FreeData(ptr);
						AppMain.LogLine("Freed data 0x{0:x}", ptr.ToInt32());
						break;
					}
					case Command.FreeCode:
					{
						int addr = BitConverter.ToInt32(data, 0);
						NativeFunctions.Execute(pss_code_mem_free, addr, 0, 0, 0);
						AppMain.LogLine("Freed code 0x{0:x}", addr);
						break;
					}
					case Command.WriteCode:
					case Command.WriteData:
					{
						if (cmd == Command.WriteCode)
						{
							NativeFunctions.Execute(pss_code_mem_unlock, 0, 0, 0, 0);
						}
						IntPtr addr = new IntPtr(BitConverter.ToInt32(data, 0));
						int size = BitConverter.ToInt32(data, sizeof(int));
						NativeFunctions.Write(addr, data, 2 * sizeof(int), size);
						resp = BitConverter.GetBytes(size);
						if (cmd == Command.WriteCode)
						{
							NativeFunctions.Execute(pss_code_mem_lock, 0, 0, 0, 0);
						}
						AppMain.LogLine("Wrote {0} bytes at 0x{1:x}", size, addr.ToInt32());
						break;
					}
					case Command.ReadData:
					{
						IntPtr addr = new IntPtr(BitConverter.ToInt32(data, 0));
						int size = BitConverter.ToInt32(data, sizeof(int));
						resp = new byte[size];
						NativeFunctions.Read(addr, resp, 0, size);
						AppMain.LogLine("Read {0} bytes at 0x{1:x}", size, addr.ToInt32());
						break;	
					}
					case Command.Execute:
					{
						int argc = data.Length / sizeof(int);
						int[] args = new int[5];
						for (int i = 0; i < argc; i++)
						{
							args[i] = BitConverter.ToInt32(data, i * sizeof(int));
						}
						AppMain.LogLine("Executing 0x{0:x} with {1} args", args[0], argc-1);
						int ret = (int)NativeFunctions.Execute(new IntPtr(args[0]), args[1], args[2], args[3], args[4]);
						resp = BitConverter.GetBytes(ret);
						AppMain.LogLine("Code returned: 0x{0:x}", ret);
						break;
					}
					case Command.Echo:
					{
						AppMain.LogLine(Encoding.ASCII.GetString(data));
						break;
					}
					case Command.SetFuncPtrs:
					{
						pss_code_mem_alloc = new IntPtr(BitConverter.ToInt32(data, 0));
						pss_code_mem_free = new IntPtr(BitConverter.ToInt32(data, 4));
						pss_code_mem_unlock = new IntPtr(BitConverter.ToInt32(data, 8));
						pss_code_mem_lock = new IntPtr(BitConverter.ToInt32(data, 12));
						AppMain.LogLine("Functions found:");
						AppMain.LogLine("pss_code_mem_alloc: 0x{0:x}", pss_code_mem_alloc.ToInt32());
						AppMain.LogLine("pss_code_mem_free: 0x{0:x}", pss_code_mem_free.ToInt32());
						AppMain.LogLine("pss_code_mem_unlock: 0x{0:x}", pss_code_mem_unlock.ToInt32());
						AppMain.LogLine("pss_code_mem_lock: 0x{0:x}", pss_code_mem_lock.ToInt32());
						break;
					}
					case Command.Exit:
					{
						alive = false;
						resp = BitConverter.GetBytes(0);
						break;
					}
					case Command.PushFile:
					{
						int pathlen = BitConverter.ToInt32(data, 0);
						string path = Encoding.ASCII.GetString(data, sizeof(int), pathlen);
						try
						{
							AppMain.LogLine("Write: {0}", path);
							using (FileStream fs = File.OpenWrite(path))
							{
								fs.Write(data, sizeof(int) + pathlen, data.Length - sizeof(int) - pathlen);
							}
						}
						catch (IOException ex)
						{
							AppMain.LogLine("Error writing file: {0}", ex.Message);
							resp_cmd = Command.Error;
							resp = Encoding.ASCII.GetBytes(ex.Message);
						}
						break;
					}
					case Command.PullFile:
					{
						int pathlen = BitConverter.ToInt32(data, 0);
						string path = Encoding.ASCII.GetString(data, sizeof(int), pathlen);
						try
						{
							AppMain.LogLine("Read: {0}", path);
							resp = File.ReadAllBytes(path);
						}
						catch (IOException ex)
						{
							AppMain.LogLine("Error reading file: {0}", ex.Message);
							resp_cmd = Command.Error;
							resp = Encoding.ASCII.GetBytes(ex.Message);
						}
						break;
					}
					case Command.GetLogger:
					{
						if (log_delegate_ptr == IntPtr.Zero)
						{
							log_delegate_ptr = Marshal.GetFunctionPointerForDelegate(log_delegate);
						}
						AppMain.LogLine("Logger at 0x{0:x}", (uint)log_delegate_ptr);
						resp = BitConverter.GetBytes((uint)log_delegate_ptr);
						break;
					}
					case Command.EnableGUI:
					{
						AppMain.EnableGUI ();
						break;
					}
					default:
					{
						AppMain.LogLine("Unrecognized command: {0}", cmd);
						break;
					}
				}
			}
			catch (Exception ex)
			{
				resp_cmd = Command.Error;
				resp = Encoding.ASCII.GetBytes(ex.Message);
				AppMain.LogLine("Error running command {0}: {1}", cmd, ex.Message);
			}
		}
	}
}

