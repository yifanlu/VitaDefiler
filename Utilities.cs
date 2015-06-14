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
                            v = new Variable();
                            v.Size = 0;
                            v.IsCode = false;
                            if (self.Length > 1 && self[1] == '#')
                            {
                                v.Data = vita.LastReturn;
                            }
                            else if (vita.Locals.ContainsKey(self.Substring(1)))
                            {
                                v.Data = vita.Locals[self.Substring(1)];
                            }
                            else
                            {
                                Console.Error.WriteLine("Invalid variable {0}", self.Substring(1));
                            }
                            v.Data = (uint)(v.Data + offset);
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

        private static bool BitSet(uint i, int b)
        {
            return (i & ((uint)0x1 << b)) != 0;
        }

        public enum DecodeResult
        {
            INSTRUCTION_UNKNOWN,
            INSTRUCTION_MOVW,
            INSTRUCTION_MOVT,
            INSTRUCTION_SYSCALL,
            INSTRUCTION_BRANCH
        }

        public static UInt32 DecodeARM32(UInt32 cur_inst, out DecodeResult type)
        {
            // if this doesn't change, error
            type = DecodeResult.INSTRUCTION_UNKNOWN;
            // Bits 31-28 should be 1110, Always Execute
            if (!(BitSet(cur_inst, 31) && BitSet(cur_inst, 30) && BitSet(cur_inst, 29) && !BitSet(cur_inst, 28)))
            {
                // Unsupported conditional instruction.
                return 0;
            }
            // Bits 27-25 should be 110 for supervisor calls
            if (BitSet(cur_inst, 27))
            {
                // Bit 24 should be set for SWI calls
                if (BitSet(cur_inst, 26) && BitSet(cur_inst, 25) && BitSet(cur_inst, 24))
                {
                    type = DecodeResult.INSTRUCTION_SYSCALL;
                    // TODO: Return syscall immediate value.
                    return 1;
                }
            }
            // Bits 27-25 should be 001 for data instructions
            else if (!BitSet(cur_inst, 26) && BitSet(cur_inst, 25))
            {
                // Bits 24-23 should be 10
                if (!(BitSet(cur_inst, 24) && !BitSet(cur_inst, 23)))
                {
                    // Not an valid ARM MOV instruction.
                    return 0;
                }
                // Bits 21-20 should be 00
                if (!(!BitSet(cur_inst, 21) && !BitSet(cur_inst, 20)))
                {
                    // Invalid ARM MOV instruction.
                    return 0;
                }
                // Bit 22 is 1 for top load 0 for bottom load
                if (BitSet(cur_inst, 22)) // top load
                {
                    type = DecodeResult.INSTRUCTION_MOVT;
                }
                else
                {
                    type = DecodeResult.INSTRUCTION_MOVW;
                }
                // Immediate value at 19-16 and 11-0
                // discard bytes 31-20
                // discard bytes 15-0
                return (((cur_inst << 12) >> 28) << 12) | ((cur_inst << 20) >> 20);
            }
            // Bits 27-25 should be 000 for jump instructions
            else if (!BitSet(cur_inst, 26) && !BitSet(cur_inst, 25))
            {
                // Bits 24-4 should be 100101111111111110001, 0x12FFF1 for BX
                if ((cur_inst << 7) >> 11 == 0x12FFF1)
                {
                    type = DecodeResult.INSTRUCTION_BRANCH;
                    return 0;
                }
                // Bits 24-4 should be 100101111111111110001, 0x12FFF3 for BLX
                else if ((cur_inst << 7) >> 11 == 0x12FFF3)
                {
                    type = DecodeResult.INSTRUCTION_BRANCH;
                    return 0;
                }
                else
                {
                    // unknown jump
                    return 0;
                }
            }
            else
            {
                // Unsupported instruction.
                return 0;
            }
            return 0;
        }

        public static UInt16 DecodeThumb2(UInt16 inst1, UInt16 inst2, out DecodeResult type)
        {
            type = DecodeResult.INSTRUCTION_UNKNOWN;
            if ((inst1 & 0xF240) == 0xF240 && (~inst1 & 0x930) == 0x930 && !BitSet(inst2, 15))
            {
                uint data = 0;
                if (BitSet(inst1, 7))
                {
                    type = DecodeResult.INSTRUCTION_MOVT;
                }
                else
                {
                    type = DecodeResult.INSTRUCTION_MOVW;
                }
                data |= ((uint)inst2 & 0xFF);
                data |= ((uint)inst2 & 0x7000) >> 12 << 8;
                data |= ((uint)inst1 & 0x400) >> 10 << 11;
                data |= ((uint)inst1 & 0xF) << 12;
                return (UInt16)data;
            }
            return 0;
        }
    }
}
