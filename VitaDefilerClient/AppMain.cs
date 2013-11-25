/* PlayStation(R)Mobile SDK 1.11.01
 * Copyright (C) 2013 Sony Computer Entertainment Inc.
 * All Rights Reserved.
 */


using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
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
        
        public static void Main (string[] args)
        {
            InitializeGraphics ();
            Render ();
			
			CommandListener.InitializeNetwork ();
			typeof(NativeFunctions).GetMethods(); // take care of lazy init
			
			Console.WriteLine("XXVCMDXX:DONE"); // signal PC

            while (true) {
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
