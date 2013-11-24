using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VitaDefiler.Modules
{
    class Code : IModule
    {
        public bool Run(Device dev, string cmd, string[] args)
        {
            switch (cmd)
            {
                case "compile":
                    if (args.Length == 2)
                    {
                        Compile(dev, args[1], args[0].ToVariable(dev).Data);
                        return true;
                    }
                    break;
                case "execute":
                case "exec":
                    if (args.Length == 1)
                    {
                        Execute(dev, args[0].ToVariable(dev).Data);
                        return true;
                    }
                    break;
            }
            return false;
        }

        public void Compile(Device dev, string file, uint addr)
        {
        }

        public void Execute(Device dev, uint addr)
        {
        }
    }
}
