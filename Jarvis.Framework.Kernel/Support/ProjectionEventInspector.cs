using Jarvis.Framework.Shared.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Jarvis.Framework.Kernel.Support
{
    public class ProjectionEventInspector
    {
        public Int32 TotalEventCount { get { return _eventTypes.Count; } }

        public HashSet<Type> EventHandled { get; private set; }

        private HashSet<Type> _eventTypes;

        public ProjectionEventInspector()
        {
            _eventTypes = new HashSet<Type>();
            EventHandled = new HashSet<Type>();
        }

        public void AddAssembly(Assembly assembly)
        {
            foreach (var type in assembly.GetTypes().Where(t => t.IsAbstract == false))
            {
                if (typeof(DomainEvent).IsAssignableFrom(type))
                {
                    _eventTypes.Add(type);
                }
            }
        }

        public void InspectProjectionForEvents(Type type)
        {
            var allMethods = type.GetMethods(BindingFlags.Instance | BindingFlags.FlattenHierarchy | BindingFlags.Public | BindingFlags.NonPublic);
            var handlingMethods = allMethods
                .Where(m => m.Name == "On" & m.IsAbstract == false)
                .Select(m => m.GetParameters())
                .Where(p => p.Length == 1 && typeof(DomainEvent).IsAssignableFrom(p[0].ParameterType));
            foreach (var parameter in handlingMethods)
            {
                AddDomainEventHandled(parameter);
            }
        }

        private void AddDomainEventHandled(ParameterInfo[] parameter)
        {
            var eventType = parameter[0].ParameterType;
            if (!eventType.IsAbstract) EventHandled.Add(eventType);
            var inheritedEvents = _eventTypes.Where(eventType.IsAssignableFrom);
            foreach (var inheritedEvent in inheritedEvents)
            {
                if (!inheritedEvent.IsAbstract) EventHandled.Add(inheritedEvent);
            }
        }

        public void ResetHandledEvents()
        {
            EventHandled.Clear();
        }
    }
}
