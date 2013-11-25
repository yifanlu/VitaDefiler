using System;
using System.Net;
using System.Net.Sockets;
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
        Echo = 9
    }
	
	public class CommandListener
	{
		private const int LISTEN_PORT = 4445;
		private static readonly IntPtr PSS_CODE_MEM_ALLOC = new IntPtr(0x814275A1);
		private static readonly IntPtr PSS_CODE_MEM_FREE = new IntPtr(0x8142767F);
		private static readonly IntPtr PSS_CODE_MEM_UNLOCK = new IntPtr(0x81427575);
		private static readonly IntPtr PSS_CODE_MEM_LOCK = new IntPtr(0x8142754D);
		private static TcpListener listener;
		
		public CommandListener ()
		{
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
			Console.WriteLine("Connection established!");
			while (true)
			{
				HandleCommands (sock);
			}
		}
		
		[SecurityCritical]
		public static void HandleCommands (Socket sock)
		{
			//Console.WriteLine("Test 0x{0:X}", System.Runtime.InteropServices.Marshal.AllocHGlobal(0x100));
			// get header
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
#if DEBUG
			Console.WriteLine("Recieved command {0}, length 0x{1:X}", cmd, len);
#endif
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
			Console.WriteLine("Sending response {0}, length 0x{1:X}", resp_cmd, resp.Length);
#endif
            byte[] packet = new byte[resp.Length + 8];
            Array.Copy(BitConverter.GetBytes((int)resp_cmd), 0, packet, 0, sizeof(int));
            Array.Copy(BitConverter.GetBytes(resp.Length), 0, packet, sizeof(int), sizeof(int));
            Array.Copy(resp, 0, packet, 8, resp.Length);
#if DEBUG
			Console.WriteLine("Response: {0}", BitConverter.ToString(packet));
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
						break;
					}
					case Command.AllocateCode:
					{
						int len = BitConverter.ToInt32(data, 0);
						IntPtr lenp = NativeFunctions.AllocData(4);
						NativeFunctions.Write(lenp, BitConverter.GetBytes(len), 4);
						int ret = NativeFunctions.Execute(PSS_CODE_MEM_ALLOC, lenp.ToInt32(), 0, 0, 0);
						resp = BitConverter.GetBytes(ret);
						NativeFunctions.FreeData(lenp);
						break;
					}
					case Command.FreeData:
					{
						NativeFunctions.FreeData(new IntPtr(BitConverter.ToInt32(data, 0)));
						break;
					}
					case Command.FreeCode:
					{
						int addr = BitConverter.ToInt32(data, 0);
						NativeFunctions.Execute(PSS_CODE_MEM_FREE, addr, 0, 0, 0);
						break;
					}
					case Command.WriteCode:
					case Command.WriteData:
					{
						if (cmd == Command.WriteCode)
						{
							NativeFunctions.Execute(PSS_CODE_MEM_UNLOCK, 0, 0, 0, 0);
						}
						IntPtr addr = new IntPtr(BitConverter.ToInt32(data, 0));
						NativeFunctions.Write(addr, data, sizeof(int));
						resp = BitConverter.GetBytes(data.Length);
						if (cmd == Command.WriteCode)
						{
							NativeFunctions.Execute(PSS_CODE_MEM_LOCK, 0, 0, 0, 0);
						}
						break;
					}
					case Command.ReadData:
					{
						IntPtr addr = new IntPtr(BitConverter.ToInt32(data, 0));
						int size = BitConverter.ToInt32(data, sizeof(int));
						resp = new byte[size];
						NativeFunctions.Read(addr, resp, 0);
						break;	
					}
					case Command.Execute:
					{
						int[] args = new int[5];
						for (int i = 0; i < data.Length / sizeof(int); i++)
						{
							args[i] = BitConverter.ToInt32(data, i * sizeof(int));
						}
						int ret = NativeFunctions.Execute(new IntPtr(args[0]), args[1], args[2], args[3], args[4]);
						resp = BitConverter.GetBytes(ret);
						break;
					}
					case Command.Echo:
					{
						Console.WriteLine(Encoding.ASCII.GetString(data));
						break;
					}
					default:
					{
						Console.WriteLine("Unrecognized command: {0}", cmd);
						break;
					}
				}
			}
			catch (Exception ex)
			{
				resp_cmd = Command.Error;
				resp = Encoding.ASCII.GetBytes(ex.Message);
				Console.WriteLine("Error running command {0}: {1}", cmd, ex.Message);
			}
		}
	}
}

