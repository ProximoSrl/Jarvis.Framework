using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Fasterflect;
using Jarvis.Framework.Kernel.ProjectionEngine;
using Jarvis.Framework.Shared.Events;
using Jarvis.Framework.Shared.MultitenantSupport;
using System.Reflection;

namespace Jarvis.Framework.Kernel.Events
{
    /// <summary>
    /// Abstract class that should implement a projection
    /// </summary>
    public abstract class AbstractProjection : IProjection
    {
        protected ProjectionInfoAttribute _projectionInfoAttribute;

        public AbstractProjection()
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

        public abstract void Drop();
        public abstract void SetUp();

        public ProjectionInfoAttribute Info { get { return _projectionInfoAttribute; } }

        public virtual void StartRebuild(IRebuildContext context)
        {
            this.RebuildContext = context;
            foreach (var observer in _observers)
            {
                observer.RebuildStarted();
            }
        }

        private IRebuildContext RebuildContext { get; set; }

        public virtual void StopRebuild()
        {
            foreach (var observer in _observers)
            {
                observer.RebuildEnded();
            }
            this.RebuildContext = null;
        }

        public void Observe(IObserveProjection observer)
        {
            this._observers.Add(observer);
        }

        public bool Handle(IDomainEvent e, bool isReplay)
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

            if(invoker != null)
            {
                invoker.Invoke(this, e);
                return true;
            }

            return false;
        }

        public bool IsReplay {
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
    }
}
