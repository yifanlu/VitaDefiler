using System;
using UnityEngine;

namespace VitaDefilerClient
{
    public class AppMain
    {
		public static IntPtr src = new IntPtr(0);
		public static byte[] dest = new byte[0x100];
        
        public static void Start ()
        {
			LogLine("Vita Defiler Client started");
			
			CommandListener.InitializeNetwork ();
			typeof(NativeFunctions).GetMethods(); // take care of lazy init

			Console.WriteLine("XXVCMDXX:DONE"); // signal PC
        }
		
		public static void EnableGUI ()
		{
		}
		
		public static void LogLine (string format, params object[] args)
		{
			string line = string.Format(format, args);
			VitaDefilerClientUnity.LogLine(line);
		}
    }
}
