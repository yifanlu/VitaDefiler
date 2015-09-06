using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VitaDefiler.Modules
{
    public interface IModule
    {
        bool Run(IDevice device, string cmd, string[] args);
    }
}
