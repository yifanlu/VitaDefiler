using System;
using System.Collections.Generic;
using System.Threading;
using System.IO;
using VitaDefiler.Modules;
using VitaDefiler.PSM;

namespace VitaDefiler
{
    public interface IPlayer
    {
    }

    public class Defiler
    {
        public delegate void ProgressCallback(float percent);
        static readonly Type[] Mods = {typeof(Code), typeof(General), typeof(Memory), typeof(FileIO), typeof(Scripting)};
        static TextWriter _logstream = Console.Out;
        static TextWriter _errstream = Console.Error;
        static TextWriter _msgstream = Console.Out;

        public static void LogLine(string format, params object[] parameters)
        {
            _logstream.WriteLine(string.Format(format, parameters));
        }

        public static void ErrLine(string format, params object[] parameters)
        {
            _errstream.WriteLine(string.Format(format, parameters));
        }

        public static void MsgLine(string format, params object[] parameters)
        {
            _msgstream.WriteLine(string.Format(format, parameters));
        }

        public static void SetLogger(TextWriter log, TextWriter err, TextWriter msg)
        {
            _logstream = log;
            _errstream = err;
            _msgstream = msg;
        }

        public static IPlayer[] FindPlayers()
        {
            ConnectionFinder.PlayerInfo[] players = ConnectionFinder.FindPlayers();
            IPlayer[] baseplayers = new IPlayer[players.Length];
            for (int i = 0; i < players.Length; i++)
            {
                baseplayers[i] = players[i];
            }
            return baseplayers;
        }

        public static IDevice Setup(IPlayer hint = null, ProgressCallback pgs = null, string package = null, bool enableGui = true, bool noExploit = false)
        {
            if (pgs != null) pgs(0.0f);

            // set environment variables
            Environment.SetEnvironmentVariable("SCE_PSM_SDK", Path.Combine(Environment.CurrentDirectory, "support/psm"));

            // set up usb
            Exploit exploit;
            string host;
            int port;
            
#if USE_UNITY
            ExploitFinder.CreateFromWireless(hint as ConnectionFinder.PlayerInfo?, package, noExploit, out exploit, out host, out port);
#else
            ExploitFinder.CreateFromUSB(package, noExploit, out exploit, out host, out port);
#endif

            if (noExploit)
            {
                return null;
            }

            if (pgs != null) pgs(0.2f);

#if !NO_EXPLOIT
            uint images_hash_ptr;
            uint[] funcs = new uint[5];
            uint logline_func;
            uint libkernel_anchor;
            Defiler.MsgLine("Defeating ASLR...");
            exploit.DefeatASLR(out images_hash_ptr, out funcs[0], out funcs[1], out funcs[2], out funcs[3], out funcs[4], out libkernel_anchor);
            if (pgs != null) pgs(0.4f);
            // exploit vita

            Defiler.MsgLine("Escalating privileges...");
            exploit.EscalatePrivilege(images_hash_ptr);
            if (pgs != null) pgs(0.6f);
#endif

#if USE_UNITY
            exploit.ResumeVM(); // The network listener is already listening in Unity.
#else
            exploit.StartNetworkListener();
            Defiler.MsgLine("Vita exploited.");
#endif


            //Thread tt = new Thread(() =>
            //{
            //});
                //tt.Start();

            // set up network
            Network net = new Network();
            if (net.Connect(host, port))
            {
                Defiler.MsgLine("Connected to Vita network");
                if (pgs != null) pgs(0.8f);
            }
            else
            {
                Defiler.MsgLine("Failed to create net listener. Exiting.");
                exploit.Disconnect();
                return null;
            }

            byte[] resp;

            // enable gui
            if (enableGui)
            {
                Defiler.MsgLine("Enabling display output");
                net.RunCommand(Command.EnableGUI, out resp);
            }
            
#if !NO_EXPLOIT
            // pass in function pointers
            if (net.RunCommand(Command.SetFuncPtrs, funcs, out resp) == Command.Error)
            {
                Defiler.ErrLine("ERROR setting function pointers!");
            }
#endif
            if (pgs != null) pgs(0.9f);

            // set up RPC context
            Device dev = new Device(exploit, net);
            
#if !NO_EXPLOIT
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
#endif
            if (pgs != null) pgs(1.0f);
            return dev;
        }

        public static void Exit(IDevice device)
        {
            Device dev = device as Device;
            dev.Network.RunCommand(Command.Exit);
            dev.Exploit.Disconnect();
        }

        public static void CommandRunner(IDevice device, string script = null, string[] args = null)
        {
            Device dev = device as Device;
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

            // run script
            if (script != null && args != null)
            {
                scripting.ParseScript(dev, script, args);
            }

            // wait for commands
            Console.WriteLine("Ready for commands. Type 'help' for a listing.");
            StringReader reader = null;
            string line = null;
            while (true)
            {
                if (dev.Script != null)
                {
                    Console.WriteLine("Running script...");
                    reader = new StringReader(dev.Script);
                    dev.Script = null;
                }
                if (reader != null)
                {
                    line = reader.ReadLine();
#if DEBUG
                    Defiler.LogLine("> {0}", line);
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
                        Console.WriteLine("Enter a command, or 'help' for a list of commands.");
                    }
                    else
                    {
                        reader = null;
                    }
                }
                else if (line == "exit")
                {
                    break;
                }
                else
                {
                    string[] entry = line.Trim().Split(new char[] { ' ' }, 2);
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
                            Console.WriteLine("Command handled by {0}", mod.GetType());
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
        }
    }
}
