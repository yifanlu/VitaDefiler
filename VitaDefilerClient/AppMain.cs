/* PlayStation(R)Mobile SDK 1.11.01
 * Copyright (C) 2013 Sony Computer Entertainment Inc.
 * All Rights Reserved.
 */


using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;

using Sce.PlayStation.Core;
using Sce.PlayStation.Core.Environment;
using Sce.PlayStation.Core.Graphics;
using Sce.PlayStation.Core.Input;
using Sce.PlayStation.HighLevel.UI;

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
	
    public class AppMain
    {
		private const int LISTEN_PORT = 4445;
		private static readonly IntPtr PSS_CODE_MEM_ALLOC = new IntPtr(0);
		private static readonly IntPtr PSS_CODE_MEM_FREE = new IntPtr(0);
		private static readonly IntPtr PSS_CODE_MEM_UNLOCK = new IntPtr(0);
		private static readonly IntPtr PSS_CODE_MEM_LOCK = new IntPtr(0);
		public static IntPtr src = new IntPtr(0);
		public static byte[] dest = new byte[0x100];
        private static GraphicsContext graphics;
		private static TcpListener listener;
        
        public static void Main (string[] args)
        {
            InitializeGraphics ();
            Render ();
			
			InitializeNetwork ();
			
			Console.WriteLine("XXVCMDXX:DONE"); // signal PC
			
			Socket sock = listener.AcceptSocket();
			Console.WriteLine("Connection established!");

            while (true) {
            }
        }
		
		public static void HandleCommands (Socket sock)
		{
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
            byte[] packet = new byte[resp.Length + 8];
            BitConverter.GetBytes(((ulong)resp_cmd << 32) | (uint)resp.Length);
            Array.Copy(BitConverter.GetBytes((int)resp_cmd), 0, packet, 0, 4);
            Array.Copy(BitConverter.GetBytes(resp.Length), 0, packet, 4, 4);
            Array.Copy(data, 0, packet, 8, resp.Length);
            sock.Send(packet);
		}
		
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
						int ret = NativeFunctions.Execute(PSS_CODE_MEM_ALLOC, len, 0, 0, 0);
						resp = BitConverter.GetBytes(ret);
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
						int[] args = new int[data.Length / sizeof(int)];
						for (int i = 0; i < args.Length; i++)
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
			}
		}

        public static void InitializeGraphics ()
        {
            // Set up the graphics system
            graphics = new GraphicsContext ();
            
            // Initialize UI Toolkit
            UISystem.Initialize (graphics);

            // Create scene
            Scene myScene = new Scene();
            Label label = new Label();
            label.X = 10.0f;
            label.Y = 50.0f;
            label.Text = "Client started!";
            myScene.RootWidget.AddChildLast(label);
            // Set scene
            UISystem.SetScene(myScene, null);
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
			Console.WriteLine("XXVCMDXX:{0}:{1}", ipaddr, LISTEN_PORT);
		}

        public static void Render ()
        {
            // Clear the screen
            graphics.SetClearColor (0.0f, 0.0f, 0.0f, 0.0f);
            graphics.Clear ();
            
            // Render UI Toolkit
            UISystem.Render ();
            
            // Present the screen
            graphics.SwapBuffers ();
        }
    }
}
