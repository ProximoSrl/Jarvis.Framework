using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jarvis.Framework.Kernel.ProjectionEngine.Client
{
    /// <summary>
    /// This class gives information on all the slots in the system, it is used to understand
    /// if some projection needs rebuild, if some projection is new or if there are some errors.
    /// </summary>
    public class CheckpointSlotStatus
    {
        /// <summary>
        /// This list contains all the slot that needs rebuild because one of the projection has
        /// a different signature than the one actually on database.
        /// </summary>
        public List<String> SlotsThatNeedsRebuild { get; private set; }

        /// <summary>
        /// This contains all the slots that are new, this is needed to allow the user to decide if this
        /// slot should start at 0 or it should start at different value. 
        /// 
        /// Usually new projection that does not have logic different from rebuild can be safely rebuild
        /// to use nitro.
        /// </summary>
        public List<String> NewSlots { get; private set; }

        /// <summary>
        /// This contains list of all slots for all projection that are currently loaded into the system
        /// </summary>
        public HashSet<String> AllSlots { get; private set; }

        public CheckpointSlotStatus()
        {
            SlotsThatNeedsRebuild = new List<string>();
            NewSlots = new List<string>();
            AllSlots = new HashSet<string>();
        }
    }
}
