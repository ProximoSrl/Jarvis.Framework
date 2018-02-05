using NStore.Core.Snapshots;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jarvis.Framework.Kernel.Engine
{
	public interface IEntityRoot : ISnapshottable
	{
		/// <summary>
		/// Id of entity
		/// </summary>
		String Id { get; }

		/// <summary>
		/// Function to init the entity.
		/// </summary>
		/// <param name="raiseEventFunction"></param>
		/// <param name="ownerId"></param>
		void Init(Action<object> raiseEventFunction, String ownerId);

		/// <summary>
		/// We need to access the state of the entity root because
		/// we need to apply events to it.
		/// </summary>
		/// <returns></returns>
		JarvisEntityState GetState();
	}
}
