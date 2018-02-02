using Jarvis.Framework.Kernel.Engine;
using Jarvis.Framework.Shared.Events;
using Jarvis.Framework.Shared.Exceptions;
using NStore.Core.Snapshots;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jarvis.Framework.Kernel.Engine
{
    /// <summary>
    /// Root of an entity that is used to create mixin
    /// </summary>
    /// <typeparam name="TState"></typeparam>
    public class JarvisEntityRoot<TState> :
		IEntityRoot
		where TState : JarvisEntityState, new()
    {
        private TState _state;

		protected TState InternalState => _state;

		public String Id { get; private set; }

		public JarvisEntityRoot(String id)
		{
			if (id == null)
				throw new ArgumentNullException(nameof(id));

			Id = id;
			_state = new TState();
		}

		/// <summary>
		/// Needed to raise event on aggregate
		/// </summary>
		private Action<object> _raiseEventFunction;

		private String _ownerId;

		public JarvisEntityState GetState()
		{
			return InternalState;
		}

		/// <summary>
		/// Init the entity root, giving access to specific part of owner
		/// aggregate
		/// </summary>
		/// <param name="raiseEventFunction"></param>
		/// <param name="ownerId"></param>
		/// <returns></returns>
		void IEntityRoot.Init(Action<object> raiseEventFunction, String ownerId)
		{
			_raiseEventFunction = raiseEventFunction;
			_ownerId = ownerId;
		}

        public SnapshotInfo GetSnapshot()
        {
            return new SnapshotInfo(
                Id,
                0,
                this._state,
                this._state.VersionSignature
            );
        }

        public bool TryRestore(SnapshotInfo snapshotInfo)
        {
			if (snapshotInfo == null) throw new ArgumentNullException(nameof(snapshotInfo));

			var processed = PreprocessSnapshot(snapshotInfo);

			if (processed == null || processed.IsEmpty)
				return false;

			var state = (TState)processed.Payload;
			if (snapshotInfo.SchemaVersion != _state.VersionSignature)
			{
				return false;
			}

			_state = state;
			return true;
		}

		protected virtual SnapshotInfo PreprocessSnapshot(SnapshotInfo snapshotInfo)
		{
			return snapshotInfo;
		}

		protected void RaiseEvent(object @event)
		{
			_raiseEventFunction(@event);
		}

		protected void ThrowDomainException(string format, params object[] p)
		{
			if (p.Length > 0)
			{
				throw new DomainException(_ownerId, string.Format(format, p));
			}
			throw new DomainException(_ownerId, format);
		}
	}
}