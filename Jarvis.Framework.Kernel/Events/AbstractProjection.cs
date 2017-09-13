using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Fasterflect;
using Jarvis.Framework.Kernel.ProjectionEngine;
using Jarvis.Framework.Shared.Events;
using Jarvis.Framework.Shared.MultitenantSupport;
using System.Reflection;
using System.Threading.Tasks;

namespace Jarvis.Framework.Kernel.Events
{
    /// <summary>
    /// Abstract class that should implement a projection
    /// </summary>
    public abstract class AbstractProjection : IProjection
    {
        protected ProjectionInfoAttribute _projectionInfoAttribute;

        protected AbstractProjection()
        {
            _projectionInfoAttribute = GetType().GetCustomAttribute<ProjectionInfoAttribute>();
            if (_projectionInfoAttribute == null)
                throw new Exception($"Projection {GetType().FullName} does not contain ProjectionInfoAttribute");
        }

        /// <summary>
        /// Single thread projection
        /// </summary>
        private readonly IDictionary<string, MethodInvoker> _handlersCache = new Dictionary<string, MethodInvoker>();
        private readonly IList<IObserveProjection> _observers = new List<IObserveProjection>();

        public Castle.Core.Logging.ILogger Logger { get; set; }

        public bool IsRebuilding { get; private set; }

        public TenantId TenantId { get; set; }

        public abstract Task DropAsync();

        public abstract Task SetUpAsync();

        public ProjectionInfoAttribute Info { get { return _projectionInfoAttribute; } }

        public virtual async Task StartRebuildAsync(IRebuildContext context)
        {
            this.RebuildContext = context;
            foreach (var observer in _observers)
            {
                await observer.RebuildStartedAsync().ConfigureAwait(false);
            }
        }

        private IRebuildContext RebuildContext { get; set; }

        public virtual async Task StopRebuildAsync()
        {
            foreach (var observer in _observers)
            {
                await observer.RebuildEndedAsync().ConfigureAwait(false);
            }
            this.RebuildContext = null;
        }

        public void Observe(IObserveProjection observer)
        {
            this._observers.Add(observer);
        }

        public async virtual Task<Boolean> HandleAsync(IDomainEvent e, bool isReplay)
        {
            IsRebuilding = isReplay;

            var eventType = e.GetType();
            string key = eventType.FullName;

            MethodInvoker invoker = null;
            if (!_handlersCache.TryGetValue(key, out invoker))
            {
                var methodInfo = this.GetType().Method("On", new Type[] { eventType }, Flags.InstancePublic);
                if (methodInfo != null)
                {
                    invoker = methodInfo.DelegateForCallMethod();
                }
                _handlersCache.Add(key, invoker);
            }

            if (invoker != null)
            {
                var retValueTask = invoker.Invoke(this, e) as Task;
                if (retValueTask != null)
                {
                    await retValueTask.ConfigureAwait(false);
                }
                return true;
            }

            return false;
        }

        public bool IsReplay
        {
            get { return IsRebuilding; }
        }

        /// <summary>
        /// Priority of the projection, the lesser is the value, the later it is
        /// executed in the slot. If you want this projection to be executed 
        /// after another projection of the same slot you need to give it a lower
        /// priority.
        /// </summary>
        public virtual int Priority
        {
            get { return 0; }
        }

        public virtual void CheckpointProjected(Int64 checkpointToken)
        {
            // Method intentionally left empty.
        }
    }
}
