using Jarvis.Framework.Shared.MultitenantSupport;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Jarvis.Framework.Kernel.Events
{
    public class TenantProjections : IEnumerable<IProjection>
    {
        readonly TenantId _tenantId;
        readonly IProjection[] _allProjections;

        public TenantProjections(TenantId tenantId, IProjection[] allProjections)
        {
            _tenantId = tenantId;
            _allProjections = allProjections;
        }

        public IEnumerator<IProjection> GetEnumerator()
        {
            return _allProjections.Where(x => x.TenantId == _tenantId).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
