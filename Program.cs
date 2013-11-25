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
        static readonly Type[] Mods = {typeof(Code), typeof(General), typeof(Memory)};

        static void ConsoleCallback(string text)
        {
            Console.WriteLine("[Vita] {0}", text);
        }

        static void Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.Error.WriteLine("usage: VitaDefiler.exe port package\n    port is serial port, ex: COM5\n    package is path to PSM package");
                return;
            }
            if (!File.Exists(args[1]))
            {
                Console.Error.WriteLine("cannot find package file");
                return;
            }

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
            USB usb = new USB(args[0], args[1]);
            ManualResetEvent doneinit = new ManualResetEvent(false);
            string host = string.Empty;
            int port = 0;
            usb.Connect((text) =>
            {
                ConsoleCallback(text);
                if (text.StartsWith("XXVCMDXX:"))
                {
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
            });
            Console.Error.WriteLine("Waiting for app to finish launching...");
            doneinit.WaitOne();

            // exploit vita
            usb.EscalatePrivilege();
            usb.StartNetworkListener();
            Console.Error.WriteLine("Vita exploited.");

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

            // wait for commands
            Console.Error.WriteLine("Ready for commands. Type 'help' for a listing.");
            Device dev = new Device(usb, net);
            while (true)
            {
                Console.Write("> ");
                string line = Console.ReadLine();
                if (line == "exit")
                {
                    break;
                }
                if (String.IsNullOrEmpty(line))
                {
                    Console.Error.WriteLine("Enter a command, or 'help' for a list of commands.");
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
