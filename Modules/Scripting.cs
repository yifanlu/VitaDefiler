using System;
using System.Collections.Generic;
using System.Linq;
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
            }
            return false;
        }
    }
}
