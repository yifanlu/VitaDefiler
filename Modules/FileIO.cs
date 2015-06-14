using System;
using System.IO;
using System.Text;

namespace VitaDefiler.Modules
{
    class FileIO : IModule
    {
        public bool Run(Device dev, string cmd, string[] args)
        {
            switch (cmd)
            {
                case "pull":
                    {
                        if (args.Length >= 1)
                        {
                            string srcpath = args[0];
                            string dstpath = args.Length >= 2 ? args[1] : Path.GetFileName(srcpath);
                            Pull(dev, srcpath, dstpath);
                            return true;
                        }
                    }
                    break;
                case "push":
                    {
                        if (args.Length >= 1)
                        {
                            string srcpath = args[0];
                            string dstpath = args.Length >= 2 ? args[1] : Path.GetFileName(srcpath);
                            Push(dev, srcpath, dstpath);
                            return true;
                        }
                    }
                    break;
            }
            return false;
        }

        public bool Pull(Device dev, string srcpath, string dstpath)
        {
            try
            {
                byte[] req = new byte[sizeof(int) + srcpath.Length];
                byte[] data;
                Array.Copy(BitConverter.GetBytes(srcpath.Length), 0, req, 0, sizeof(int));
                Encoding.ASCII.GetBytes(srcpath, 0, srcpath.Length, req, sizeof(int));
                if (dev.Network.RunCommand(Command.PullFile, req, out data) == Command.Error)
                {
                    Console.Error.WriteLine("Error pulling file.");
                    return false;
                }
                using (FileStream fs = File.OpenWrite(dstpath))
                {
                    fs.Write(data, 0, data.Length);
                }
                return true;
            }
            catch (IOException ex)
            {
                Console.Error.WriteLine(ex.ToString());
                return false;
            }
        }

        public bool Push(Device dev, string srcpath, string dstpath)
        {
            try
            {
                byte[] data = File.ReadAllBytes(srcpath);
                byte[] req = new byte[sizeof(int) + dstpath.Length + data.Length];
                byte[] resp;
                Array.Copy(BitConverter.GetBytes(dstpath.Length), 0, req, 0, sizeof(int));
                Encoding.ASCII.GetBytes(dstpath, 0, dstpath.Length, req, sizeof(int));
                Array.Copy(data, 0, req, sizeof(int) + dstpath.Length, data.Length);
                if (dev.Network.RunCommand(Command.PushFile, req, out resp) == Command.Error)
                {
                    Console.Error.WriteLine("Error pushing file.");
                    return false;
                }
                return true;
            }
            catch (IOException ex)
            {
                Console.Error.WriteLine(ex.ToString());
                return false;
            }
        }
    }
}
