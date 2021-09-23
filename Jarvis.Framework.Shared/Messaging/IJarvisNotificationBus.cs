using Castle.Core.Logging;
using Castle.MicroKernel;
using Fasterflect;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Jarvis.Framework.Shared.Messaging
{
    /// <summary>
    /// This is a generic bus interface not to depend on rebus
    /// and expecially to remove dependencies from MSMQ
    /// </summary>
    public interface IJarvisNotificationBus
    {
        /// <summary>
        /// Send an object in a storage to be polled
        /// </summary>
        /// <param name="message"></param>
        /// <param name="optionalHeaders"></param>
        /// <returns></returns>
        Task Publish(object message);
    }

    /// <summary>
    /// Automatically register all handler with castle as notification handler.
    /// </summary>
    /// <typeparam name="TMessage"></typeparam>
    public interface INotificationHandler<in TMessage>
    {
        Task Handle(TMessage message);
    }

    /// <summary>
    /// Simple subscriber for notifications.
    /// </summary>
    public interface IJarvisNotifierListener
    {
        /// <summary>
        /// This will subscribe to a type and can specify the action that will
        /// be called. The action will be wrapped to try catch, so every exception
        /// you can throw does not block the poller but will be swallowed.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="action"></param>
        void Subscribe<T>(Func<Object, Task> action) where T : class;

        /// <summary>
        /// Same of previsous subscribe, but without the nedd of async function
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="action"></param>
        void Subscribe<T>(Action<Object> action) where T : class;
    }

    /// <summary>
    /// Abstract class to handle notifier listener.
    /// </summary>
    public abstract class AbstractNotifierManager : IJarvisNotifierListener
    {
        public ILogger Logger { get; set; } = NullLogger.Instance;

        private readonly ConcurrentDictionary<Type, List<Func<Object, Task>>> registeredActions
            = new ConcurrentDictionary<Type, List<Func<Object, Task>>>();

        private readonly object _syncRoot = new object();
        private readonly IKernel _kernel;

        /// <summary>
        /// With reflection I need to grab the interface type that shoudl be registered in castle, this will
        /// imply that I want to cache them without the need to always use reflection.
        /// </summary>
        private readonly ConcurrentDictionary<Type, Type> _interfaceHandler = new ConcurrentDictionary<Type, Type>();

        private readonly ConcurrentDictionary<Type, Array> _castleRegisteredHandlers = new ConcurrentDictionary<Type, Array>();

        protected AbstractNotifierManager(IKernel kernel)
        {
            _kernel = kernel;
        }

        public void Subscribe<T>(Func<Object, Task> action) where T : class
        {
            lock (_syncRoot)
            {
                AddRegistration(typeof(T), action);
            }
        }

        public void Subscribe<T>(Action<Object> action) where T : class
        {
            lock (_syncRoot)
            {
                AddRegistration(typeof(T), obj =>
                {
                    action(obj);
                    return Task.CompletedTask;
                });
            }
        }

        private void AddRegistration(Type type, Func<Object, Task> action)
        {
            if (!registeredActions.TryGetValue(type, out var registerList))
            {
                registerList = new List<Func<object, Task>>();
                registeredActions[type] = registerList;
            }
            registerList.Add(action);
        }

        protected async Task Consume(Object message)
        {
            //First of all explicitly registered messages
            if (registeredActions.TryGetValue(message.GetType(), out var handlers))
            {
                foreach (var handler in handlers)
                {
                    try
                    {
                        await handler(message);
                    }
                    catch (Exception ex)
                    {
                        Logger.ErrorFormat(ex, "Notifier: Error notifying message {0} - {1}", message.GetType(), ex.Message);
                    }
                }
            }

            var handlerType = GetHandlerType(message);
            var allResolved = GetRegisteredHandler(handlerType);
            if (allResolved != null)
            {
                foreach (var resolved in allResolved)
                {
                    try
                    {
                        await (Task)resolved.CallMethod("Handle", message);
                    }
                    catch (Exception ex)
                    {
                        Logger.ErrorFormat(ex, "Notifier: Error notifying message {0} - {1}", message.GetType(), ex.Message);
                    }
                }
            }
        }

        /// <summary>
        /// Will clear internal cache, this is needed if you register more object into
        /// castle windsor so you nedd to be 100% sure that new classes are correctly registered.
        /// </summary>
        public void ClearCache()
        {
            _castleRegisteredHandlers.Clear();
        }

        private Array GetRegisteredHandler(Type handlerType)
        {
            if (!_castleRegisteredHandlers.TryGetValue(handlerType, out var array))
            {
                array = _kernel.ResolveAll(handlerType);
                _castleRegisteredHandlers[handlerType] = array;
            }
            return array;
        }

        private Type GetHandlerType(object message)
        {
            if (!_interfaceHandler.TryGetValue(message.GetType(), out var handlerType))
            {
                handlerType = typeof(INotificationHandler<>).MakeGenericType(message.GetType());
                _interfaceHandler[message.GetType()] = handlerType;
            }

            return handlerType;
        }

        public abstract Task StartPollingAsync();

        public abstract Task StopPollingAsync();
    }
}
