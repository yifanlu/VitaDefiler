using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace VitaDefiler.Modules
{
    class Code : IModule
    {
    	private static readonly string TEMP_OBJECT = "code.o";

        public bool Run(Device dev, string cmd, string[] args)
        {
            switch (cmd)
            {
                case "compile":
                    if (args.Length == 2)
                    {
                        Compile(dev, args[0], args[1]);
                        return true;
                    }
                    break;
                case "execute":
                case "exec":
                    if (args.Length >= 1)
                    {
                        uint[] callargs = new uint[args.Length];
                        for (int i = 0; i < args.Length; i++)
                        {
                            Variable v = args[i].ToVariable(dev);
                            callargs[i] = v.Data;
                        }
                        Execute(dev, callargs);
                        return true;
                    }
                    break;
            }
            return false;
        }

        public void Compile(Device dev, string file, string output)
        {
            if (!File.Exists(file))
            {
                Console.Error.WriteLine("Cannot find {0}", file);
                return;
            }
            ProcessStartInfo info = new ProcessStartInfo(){
                FileName = "arm-none-eabi-gcc.exe",
                Arguments = string.Format("-fPIE -fno-zero-initialized-in-bss -std=c99 -mcpu=cortex-a9 -D DEBUG -mthumb-interwork -mthumb -c -o {0} {1}", TEMP_OBJECT, file),
                RedirectStandardOutput = true,
                UseShellExecute = false
            };
            Process gcc = Process.Start(info);
            Console.Error.WriteLine(gcc.StandardOutput.ReadToEnd());
            gcc.WaitForExit();
            if (!File.Exists(TEMP_OBJECT))
            {
                Console.Error.WriteLine("GCC did not produce a valid output");
                return;
            }
            info = new ProcessStartInfo(){
                FileName = "arm-none-eabi-objcopy.exe",
                Arguments = string.Format("-O binary {0} {1}", TEMP_OBJECT, output),
                RedirectStandardOutput = true,
                UseShellExecute = false
            };
            Process objcopy = Process.Start(info);
            Console.Error.WriteLine(objcopy.StandardOutput.ReadToEnd());
            objcopy.WaitForExit();
            File.Delete(TEMP_OBJECT);
            if (!File.Exists(output))
            {
                Console.Error.WriteLine("No valid binary was produced.");
            }
        }

        public void Execute(Device dev, uint[] args)
        {
            byte[] resp;
            uint ret;
            if (dev.Network.RunCommand(Command.Execute, args, out resp) != Command.Error)
            {
                ret = BitConverter.ToUInt32(resp, 0);
                Console.Error.WriteLine("Return value: 0x{0:X}", ret);
                dev.LastReturn = ret;
            }
        }
    }
}
