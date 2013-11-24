using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace VitaDefiler.Modules
{
    class Memory : IModule
    {
        public static readonly uint BLOCK_SIZE = 0x100;

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
            bool tofile = file != null && length == 0 && !UInt32.TryParse(file, out length);
            try
            {
                FileStream fout = tofile ? File.OpenWrite(file) : null;
                byte[] data = new byte[BLOCK_SIZE];
                for (uint l = 0; l < length; l += BLOCK_SIZE)
                {
                    uint size = l + BLOCK_SIZE > length ? length % BLOCK_SIZE : BLOCK_SIZE;
                    Console.Error.WriteLine("Dumping 0x{0:X}", addr + l);
                    if (dev.Network.RunCommand(Command.ReadData, BitConverter.GetBytes(size), out data) == Command.Error)
                    {
                        Console.WriteLine("Read failed.");
                        break;
                    }
                    if (tofile)
                    {
                        fout.Write(data, 0, (int)size);
                    }
                    else
                    {
                        data.PrintHexDump(size, 16);
                    }
                }
                fout.Close();
            }
            catch (IOException ex)
            {
                Console.Error.WriteLine("Error writing to file: {0}", ex.Message);
            }
        }

        public void USBRead(Device dev, uint addr, uint length, string file = null)
        {
        }

        public void Write(Device dev, uint addr, uint length, bool isCode, uint data = 0, string file = null)
        {
            bool fromfile = file != null && File.Exists(file);
            if (!fromfile)
            {
                length = sizeof(UInt32);
            }
            try
            {
                FileStream fin = fromfile ? File.OpenRead(file) : null;
                byte[] buf = fromfile ? new byte[BLOCK_SIZE] : BitConverter.GetBytes((int)data);
                for (uint l = 0; l < length; l += BLOCK_SIZE)
                {
                    uint size = l + BLOCK_SIZE > length ? length % BLOCK_SIZE : BLOCK_SIZE;
                    Console.Error.WriteLine("Writing 0x{0:X}", addr + l);
                    if (fromfile)
                    {
                        fin.Read(buf, 0, (int)size);
                    }
                    if (dev.Network.RunCommand(isCode ? Command.WriteCode : Command.WriteData, BitConverter.GetBytes(size), out buf) == Command.Error)
                    {
                        Console.WriteLine("Read failed.");
                        break;
                    }
                }
                if (fromfile)
                {
                    fin.Close();
                }
            }
            catch (IOException ex)
            {
                Console.Error.WriteLine("Error writing to file: {0}", ex.Message);
            }
        }

        public void Allocate(Device dev, uint length, bool isCode)
        {
            uint addr = (uint)dev.Network.RunCommand(isCode ? Command.AllocateCode : Command.AllocateData, (int)length);
            if (addr > 0)
            {
                Console.Error.WriteLine("Allocated at 0x{0:X}", addr);
                dev.CreateVariable(addr, length, isCode);
            }
            else
            {
                Console.Error.WriteLine("Allocate failed.");
            }
        }

        public void Free(Device dev, uint addr, bool isCode)
        {
            dev.Network.RunCommand(isCode ? Command.FreeCode : Command.FreeData, (int)addr);
        }
    }
}
