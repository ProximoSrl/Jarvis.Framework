using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jarvis.Framework.Shared.ReadModel
{
    /// <summary>
    /// Some readmodel are used from poller that will take new
    /// objects from readmodel, then index in Elasticsearch or 
    /// other stuff. It is useful for these types of readmodels
    /// to have come properties that are used to understand when
    /// the readmodel was modified and the Checkpoint token that
    /// lead to the modification.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract class PollableReadModel<T> : AbstractReadModel<T>, IPollableReadModel
    {
        public Int64 CheckpointToken { get; set; }

        public Int64 LastUpdatedTicks { get; set; }

        public Boolean Deleted { get; set; }
    }
}
