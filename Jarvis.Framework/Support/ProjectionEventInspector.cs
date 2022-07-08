using Jarvis.Framework.Kernel.Engine;
using Jarvis.Framework.Kernel.Events;
using Jarvis.Framework.Shared.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Jarvis.Framework.Kernel.Support
{
    public class ProjectionEventInspector
    {
        public Int32 TotalEventCount { get { return _eventTypes.Count; } }

        public HashSet<Type> EventHandled { get; private set; }

        private readonly HashSet<Type> _eventTypes;

        public ProjectionEventInspector()
        {
            _eventTypes = new HashSet<Type>();
            EventHandled = new HashSet<Type>();
        }

        public void AddAssembly(Assembly assembly)
        {
            foreach (var type in assembly.GetTypes().Where(t => !t.IsAbstract))
            {
                if (typeof(DomainEvent).IsAssignableFrom(type))
                {
                    _eventTypes.Add(type);
                }
            }
        }

        /// <summary>
        /// Inspect a projection type, add into an internal buffer the list of every
        /// domain that is handled with an On function, and returns this lists to the caller.
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public HashSet<Type> InspectProjectionForEvents(Type type)
        {
            HashSet<Type> retValue = new HashSet<Type>();
            var allMethods = type.GetMethods(BindingFlags.Instance | BindingFlags.FlattenHierarchy | BindingFlags.Public | BindingFlags.NonPublic);
            var handlingMethods = allMethods
                .Where(m => m.Name == "On" && !m.IsAbstract)
                .Select(m => m.GetParameters())
                .Where(p => p.Length == 1 && typeof(DomainEvent).IsAssignableFrom(p[0].ParameterType));
            foreach (var parameter in handlingMethods)
            {
                foreach (var eventHandled in ScanForDomainEvent(parameter))
                {
                    AddHandledType(eventHandled);
                }
            }

            //Now scan the IHandleMessage
            var typesExplicitlyHandled = type
                .GetInterfaces()
                .Where(i => i.IsGenericType && typeof(IEventHandler<>) == i.GetGenericTypeDefinition())
                .Select(i => i.GetGenericArguments()[0])
                .Where(i => !i.IsAbstract);

            foreach (var typeExplicitlyHandled in typesExplicitlyHandled)
            {
                AddHandledType(typeExplicitlyHandled);
            }
            return retValue;

            void AddHandledType(Type typeToAdd)
            {
                retValue.Add(typeToAdd);
                EventHandled.Add(typeToAdd);
                var upcastedEventChain = StaticUpcaster.FindUpcastedEvents(typeToAdd);
                foreach (var evt in upcastedEventChain)
                {
                    retValue.Add(evt);
                    EventHandled.Add(evt);
                }
            }
        }

        private IEnumerable<Type> ScanForDomainEvent(ParameterInfo[] parameter)
        {
            var eventType = parameter[0].ParameterType;
            if (!eventType.IsAbstract) yield return eventType;
            var inheritedEvents = _eventTypes.Where(eventType.IsAssignableFrom);
            foreach (var inheritedEvent in inheritedEvents)
            {
                if (!inheritedEvent.IsAbstract) yield return inheritedEvent;
            }
        }

        public void ResetHandledEvents()
        {
            EventHandled.Clear();
        }
    }
}
