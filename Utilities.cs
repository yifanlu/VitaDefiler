using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VitaDefiler
{
    static class Utilities
    {
        public static Variable ToVariable(this string self, Device vita)
        {
            Variable v = Variable.Null;
            if (!string.IsNullOrEmpty(self))
            {
                switch (self[0])
                {
                    case '$':
                        {
                            int idx;
                            if (Int32.TryParse(self.Substring(1), out idx) && vita.Vars.Count > idx)
                            {
                                v = vita.Vars[idx];
                            }
                            break;
                        }
                    case '%':
                        {
                            break;
                        }
                    default:
                        {
                            v = new Variable();
                            v.Data = self.ToInteger();
                            v.Size = 0;
                            break;
                        }

                }
            }
#if DEBUG
            Console.Error.WriteLine("Parsed '{0}' to '0x{1:X}' with size {2}", self, v.Data, v.Size);
#endif
            return v;
        }

        public static uint ToInteger(this string self)
        {
            uint data = 0;
            if (self.ToLowerInvariant().StartsWith("0x"))
            {
                try
                {
                    data = Convert.ToUInt32(self, 16);
                }
                catch (FormatException ex)
                {
                }
            }
            else
            {
                UInt32.TryParse(self, out data);
            }
            return data;
        }

        public static int ToDataSize(this string self)
        {
            switch (self)
            {
                case "int64":
                case "uint64":
                case "long":
                case "ulong":
                case "double":
#if DEBUG
                    Console.Error.WriteLine("Parsed '{0}' to {1}", self, 8);
#endif
                    return 8;
                case "int32":
                case "uint32":
                case "int":
                case "uint":
                case "float":
#if DEBUG
                    Console.Error.WriteLine("Parsed '{0}' to {1}", self, 4);
#endif
                    return 4;
                case "int16":
                case "uint16":
                case "short":
                case "ushort":
#if DEBUG
                    Console.Error.WriteLine("Parsed '{0}' to {1}", self, 2);
#endif
                    return 2;
                case "int8":
                case "uint8":
                case "char":
                case "byte":
#if DEBUG
                    Console.Error.WriteLine("Parsed '{0}' to {1}", self, 1);
#endif
                    return 1;
                default:
#if DEBUG
                    Console.Error.WriteLine("Parsed '{0}' to {1}", self, 0);
#endif
                    return 0;
            }
        }

        public static void PrintHexDump(this byte[] data, uint size, uint num)
        {
            uint i = 0, j = 0, k = 0, l = 0;
            for (l = size / num, k = 1; l > 0; l /= num, k++)
                ; // find number of zeros to prepend line number
            while (j < size)
            {
                // line number
                Console.Write("{0:X" + k + "}: ", j);
                // hex value
                for (i = 0; i < num; i++, j++)
                {
                    if (j < size)
                    {
                        Console.Write("{0:X2} ", data[j]);
                    }
                    else
                    { // print blank spaces
                        Console.Write("   ");
                    }
                }
                // seperator
                Console.Write("| ");
                // ascii value
                for (i = num; i > 0; i--)
                {C:\Users\Yifan\Dropbox\development\CS\VitaDefiler\VitaDefiler\Utilities.cs
                    if (j - i < size)
                    {
                        Console.Write("{0}", data[j - i] < 32 || data[j - i] > 126 ? "." : Char.ToString((char)data[j - i])); // print only visible characters
                    }
                    else
                    {
                        Console.Write(" ");
                    }
                }
                // new line
                Console.WriteLine();
            }
        }
    }
}
