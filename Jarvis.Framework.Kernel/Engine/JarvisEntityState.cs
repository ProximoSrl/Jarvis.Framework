using Jarvis.Framework.Kernel.Engine;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.Options;
using NStore.Domain;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;

namespace Jarvis.Framework.Kernel.Engine
{
    public class JarvisEntityState : ICloneable, IInvariantsChecker
	{
		protected JarvisEntityState()
		{
			VersionSignature = "default";
		}

		/// <summary>
		/// Clona lo stato con una copia secca dei valori. Va reimplementata nel caso di utilizzo di strutture o referenze ad oggetti
		/// </summary>
		/// <returns>copia dello stato</returns>
		public object Clone()
		{
			return DeepCloneMe();
		}

		public InvariantsCheckResult CheckInvariants()
		{
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
