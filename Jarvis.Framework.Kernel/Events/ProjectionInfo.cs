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

		/// <summary>
		/// Name of the slot used, all projection in the same slot are "logically" a single
		/// projection and all events are dispatched sequentially.
		/// </summary>
        public String SlotName { get; set;  }

		/// <summary>
		/// This is a string that is used to understand if the projection is changed from
		/// the version that was present in database, if the versions differs, we will trigger
		/// a rebuild for the slot.
		/// </summary>
        public String Signature { get; set; }

		/// <summary>
		/// This is the name of the projection.
		/// </summary>
		public String CommonName { get; private set; }

		/// <summary>
		/// If this property is True this is a projection that should run only offline. It
		/// is used if we have a projection that need to be different when it is run offline.
		/// </summary>
		public Boolean OfflineProjection { get; set; }

        /// <summary>
        /// It is convenient to have the ability to disable a projection.
        /// </summary>
        public Boolean Disabled { get; set; }
    }
}
