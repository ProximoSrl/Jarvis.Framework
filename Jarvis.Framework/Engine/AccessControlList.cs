using Jarvis.Framework.Shared.Claims;
using System.Collections.Generic;
using System.Linq;

namespace Jarvis.Framework.Kernel.Engine
{
    public enum GrantType
    {
        Allowed,
        Denied,
        Inherited
    };

    public class AccessControlListGrants
    {
        readonly string[] _claimsOrder;

        class GrantSlot
        {
            public Claim Claim { get; private set; }
            public GrantType Value { get; private set; }

            public GrantSlot(Claim claim, GrantType grantValue)
            {
                Claim = claim;
                Value = grantValue;
            }
        }

        private readonly List<GrantSlot> _grants = new List<GrantSlot>();

        public AccessControlListGrants(string[] claimsOrder)
        {
            _claimsOrder = claimsOrder;
        }

        public void Set(Claim claim, GrantType type)
        {
            UnSet(claim);
            _grants.Add(new GrantSlot(claim, type));
        }

        public void UnSet(Claim claim)
        {
            var slot = _grants.FirstOrDefault(x => x.Claim == claim);
            if (slot != null)
            {
                _grants.Remove(slot);
            }
        }

        public GrantType GetGrantAccess(Claim[] claims)
        {
            foreach (var claimId in _claimsOrder)
            {
                var slots = _grants.Where(x => x.Value != GrantType.Inherited && x.Claim.Id == claimId).ToArray();
                if (slots.Any())
                {
                    bool allowed = false;
                    var weakGrant = GrantType.Inherited;

                    foreach (var slot in slots)
                    {
                        // strict
                        if (claims.Any(x => x.Id == slot.Claim.Id && slot.Claim.Value == x.Value))
                        {
                            // la negazione vince sempre
                            if (slot.Value == GrantType.Denied)
                                return GrantType.Denied;

                            allowed = true;
                        }
                        else if (!allowed && claims.Any(x => x.Id == slot.Claim.Id && slot.Claim.Value == "*"))
                        {
                            weakGrant = slot.Value;
                        }
                    }

                    if (allowed)
                        return GrantType.Allowed;

                    if (weakGrant != GrantType.Inherited)
                        return weakGrant;
                }
            }

            return GrantType.Inherited;
        }

        public int Count()
        {
            return _grants.Count;
        }

    }

    public class AccessControlList
    {
        readonly string[] _claimsOrder;
        readonly IDictionary<string, AccessControlListGrants> _list = new Dictionary<string, AccessControlListGrants>();

        public AccessControlList(IEnumerable<string> claimsOrder)
        {
            // memorizzati in ordine inverso
            _claimsOrder = claimsOrder.Reverse().ToArray();
        }

        public void Set(string name, Claim claim, GrantType defaultType)
        {
            AccessControlListGrants grants;

            if (!_list.TryGetValue(name, out grants))
            {
                grants = new AccessControlListGrants(_claimsOrder);
                _list.Add(name, grants);
            }
            grants.Set(claim, defaultType);
        }

        public GrantType GetGrantAccess(string name, Claim[] claims)
        {
            AccessControlListGrants grants;
            if (!_list.TryGetValue(name, out grants))
                return GrantType.Inherited;

            return grants.GetGrantAccess(claims);
        }

        public int Count()
        {
            return _list.Values.Sum(x => x.Count());
        }

        public void UnSet(string name, Claim claim)
        {
            AccessControlListGrants grants;
            if (_list.TryGetValue(name, out grants))
            {
                grants.UnSet(claim);
            }
        }
    }
}
