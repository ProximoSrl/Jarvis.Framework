using System;

namespace Jarvis.MonitoringAgentServer.Server.Dto
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
