using System;
using System.Collections.Generic;

namespace VitaDefiler
{
    struct Variable
    {
        public static readonly Variable Null = new Variable();
        public uint Data;
        public uint Size;
        public bool IsCode;
    }

    class Device
    {
        public List<Variable> Data { get; private set; }
        public List<Variable> Code { get; private set; }
        public Network Network { get; private set; }
        public USB USB { get; private set; }

        public Device()
        {
            Data = new List<Variable>();
            Code = new List<Variable>();
            Network = new Network();
            USB = new USB();
        }

        public int CreateVariable(uint addr, uint size)
        {
            return 0;
        }

        public int FreeVariable(uint addr)
        {
            return 0;
        }

        public void ConnectUSB()
        {
        }

        public void ConnectNetwork()
        {
        }
    }
}
