using System;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;
using NStore.Domain;
using System.Collections.Generic;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.Options;
using System.Linq;

namespace Jarvis.Framework.Kernel.Engine
{
	/// <summary>
	/// Stato interno dell'aggregato
	/// Deve implementare ICloneable se usa strutture o referenze ad oggetti
	/// </summary>
	[Serializable]
    public abstract class JarvisAggregateState : ICloneable, IInvariantsChecker
    {
        protected JarvisAggregateState()
        {
            VersionSignature = "default";
			EntityStates = new Dictionary<string, JarvisEntityState>();
        }

		/// <summary>
		/// To transparently handle snapshots of child entities, we need to 
		/// </summary>
		[BsonDictionaryOptions(Representation = DictionaryRepresentation.ArrayOfDocuments)]
		public Dictionary<String, JarvisEntityState> EntityStates { get; set; }

		/// <summary>
		/// Clona lo stato con una copia secca dei valori. Va reimplementata nel caso di utilizzo di strutture o referenze ad oggetti
		/// </summary>
		/// <returns>copia dello stato</returns>
		public object Clone()
        {
            var cloned = (JarvisAggregateState) DeepCloneMe();
			if (cloned.EntityStates == null || Object.ReferenceEquals(cloned.EntityStates, EntityStates))
			{
				cloned.EntityStates = this.EntityStates
					.ToDictionary(_ => _.Key, _ => (JarvisEntityState) _.Value.Clone());
			}
			return cloned;
        }

        public InvariantsCheckResult CheckInvariants()
		{
			foreach (var entityState in EntityStates)
			{
				var invariantCheck = entityState.Value.CheckInvariants();
				if (invariantCheck.IsInvalid)
					return invariantCheck;
			}
			return OnCheckInvariants();
		}

		protected virtual InvariantsCheckResult OnCheckInvariants()
		{
			return InvariantsCheckResult.Ok;
		}

		/// <summary>
		/// A string property that allows for change in state. If an object needs to
		/// change state, all the snapshot should be deleted because they are obsolete. With
		/// this property the object can declare when state change format and all 
		/// snapshot should be invalidated.
		/// </summary>
		public String VersionSignature { get; protected set; }

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