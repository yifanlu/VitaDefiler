/* PlayStation(R)Mobile SDK 1.11.01
 * Copyright (C) 2013 Sony Computer Entertainment Inc.
 * All Rights Reserved.
 */


using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Security;

using Sce.PlayStation.Core;
using Sce.PlayStation.Core.Environment;
using Sce.PlayStation.Core.Graphics;
using Sce.PlayStation.Core.Input;
using Sce.PlayStation.HighLevel.UI;

namespace VitaDefilerClient
{
	
    public class AppMain
    {
		public static IntPtr src = new IntPtr(0);
		public static byte[] dest = new byte[0x100];
        private static GraphicsContext graphics;
		private static readonly int LOG_SIZE = 20;
		private static string log;
		private static Label label;
		private static bool enable_gui;
		private static bool gui_enabled;
        
        public static void Main (string[] args)
        {
			// Init log
			log = string.Empty;
			for (int i = 0; i < LOG_SIZE; i++)
			{
				log += "\n";
			}
			
			AppMain.LogLine("Vita Defiler Client started");
			
			CommandListener.InitializeNetwork ();
			typeof(NativeFunctions).GetMethods(); // take care of lazy init
			
			Console.WriteLine("XXVCMDXX:DONE"); // signal PC
			
			gui_enabled = false;
			
            while (true) {
				if (enable_gui)
				{
					enable_gui = false;
					InitializeGraphics ();
				}
				if (gui_enabled)
				{
					Update ();
	            	Render ();
				}
            }
        }
		
		public static void EnableGUI ()
		{
			enable_gui = true;
		}
		
		public static void LogLine (string format, params object[] args)
		{
			string line = string.Format(format, args);
#if DEBUG
			Console.WriteLine(line);
#endif
			int lines = log.Length - log.Replace("\n", "").Length;
			if (lines >= LOG_SIZE)
			{
				log = log.Substring(log.IndexOf('\n')+1);
			}
			log += line + "\n";
		}

        public static void InitializeGraphics ()
        {
            // Set up the graphics system
            graphics = new GraphicsContext ();
            
            // Initialize UI Toolkit
            UISystem.Initialize (graphics);

            // Create scene
            Scene myScene = new Scene();
            label = new Label();
            label.X = 10.0f;
            label.Y = 10.0f;
			label.Width = graphics.Screen.Width - 20.0f;
			label.Height = graphics.Screen.Height - 20.0f;
			label.TextTrimming = TextTrimming.None;
            myScene.RootWidget.AddChildLast(label);
            // Set scene
            UISystem.SetScene(myScene, null);
			
			gui_enabled = true;
        }
		
		public static void Update ()
		{
			// update log
			label.Text = log;
			
            // Query touch for current state
            List<TouchData> touchDataList = Touch.GetData (0);
            
            // Update UI Toolkit
            UISystem.Update(touchDataList);
		}

        public static void Render ()
        {
            // Clear the screen
            graphics.SetClearColor (0.0f, 0.0f, 255.0f, 0.0f);
            graphics.Clear ();
            
            // Render UI Toolkit
            UISystem.Render ();
            
            // Present the screen
            graphics.SwapBuffers ();
        }
    }
}
