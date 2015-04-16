using System;
using Fasterflect;
using Jarvis.Framework.Shared.Events;
using Jarvis.Framework.Shared.IdentitySupport;
using Jarvis.NEventStoreEx.CommonDomainEx;

namespace Jarvis.Framework.Kernel.Engine
{
    /// <summary>
    /// Stato interno dell'aggregato
    /// Deve implementare ICloneable se usa strutture o referenze ad oggetti
    /// </summary>
	public abstract class AggregateState : ICloneable, IInvariantsChecker
    {
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
            var method = GetType().Method("When", new [] {evt.GetType()}, Flags.InstanceAnyVisibility);
            if(method != null)
                method.Invoke(this, new object[]{evt});
        }

        public virtual InvariantCheckResult CheckInvariants()
        {
            return InvariantCheckResult.Success;
        }
    }
}