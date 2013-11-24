using System;
using System.Collections.Generic;
using VitaDefiler.Modules;

namespace VitaDefiler
{
    class Program
    {
        static readonly Type[] Mods = {typeof(Code), typeof(General), typeof(Memory)};

        static void Main(string[] args)
        {
            // initialize the modules
            List<IModule> mods = new List<IModule>();
            foreach (Type t in mods)
            {
                if (typeof(IModule).IsAssignableFrom(t))
                {
                    mods.Add((IModule)Activator.CreateInstance(t));
                }
            }

            // intitalize USB/exploit
            Device dev = new Device();

            // wait for commands
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
        }
    }
}
