using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VitaDefiler.Modules
{
    class Scripting : IModule
    {
        public bool Run(Device dev, string cmd, string[] args)
        {
            switch (cmd)
            {
                case "set":
                    break;
                case "get":
                    break;
                case "if":
                    break;
                case "while":
                    break;
            }
            return false;
        }
    }
}
