using System;
using System.Collections.Generic;
using VitaDefiler.Modules;
using System.Threading;

namespace VitaDefiler
{
    class Program
    {
        static readonly Type[] Mods = {typeof(Code), typeof(General), typeof(Memory)};

        static void ConsoleCallback(string text)
        {
            Console.WriteLine("[Vita] ", text);
        }

        static void Main(string[] args)
        {
            if (args.Length < 3)
            {
                Console.Error.WriteLine("usage: VitaDefiler.exe port package\n    port is serial port, ex: COM5\n    package is path to PSM package");
                return;
            }


            // initialize the modules
            List<IModule> mods = new List<IModule>();
            foreach (Type t in mods)
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
                    string[] cmd = text.Split(':');
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

            // exploit vita
            usb.EscalatePrivilege();

            // wait for commands
            Device dev = new Device(usb, net);
            while (true)
            {
                Console.Write("> ");
                string line = Console.ReadLine();
                if (line == "exit")
                {
                    break;
                }
                if (String.IsNullOrWhiteSpace(line))
                {
                    Console.Error.WriteLine("Enter a command, or 'help' for a list of commands.");
                }
                else
                {
                    string[] entry = line.Split(new char[]{' '}, 2);
                    foreach (IModule mod in mods)
                    {
                        if (mod.Run(dev, entry[0], entry.Length > 1 ? entry[1].Split(' ') : new string[]{}))
                        {
#if DEBUG
                            Console.Error.WriteLine("Command handled by {0}", mod.GetType());
#endif
                            break;
                        }
                    }
                    Console.Error.WriteLine("Invalid arguments or command '{0}'", entry[0]);
                }
            }

            // cleanup
            usb.Disconnect();
        }
    }
}
