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
                    if (args.Length >= 1)
                    {
                        int[] callargs = new int[args.Length];
                        for (int i = 0; i < args.Length; i++)
                        {
                            Variable v = args[i].ToVariable(dev);
                            callargs[i] = (int)v.Data;
                        }
                        Execute(dev, callargs);
                        return true;
                    }
                    break;
            }
            return false;
        }

        public void Compile(Device dev, string file, uint addr)
        {
        }

        public void Execute(Device dev, int[] args)
        {
            byte[] resp;
            if (dev.Network.RunCommand(Command.Execute, args, out resp) != Command.Error)
            {
                Console.Error.WriteLine("Return value: 0x{0:X}", BitConverter.ToInt32(resp, 0));
            }
        }
    }
}
