using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jarvis.Framework.Shared.Support
{
	/// <summary>
	/// Marker interface that all objects that are saved in mongo database should implements
	/// to have it registered with BsonClassMap to avoid error of unknow identifier.
	/// With plain Stream Processing we have plain objects inside NStore database and this
	/// interface is useful to automapp all types with a simple convention.
	/// </summary>
	public interface IMongoPersistedPocoObject
	{
	}
}
