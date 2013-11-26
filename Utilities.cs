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
            string[] parts = self.Split(new char[] { '+', '-' }, 2);
            long offset = 0;
            if (parts.Length > 1)
            {
                offset = parts[1].ToInteger();
                if (self.Contains("-"))
                {
                    offset = -offset;
                }
                self = parts[0];
            }
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
                                v.Data = (uint)(v.Data + offset);
                                v.Size = (uint)(v.Size - offset);
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
            string[] parts = self.Split(new char[] { '+', '-' }, 2);
            if (parts.Length > 1)
            {
                switch (self[parts[0].Length])
                {
                    case '+': return parts[0].ToInteger() + parts[1].ToInteger();
                    case '-': return parts[0].ToInteger() - parts[1].ToInteger();
                    default: throw new FormatException("Invalid operator");
                }
            }
            if (self.ToLowerInvariant().StartsWith("0x"))
            {
                try
                {
                    data = Convert.ToUInt32(self, 16);
                }
                catch (FormatException)
                {
                }
            }
            else
            {
                UInt32.TryParse(self, out data);
            }
            return data;
        }

        public static uint ToDataSize(this string self)
        {
            switch (self)
            {
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
                    return self.ToInteger();
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
                {
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
