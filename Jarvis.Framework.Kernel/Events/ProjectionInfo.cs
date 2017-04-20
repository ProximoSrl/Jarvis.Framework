using Jarvis.Framework.Kernel.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jarvis.Framework.Kernel.Events
{
    /// <summary>
    /// This class contains all information we need about a projection, this is needed
    /// to avoid the need to really instantiate a projection to know its info. Whenever
    /// a component of the framework need info about projection we can use this 
    /// class instead.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class ProjectionInfoAttribute : Attribute
    {
        public ProjectionInfoAttribute(String commonName)
        {
            SlotName = "default";
            Signature = "signature";
            CommonName = commonName;
        }

        public ProjectionInfoAttribute(string slotName, string signature, string commonName)
        {
            SlotName = slotName;
            Signature = signature;
            CommonName = commonName;
        }

        public String SlotName { get; set;  }

        public String Signature { get; set; }

        public String CommonName { get; private set; }
    }
}
