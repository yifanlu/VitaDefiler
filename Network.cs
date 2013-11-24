using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VitaDefiler
{
    enum Command
    {
        Error = 0,
        AllocateData = 1,
        AllocateCode = 2,
        FreeData = 3,
        FreeCode = 4,
        WriteData = 5,
        WriteCode = 6,
        ReadData = 7,
        Execute = 8,
        Echo = 9
    }

    class Network
    {
        public int RunCommand(Command cmd)
        {
            byte[] ret;
            RunCommand(cmd, out ret);
            return BitConverter.ToInt32(ret, 0);
        }

        public int RunCommand(Command cmd, int data)
        {
            return RunCommand(cmd, BitConverter.GetBytes(data));
        }

        public int RunCommand(Command cmd, byte[] data)
        {
            return 0;
        }

        public int RunCommand(Command cmd, byte[] data, out byte[] response)
        {
            response = null;
            return 0;
        }

        public void RunCommand(Command cmd, out byte[] data)
        {
            data = null;
        }
    }
}
