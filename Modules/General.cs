using System;
using System.Collections.Generic;
using System.Text;

namespace VitaDefiler.Modules
{
    class General : IModule
    {
        private static readonly string HelpText =
@"
Commands:
alloc type length               Allocates space for a variable. Type is 
                                'data' or 'code'.
compile addr file.c file        Compiles file.c and produce file.
echo [text]                     Prints text to Vita/console screen
exec addr [arg0] ... [arg3]     Executes code located at address with args.
free addr                       Frees memory allocated at addr.
usbread addr length [file]      Uses USB to dump address. Optional file to 
                                capture output to. Optional length to read.
read addr [length] [file]       Uses network to dump address. Optional file 
                                to capture output to. Optional length to read.
write addr length (file|int)    Writes binary data or an integer to addr.
vars                            Print list of variables

Paramaters:
addr    Can be either an integer address (ex: 0x81000000) or a variable of form
        $x (for code/data variables) or %x (for local variables). Can also optionally 
        include an offset in the form of $x+num or $x-num (ex: $2+0x100, $0-256, 
        0x81000000+0x100)
length  Can be a hex number (ex: 0x1000), a decimal number (ex: 256), or a data type 
        including int, uint, char, short, float, etc. int/uint can also be 
        qualified with size, for example: int32 or uint16.
file    Filename relative to current working directory or absolute path.
";

        public bool Run(Device dev, string cmd, string[] args)
        {
            switch (cmd)
            {
                case "help":
                    Help();
                    return true;
                case "echo":
                case "print":
                    Echo(dev, string.Join(" ", args));
                    return true;
                case "vars":
                    PrintVars(dev);
                    return true;
            }
            return false;
        }

        public void Help()
        {
            Console.Error.Write(HelpText);
        }

        public void Echo(Device dev, string text)
        {
            byte[] resp;
            dev.Network.RunCommand(Command.Echo, Encoding.ASCII.GetBytes(text), out resp);
            Console.WriteLine(Encoding.ASCII.GetString(resp));
        }

        public void PrintVars(Device dev)
        {
            var dataVars = dev.Vars;
            for (int i = 0; i < dataVars.Count; i++)
            {
                if (dataVars[i].Data == 0)
                {
                    continue;
                }
                Console.WriteLine("${0}: 0x{1:X}, size: 0x{2:X}, code: {3}", i, dataVars[i].Data, dataVars[i].Size, dataVars[i].IsCode);
            }
        }
    }
}
