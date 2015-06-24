using System;
using System.Collections.Generic;
using System.Threading;
using System.Diagnostics;
using System.IO;
using VitaDefiler.Modules;

namespace VitaDefiler
{
    class Program
    {
        static readonly Type[] Mods = {typeof(Code), typeof(General), typeof(Memory), typeof(FileIO), typeof(Scripting)};

        static void Main(string[] args)
        {
            bool enablegui = true;

            if (args.Length < 1)
            {
                Console.Error.WriteLine("usage: VitaDefiler.exe package [-nodisp] [script args]\n    package is path to PSM package\n    nodisp starts client without logging to screen\n    script is the script to run\n    args are arguments for the script");
                return;
            }
            if (!File.Exists(args[0]))
            {
                Console.Error.WriteLine("cannot find package file");
                return;
            }
            if (args.Length >= 2 && args[1] == "-nodisp")
            {
                enablegui = false;
            }
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
                if (process.ProcessName.StartsWith("PsmDevice"))
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
            USB usb = new USB(args[0], null);
            ManualResetEvent doneinit = new ManualResetEvent(false);
            string host = string.Empty;
            int port = 0;
            usb.Connect((text) =>
            {
                if (text.StartsWith("XXVCMDXX:"))
                {
#if DEBUG
                    Console.Error.WriteLine("[Vita] {0}", text);
#endif
                    string[] cmd = text.Trim().Split(':');
                    switch (cmd[1])
                    {
                        case "IP":
                            host = cmd[2];
                            port = Int32.Parse(cmd[3]);
                            Console.Error.WriteLine("Found Vita network at {0}:{1}", host, port);
                            break;
                        case "DONE":
                            Console.Error.WriteLine("Vita done initializing");
                            doneinit.Set();
                            break;
                        default:
                            Console.Error.WriteLine("Unrecognized startup command");
                            break;
                    }
                }
                else
                {
                    Console.Error.WriteLine("[Vita] {0}", text);
                }
            });
            Console.Error.WriteLine("Waiting for app to finish launching...");
            doneinit.WaitOne();

            uint images_hash_ptr;
            uint[] funcs = new uint[5];
            uint logline_func;
            uint libkernel_anchor;
            Console.Error.WriteLine("Defeating ASLR...");
            usb.DefeatASLR(out images_hash_ptr, out funcs[0], out funcs[1], out funcs[2], out funcs[3], out funcs[4], out libkernel_anchor);
#if !NO_ESCALATE_PRIVILEGES
            // exploit vita
            Console.Error.WriteLine("Escalating privileges...");
            usb.EscalatePrivilege(images_hash_ptr);
            //Thread tt = new Thread(() =>
            //{
                usb.StartNetworkListener();
                Console.Error.WriteLine("Vita exploited.");
            //});
                //tt.Start();
#endif

            // set up network
            Network net = new Network();
            if (net.Connect(host, port))
            {
                Console.Error.WriteLine("Connected to Vita network");
            }
            else
            {
                Console.Error.WriteLine("Failed to create net listener. Exiting.");
                usb.Disconnect();
                return;
            }

            byte[] resp;

            // enable gui
            if (enablegui)
            {
                Console.Error.WriteLine("Enabling display output");
                net.RunCommand(Command.EnableGUI, out resp);
            }

            // pass in function pointers
            if (net.RunCommand(Command.SetFuncPtrs, funcs, out resp) == Command.Error)
            {
                Console.Error.WriteLine("ERROR setting function pointers!");
            }

            // set up RPC context
            Device dev = new Device(usb, net);

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

            // run script if needed
            if ((!enablegui && args.Length >= 3) || (enablegui && args.Length >= 2))
            {
                string script;
                string[] scriptargs;
                if (enablegui)
                {
                    script = args[1];
                    scriptargs = new string[args.Length - 2];
                    Array.Copy(args, 2, scriptargs, 0, args.Length - 2);
                }
                else
                {
                    script = args[2];
                    scriptargs = new string[args.Length - 3];
                    Array.Copy(args, 3, scriptargs, 0, args.Length - 3);
                }
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
            usb.Disconnect();
        }
    }
}
