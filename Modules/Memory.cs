using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VitaDefiler.Modules
{
    class Memory : IModule
    {
        public bool Run(Device dev, string cmd, string[] args)
        {
            switch (cmd)
            {
                case "usbread":
                    if (args.Length >= 2)
                    {
                        USBRead(dev, args[0].ToVariable(dev).Data, args[1].ToInteger(), args.Length > 2 ? args[2] : null);
                        return true;
                    }
                    break;
                case "read":
                    if (args.Length >= 2)
                    {
                        Read(dev, args[0].ToVariable(dev).Data, args[1].ToInteger(), args.Length > 2 ? args[2] : null);
                        return true;
                    }
                    break;
                case "write":
                    if (args.Length >= 3)
                    {
                        Write(dev, args[0].ToVariable(dev).Data, args[1].ToInteger(), args[0].ToVariable(dev).IsCode, args[2].ToInteger(), args[2]);
                        return true;
                    }
                    break;
                case "allocate":
                case "alloc":
                    if (args.Length == 2)
                    {
                        Allocate(dev, args[1].ToInteger(), args[0] == "code" ? true : false);
                        return true;
                    }
                    break;
                case "free":
                    if (args.Length == 1)
                    {
                        Free(dev, args[0].ToVariable(dev).Data, args[0].ToVariable(dev).IsCode);
                        return true;
                    }
                    break;
            }
            return false;
        }

        public void Read(Device dev, uint addr, uint length, string file = null)
        {
        }

        public void USBRead(Device dev, uint addr, uint length, string file = null)
        {
        }

        public void Write(Device dev, uint addr, uint length, bool isCode, uint data = 0, string file = null)
        {
        }

        public void Allocate(Device dev, uint length, bool isCode)
        {
        }

        public void Free(Device dev, uint addr, bool isCode)
        {
        }
    }
}
