using System;
using System.Diagnostics;
using VitaDefiler;

namespace VitaDefilerConsole
{
    class Program
    {
        static void Main(string[] args)
        {
            IDevice dev;
            int scriptIndex = 0;
            bool enablegui = true;
            bool noexploit = false;
            string package = null;
            string script = null;
            string[] scriptargs = new string[0];

            if (args.Length < 1)
            {
                Console.Error.WriteLine("usage: VitaDefiler.exe [-rpc] [-pkg package] [-nodisp] [script args]\n    package is path to PSM package\n        You must run with -pkg for the first time!\n    nodisp starts client without logging to screen\n    script is the script to run\n    args are arguments for the script");
                return;
            }

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                switch (arg)
                {
                    case "-nodisp":
                        ++scriptIndex;
                        enablegui = false;
                        break;

                    case "-noexploit":
                        ++scriptIndex;
                        noexploit = true;
                        break;

                    case "-pkg":
                        package = i + 1 < args.Length ? args[i + 1] : null;
                        i++;
                        scriptIndex += 2;
                        break;

                    case "-rpc":
                        break;

                    default:
                        if (arg.StartsWith("-"))
                        {
                            Console.Error.WriteLine("Ignoring unknown argument: {0}", arg);
                        }
                        break;
                }
            }

            // kill PSM
            if (Environment.OSVersion.VersionString.Contains("Microsoft Windows"))
            {
                Process[] potential = Process.GetProcesses();
                foreach (Process process in potential)
                {
                    if (process.ProcessName.StartsWith("PsmDevice"))
                    {
                        Console.WriteLine("Killing PsmDevice process {0}", process.Id);
                        process.Kill();
                    }
                }
            }

            // parse script args
            if (args.Length > scriptIndex && !args[scriptIndex].StartsWith("-"))
            {
                script = args[scriptIndex];
                scriptargs = new string[args.Length - scriptIndex - 1];
                Array.Copy(args, scriptIndex + 1, scriptargs, 0, args.Length - scriptIndex - 1);
            }

            dev = Defiler.Setup(null, null, package, enablegui, noexploit);

            if (dev != null)
            {
                Defiler.CommandRunner(dev, script, scriptargs);
            }
        }
    }
}
