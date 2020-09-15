using Jarvis.Framework.Shared.Exceptions;
using NStore.Core.Snapshots;
using System;

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

        /// <summary>
        /// Store original Id of containing aggregate.
        /// </summary>
		protected String OwnerId { get; private set; }

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
            OwnerId = ownerId;
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
                throw new DomainException(OwnerId, string.Format(format, p));
            }
            throw new DomainException(OwnerId, format);
        }

        public void EventEmitting(object @event)
        {
            OnEventEmitting(@event);
        }

        protected virtual void OnEventEmitting(object @event)
        {
            //Leave to inherited object the ability to intercept and handle the event.
        }
    }
}