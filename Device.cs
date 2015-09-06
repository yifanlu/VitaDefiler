using System;
using System.Collections.Generic;

namespace VitaDefiler
{
    public struct Variable
    {
        public static readonly Variable Null = new Variable();
        public uint Data;
        public uint Size;
        public bool IsCode;
    }

    public interface IDevice
    {
    }

    class Device : IDevice
    {
        public List<Variable> Vars { get; private set; }
        public Dictionary<string, uint> Locals { get; private set; }
        public Network Network { get; private set; }
        public Exploit Exploit { get; private set; }
        public uint LastReturn { get; set; }
        public string Script { get; set; }

        public Device(Exploit exploit, Network net)
        {
            Vars = new List<Variable>();
            Locals = new Dictionary<string, uint>();
            Network = net;
            Exploit = exploit;
        }

        public int CreateVariable(uint addr, uint size, bool isCode)
        {
            Variable var = new Variable()
            {
                Data = addr,
                Size = size,
                IsCode = isCode
            };
            int i;
            for (i = 0; i < Vars.Count; i++)
            {
                if (Vars[i].Data == 0)
                {
                    Vars[i] = var;
                    return i;
                }
            }
            Vars.Add(var);
            Defiler.LogLine("${0} = 0x{1:X}", i, addr);
            return i;
        }

        public int DeleteVariable(Variable var)
        {
            for (int i = 0; i < Vars.Count; i++)
            {
                if (Vars[i].Data == var.Data)
                {
                    Vars[i] = Variable.Null;
#if DEBUG
                    Defiler.LogLine("Deleted variable ${0}", i);
#endif
                }
            }
            return 0;
        }

        public int DeleteVariable(int id)
        {
            if (id < Vars.Count)
            {
                Vars[id] = Variable.Null;
            }
            return 0;
        }

        public void CreateLocal(string name, uint data)
        {
            Locals[name] = data;
            Defiler.LogLine("%{0} = 0x{1:X}", name, data);
        }
    }
}
