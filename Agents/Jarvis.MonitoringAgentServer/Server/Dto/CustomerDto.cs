using System;

namespace Jarvis.MonitoringAgentServer.Server.Dto
{
    public class CustomerDto
    {
        public String Name { get; private set; }

        public Boolean IsEnabled { get; private set; }

        public String PublicKey { get; private set; }

        public CustomerDto(string name, Boolean isEnabled)
            :this (name, isEnabled, "")
        {

        }

        public CustomerDto(string name, Boolean isEnabled, String publicKey)
        {
            Name = name;
            IsEnabled = isEnabled;
            PublicKey = publicKey;
        }
    }
}
