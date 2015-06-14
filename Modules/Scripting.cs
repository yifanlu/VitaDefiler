using System;
using System.IO;
using System.Text;

namespace VitaDefiler.Modules
{
    class Scripting : IModule
    {
        public bool Run(Device dev, string cmd, string[] args)
        {
            switch (cmd)
            {
                case "set":
                    {
                        if (args.Length >= 2)
                        {
                            uint addr = args[0].ToVariable(dev).Data;
                            uint val = args[1].ToVariable(dev).Data;
                            bool isCode = args[0].ToVariable(dev).IsCode;
                            Memory.Write(dev, addr, sizeof(uint), true, val);
                            return true;
                        }
                    }
                    break;
                case "get":
                    {
                        if (args.Length >= 1)
                        {
                            uint addr = args[0].ToVariable(dev).Data;
                            uint data;
                            Memory.Read(dev, addr, sizeof(uint), out data);
                            dev.LastReturn = data;
                            return true;
                        }
                    }
                    break;
                case "if":
                    break;
                case "while":
                    break;
                case "script":
                    {
                        if (args.Length >= 1)
                        {
                            string[] scriptargs = new string[args.Length - 1];
                            Array.Copy(args, 1, scriptargs, 0, args.Length - 1);
                            ParseScript(dev, args[0], scriptargs);
                            return true;
                        }
                    }
                    break;
            }
            if (cmd[0] == '#')
            {
                return true; // skip comments
            }
            return false;
        }

        public bool ParseScript(Device dev, string filename, string[] args)
        {
            try
            {
                string script = File.ReadAllText(filename, Encoding.ASCII);
                // parse args
                string[] tokens = script.Split(new char[] { ' ', '\r', '\n' });
                foreach (string token in tokens)
                {
                    if (token.Length >= 1 && token[0] == '@')
                    {
                        int idx;
                        if (!Int32.TryParse(token.Substring(1), out idx))
                        {
                            continue;
                        }
                        if (idx-1 >= args.Length)
                        {
                            Console.Error.WriteLine("Not enough arguments specified: {0}, have {1} args", token, args.Length);
                            return false;
                        }
                        else
                        {
                            script = script.Replace(token, args[idx - 1]);
                        }
                    }
                }
                // set script
                dev.Script = script;
                return true;
            }
            catch (IOException ex)
            {
                Console.Error.WriteLine("Error parsing script: {0}", ex.Message);
                return false;
            }
        }
    }
}
