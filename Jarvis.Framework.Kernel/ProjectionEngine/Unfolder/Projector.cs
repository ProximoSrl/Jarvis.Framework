using Fasterflect;
using Jarvis.Framework.Shared.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Jarvis.NEventStoreEx.CommonDomainEx;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security.Policy;
using Jarvis.Framework.Kernel.Engine;

namespace Jarvis.Framework.Kernel.ProjectionEngine.Unfolder
{
    /// <summary>
    /// Unfold events, it is used to generate InMemory projection.
    /// </summary>
    [Serializable]
    public abstract class BaseAggregateQueryModel : ICloneable
    {
   
        public void Apply(IDomainEvent evt)
        {
            var method = GetType().Method("When", new[] { evt.GetType() }, Flags.InstanceAnyVisibility);
            if (method != null)
                method.Invoke(this, new object[] { evt });
        }

        public virtual Object Clone()
        {
            return DeepCloneMe(this);
        }

        /// <summary>
        /// Create a deep clone with Serialization. It can be overriden in derived
        /// class if you want a more performant way to clone the object. Using 
        /// serialization gives us the advantage of automatic creation of deep cloned
        /// object.
        /// </summary>
        /// <param name="objectToClone"></param>
        /// <returns></returns>
        protected virtual Object DeepCloneMe(Object objectToClone)
        {
            IFormatter formatter = new BinaryFormatter();
            using (Stream stream = new MemoryStream())
            {
                formatter.Serialize(stream, objectToClone);
                stream.Seek(0, SeekOrigin.Begin);
                return formatter.Deserialize(stream);
            }
        }

    }

    /// <summary>
    /// Abstract component used to create an on-the-fly projection of an aggregate. It is useful
    /// to have different viewModel for aggregate that can be requested at specific point in time.
    /// </summary>
    /// <typeparam name="TQueryModel"></typeparam>
    public abstract class Projector<TQueryModel> : ISnapshotable where TQueryModel : BaseAggregateQueryModel, new()
    {
        protected TQueryModel _state;

        public IIdentity Id { get; protected set; }

        public Int32 Version { get; private set; }

        private Int32 _snapshotVersion;

        /// <summary>
        /// Signature is used to guarantee validity of snapshots.
        /// </summary>
        public virtual String Signature { get { return "default"; } }

        public abstract String BucketId { get; }

        protected ISnapshotPersister _snapshotPersister;

        public Projector()
        {
            _state = new TQueryModel();
            _snapshotVersion = Version = 0;
        }

        public void ApplyEvent(DomainEvent evt)
        {
            _state.Apply(evt);
            this.Version++;
        }

        public TQueryModel GetProjection()
        {
            return _state;
        }

        public Boolean ValidateMemento(IMementoEx memento)
        {
            var validationErrors = InnerValidateMemento(memento);
            return String.IsNullOrEmpty(validationErrors);
        }

        public virtual void Restore(IMementoEx memento)
        {
            var validationErrors = InnerValidateMemento(memento);
            if (!String.IsNullOrEmpty(validationErrors))
                throw new ArgumentException(validationErrors, "memento");

            var eventUnfolderMemento = (EventUnfolderMemento) memento;
            _state = (TQueryModel)((TQueryModel)eventUnfolderMemento.Payload).Clone();
            _snapshotVersion = Version = memento.Version;
        }

        internal virtual Boolean ShouldSnapshot()
        {
            return false;
        }

        private String InnerValidateMemento(IMementoEx memento)
        {
            if (!(memento is EventUnfolderMemento))
                return "Cannot restore Projector from type " + memento.GetType() + " expecting EventUnfolderSnapshot.";

            EventUnfolderMemento eventUnfolderMemento = (EventUnfolderMemento)memento;
            if (eventUnfolderMemento.Payload.GetType() != typeof (TQueryModel))
                return "Payload of the memento is expected to be of type " + typeof (TQueryModel) + " but it is of type " +
                       eventUnfolderMemento.Payload.GetType();

            if (eventUnfolderMemento.Signature != this.Signature)
               return "Memento has wrong signature [" + eventUnfolderMemento + "] unfolder signature is " + Signature;

            return String.Empty;
        }

        public virtual IMementoEx GetSnapshot()
        {
            var clone = _state.Clone();
            EventUnfolderMemento memento = new EventUnfolderMemento();
            memento.Id = this.Id;
            memento.Version = this.Version;
            memento.Payload = clone;
            memento.Signature = this.Signature;
            return memento;
        }
    }

    public class EventUnfolderMemento : IMementoEx
    {
        public IIdentity Id { get; set; }
        public int Version { get; set; }
        public Object Payload { get; set; }

        /// <summary>
        /// Used to guarantee validity of the Memento.
        /// </summary>
        public String Signature { get; set; }
    }

}
