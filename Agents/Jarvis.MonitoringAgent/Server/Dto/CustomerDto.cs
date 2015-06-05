using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jarvis.MonitoringAgent.Server.Dto
{
    public class CustomerDto
    {
        public String Name { get; private set; }

        public Boolean IsEnabled { get; private set; }

        public CustomerDto(string name, Boolean isEnabled)
        {
            Name = name;
            IsEnabled = isEnabled;
        }
    }
}
