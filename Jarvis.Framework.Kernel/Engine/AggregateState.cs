using System;
using Fasterflect;
using Jarvis.Framework.Shared.Events;
using Jarvis.Framework.Shared.IdentitySupport;
using Jarvis.NEventStoreEx.CommonDomainEx;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;

namespace Jarvis.Framework.Kernel.Engine
{
    /// <summary>
    /// Stato interno dell'aggregato
    /// Deve implementare ICloneable se usa strutture o referenze ad oggetti
    /// </summary>
    [Serializable]
    public abstract class AggregateState : ICloneable, IInvariantsChecker
    {
        public AggregateState()
        {
            Signature = "default";
        }

        /// <summary>
        /// Clona lo stato con una copia secca dei valori. Va reimplementata nel caso di utilizzo di strutture o referenze ad oggetti
        /// </summary>
        /// <returns>copia dello stato</returns>
        public object Clone()
        {
            return DeepCloneMe();
        }

        public void Apply(IDomainEvent evt)
        {
            var method = GetType().Method("When", new[] { evt.GetType() }, Flags.InstanceAnyVisibility);
            if (method != null)
                method.Invoke(this, new object[] { evt });
        }

        public virtual InvariantCheckResult CheckInvariants()
        {
            return InvariantCheckResult.Success;
        }

        public String Signature { get; protected set; }

		/// <summary>
		/// Create a deep clone with Serialization. It can be overriden in derived
		/// class if you want a more performant way to clone the object. Using 
		/// serialization gives us the advantage of automatic creation of deep cloned
		/// object.
		/// </summary>
		/// <returns></returns>
		protected virtual Object DeepCloneMe()
        {
            IFormatter formatter = new BinaryFormatter();
            using (Stream stream = new MemoryStream())
            {
                formatter.Serialize(stream, this);
                stream.Seek(0, SeekOrigin.Begin);
                return formatter.Deserialize(stream);
            }
        }
    }
}