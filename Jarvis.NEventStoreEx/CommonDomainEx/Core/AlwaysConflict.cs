using NEventStore.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jarvis.NEventStoreEx.CommonDomainEx.Core
{
    public class AlwaysConflict : IDetectConflicts
    {
        public bool ConflictsWith(IEnumerable<object> uncommittedEvents, IEnumerable<object> committedEvents)
        {
            return true;
        }

        public void Register<TUncommitted, TCommitted>(ConflictDelegate<TUncommitted, TCommitted> handler)
            where TUncommitted : class
            where TCommitted : class
        {
            throw new NotImplementedException();
        }
    }
}
