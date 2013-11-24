using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VitaDefiler.Modules
{
    interface IModule
    {
        public bool Run(Device dev, string cmd, string[] args);
    }
}
