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
                        USBRead(dev, args[0].ToVariable(dev).Data, args[1].ToDataSize(), args.Length > 2 ? args[2] : null);
                        return true;
                    }
                    break;
                case "read":
                    if (args.Length >= 2)
                    {
                        Read(dev, args[0].ToVariable(dev).Data, args[1].ToDataSize(), args.Length > 2 ? args[2] : null);
                        return true;
                    }
                    break;
                case "write":
                    if (args.Length >= 3)
                    {
                        Write(dev, args[0].ToVariable(dev).Data, args[1].ToDataSize(), args[0].ToVariable(dev).IsCode, args[2].ToInteger(), args[2]);
                        return true;
                    }
                    break;
                case "allocate":
                case "alloc":
                    if (args.Length == 2)
                    {
                        Allocate(dev, args[1].ToDataSize(), args[0] == "code" ? true : false);
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
                    if (dev.Network.RunCommand(Command.ReadData, new int[]{(int)(addr+l), (int)size}, out data) == Command.Error)
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
                if (tofile)
                {
                    fout.Close();
                }
            }
            catch (IOException ex)
            {
                Console.Error.WriteLine("Error writing to file: {0}", ex.Message);
            }
        }

        public void USBRead(Device dev, uint addr, uint length, string file = null)
        {
            FileStream fs = file == null ? null : File.OpenWrite(file);
            dev.USB.StartDump(addr, length, fs);
            if (fs != null)
            {
                fs.Close();
            }
        }

        public void Write(Device dev, uint addr, uint length, bool isCode, uint data = 0, string file = null)
        {
            byte[] resp;
            if (file == null || !File.Exists(file))
            {
                if (dev.Network.RunCommand(isCode ? Command.WriteCode : Command.WriteData, new int[] { (int)addr, (int)length, (int)data }, out resp) == Command.Error)
                {
                    Console.Error.WriteLine("Write failed.");
                }
                else
                {
                    Console.Error.WriteLine("Wrote 0x{0:X} byte.", BitConverter.ToInt32(resp, 0));
                }
            }
            else
            {
                try
                {
                    FileStream fin = File.OpenRead(file);
                    byte[] buf = new byte[BLOCK_SIZE + sizeof(int)];
                    for (uint l = 0; l < length; l += BLOCK_SIZE)
                    {
                        uint size = l + BLOCK_SIZE > length ? length % BLOCK_SIZE : BLOCK_SIZE;
                        Console.Error.WriteLine("Writing 0x{0:X}", addr + l);
                        Array.Copy(BitConverter.GetBytes(addr), 0, buf, 0, sizeof(int));
                        fin.Read(buf, sizeof(int), (int)size);
                        if (dev.Network.RunCommand(isCode ? Command.WriteCode : Command.WriteData, buf, out resp) == Command.Error)
                        {
                            Console.Error.WriteLine("Write failed.");
                            break;
                        }
                        else
                        {
                            Console.Error.WriteLine("Wrote 0x{0:X} byte.", BitConverter.ToInt32(resp, 0));
                        }
                    }
                    fin.Close();
                }
                catch (IOException ex)
                {
                    Console.Error.WriteLine("Error writing to file: {0}", ex.Message);
                }
            }
        }

        public void Allocate(Device dev, uint length, bool isCode)
        {
            uint addr = (uint)dev.Network.RunCommand(isCode ? Command.AllocateCode : Command.AllocateData, (int)length);
            if (addr > 0)
            {
                int idx = dev.CreateVariable(addr, length, isCode);
                Console.Error.WriteLine("Allocated variable ${0} at 0x{1:X}", idx, addr);
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
