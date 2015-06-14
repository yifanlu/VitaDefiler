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
                Console.Error.WriteLine("usage: VitaDefiler.exe package [-nogui] [script]\n    package is path to PSM package\n    nogui starts client without GUI\n    script is the script to run");
                return;
            }
            if (!File.Exists(args[0]))
            {
                Console.Error.WriteLine("cannot find package file");
                return;
            }
            if (args.Length >= 2 && args[1] == "-nogui")
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
            Process[] potential = Process.GetProcessesByName("PsmDevice");
            foreach (Process process in potential)
            {
                Console.WriteLine("Killing PsmDevice process {0}", process.Id);
                process.Kill();
            }

            // initialize the modules
            List<IModule> mods = new List<IModule>();
            foreach (Type t in Mods)
            {
                if (typeof(IModule).IsAssignableFrom(t))
                {
                    mods.Add((IModule)Activator.CreateInstance(t));
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
                Console.Error.WriteLine("Enabling GUI");
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
                    foreach (IModule mod in mods)
                    {
                        if (handled = mod.Run(dev, entry[0], entry.Length > 1 ? entry[1].Split(' ') : new string[] { }))
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
