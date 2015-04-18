using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using Fasterflect;
using Jarvis.Framework.Shared.Domain;
using Jarvis.Framework.Shared.Events;
using Jarvis.NEventStoreEx.CommonDomainEx;

namespace Jarvis.Framework.Kernel.Engine
{
    /// <summary>
    /// Token
    /// </summary>
    public class Token : StringValue
    {
        public Token(string value)
            : base(value)
        {
        }
    }

    /// <summary>
    /// GrantName
    /// </summary>
    public class GrantName : StringValue
    {
        public GrantName(string value)
            : base(value)
        {
        }
    }

    /// <summary>
    /// 
    /// </summary>
    public class Grant
    {
        public Token Token { get; private set; }
        public GrantName GrantName { get; private set; }

        public Grant(Token token, GrantName grantName)
        {
            Token = token;
            GrantName = grantName;
        }

        protected bool Equals(Grant other)
        {
            return Equals(Token, other.Token) && Equals(GrantName, other.GrantName);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((Grant)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Token != null ? Token.GetHashCode() : 0) * 397) ^ (GrantName != null ? GrantName.GetHashCode() : 0);
            }
        }
    }

    /// <summary>
    /// Stato interno dell'aggregato
    /// Deve implementare ICloneable se usa strutture o referenze ad oggetti
    /// </summary>
    public abstract class AggregateState : ICloneable, IInvariantsChecker
    {
        private readonly HashSet<Grant> _grants = new HashSet<Grant>();

        /// <summary>
        /// Clona lo stato con una copia secca dei valori. Va reimplementata nel caso di utilizzo di strutture o referenze ad oggetti
        /// </summary>
        /// <returns>copia dello stato</returns>
        public virtual object Clone()
        {
            return MemberwiseClone();
        }

        public void Apply(IDomainEvent evt)
        {
            var method = GetType().Method("When", new[] { evt.GetType() }, Flags.InstanceAnyVisibility);
            if (method != null)
            {
                method.Invoke(this, new object[] { evt });
            }
        }

        private void When(GrantAdded e)
        {
            _grants.Add(e.Grant);
        }     
        
        private void When(GrantRevoked e)
        {
            _grants.Remove(e.Grant);
        }

        public virtual InvariantCheckResult CheckInvariants()
        {
            return InvariantCheckResult.Success;
        }

        public bool ValidateGrant(Grant grant)
        {
            return _grants.Contains(grant);
        }

        public bool HasGrant(GrantName name)
        {
            return _grants.Any(x => x.GrantName == name);
        }
    }
}