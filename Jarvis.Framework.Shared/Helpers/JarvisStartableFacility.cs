using System;
using System.Collections.Generic;
using System.Linq;

namespace Jarvis.Framework.Shared.Helpers
{
    using Castle.MicroKernel;
    using Castle.MicroKernel.Facilities;
    using Castle.MicroKernel.Registration;
    using Castle.MicroKernel.SubSystems.Conversion;
    using Castle.Core;
    using Castle.MicroKernel.Context;
    using Castle.Facilities.Startable;
    using global::Metrics;
    using Castle.Core.Logging;
	using Jarvis.Framework.Shared.Exceptions;

	/// <summary>
	/// All these classes are copied from the original classes of Castle.Windsor and they 
	/// were adapted to change the facility so we can have a delayed start.
	/// 
	/// This facility uses the same IStartable interface, but it does not start any component
	/// automatically, instead it simply start everything when requested.
	/// </summary>
	public class JarvisStartableFacility
        : AbstractFacility
    {
        private ITypeConverter _converter;

        private List<HandlerInfo> _handlersWithStartError;

        public const String PriorityExtendedPropertyKey = "startable-priority";

        private Int32 _timeoutInSecondsBeforeRetryRestartFailedServices;

        private System.Threading.Timer _retryStartTimer;

        private ILogger _logger;

        public static class Priorities
        {
            public const Int32 Highest = 0;
            public const Int32 High = 1;
            public const Int32 Normal = 2;
            public const Int32 Low = 3;
            public const Int32 Lowest = 4;
        }

        private class HandlerInfo
        {
            public IHandler Handler { get; set; }

            public Int32 Priority { get; set; }

            public Exception StartException { get; set; }

            public String Description
            {
                get
                {
                    return "Handler: " + Handler.ComponentModel.ComponentName.Name;
                }
            }
        }

        /// <summary>
        /// Constructor for startable facility
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="timeoutInSecondsBeforeRetryRestartFailedServices">If an handler failed to start because
        /// it throws exception in start method, this facility can retry to restart it after a certain amout of seconds.
        /// This will guarantee that if a service is not able to start due to some reason (db momentarly down) the entire
        /// service can start and the component will try to restart.</param>
        public JarvisStartableFacility(
            ILogger logger,
            Int32 timeoutInSecondsBeforeRetryRestartFailedServices = 120)
        {
            _logger = logger;
            _timeoutInSecondsBeforeRetryRestartFailedServices = timeoutInSecondsBeforeRetryRestartFailedServices;
            _handlersWithStartError = new List<HandlerInfo>();
        }

        protected override void Init()
        {
            _converter = Kernel.GetConversionManager();
            Kernel.ComponentModelBuilder.AddContributor(new StartableContributor(_converter));
        }

        public void StartAllIStartable()
        {
            HealthChecks.RegisterHealthCheck("Startable", (Func<HealthCheckResult>)StartableHealtCheck);
            IHandler[] handlers = Kernel.GetAssignableHandlers(typeof(object));
            IEnumerable<HandlerInfo> startableHandlers = GetStartableHandlers(handlers);
            foreach (var handlerInfo in startableHandlers.OrderBy(h => h.Priority))
            {
                try
                {
                    handlerInfo.Handler.Resolve(CreationContext.CreateEmpty());
                    _logger.InfoFormat("Component {0} started correctly.", handlerInfo.Description);
                }
                catch (Exception ex)
                {
                    _logger.ErrorFormat(ex, "Cannot start component {0} because it raised exception. Retry in {1} seconds.", handlerInfo.Description, _timeoutInSecondsBeforeRetryRestartFailedServices);
                    handlerInfo.StartException = ex;
                    _handlersWithStartError.Add(handlerInfo);
                }
            }
            if (_handlersWithStartError.Count > 0)
                _retryStartTimer = new System.Threading.Timer(
                    RetryStartTimerCallback,
                    null,
                    1000 * _timeoutInSecondsBeforeRetryRestartFailedServices, //Due time
                    1000 * _timeoutInSecondsBeforeRetryRestartFailedServices //Period
                );
        }

        private Int32 _retryCount = 0;
        Boolean _executing = false;
        private void RetryStartTimerCallback(object state)
        {
            _retryCount++;
            if (!_executing)
            {
                try
                {
                    _executing = true;
                    foreach (var handlerInfo in _handlersWithStartError.ToList())
                    {
                        try
                        {
                            handlerInfo.Handler.Resolve(CreationContext.CreateEmpty());
                            _handlersWithStartError.Remove(handlerInfo);
                            _logger.InfoFormat("Component {0} started correctly after {1} retries.", handlerInfo.Description, _retryCount);
                        }
                        catch (Exception ex)
                        {
                            //Handler still failed start, leave it into collection and will be restarted.
                            _logger.ErrorFormat(ex, "Cannot start component {0} because it raised exception. Retry in {1} seconds.", handlerInfo.Description, _timeoutInSecondsBeforeRetryRestartFailedServices);

                        }
                    }
                }
                finally
                {
                    _executing = false;
                    if (_handlersWithStartError.Count == 0) _retryStartTimer.Dispose();
                }
            }

        }

        private HealthCheckResult StartableHealtCheck()
        {
            if (_handlersWithStartError.Count == 0)
                return HealthCheckResult.Healthy();

			return HealthCheckResult.Unhealthy(
                "The following startable object throw error on start: \n{0}",
                    _handlersWithStartError.Select(h => h.Description + " Ex: " + h.StartException.GetExceptionDescription())
                        .Aggregate((s1, s2) => s1 + "\n" + s2));
        }

        private IEnumerable<HandlerInfo> GetStartableHandlers(IHandler[] handlers)
        {
            foreach (var handler in handlers)
            {
                if (HasStartableWithExtendedProperty(handler) ||
                    typeof(IStartable).IsAssignableFrom(handler.ComponentModel.Implementation))
                {
                    //is startable
                    var priorityValue = handler.ComponentModel.ExtendedProperties[PriorityExtendedPropertyKey];
                    Int32 priority = Priorities.Normal;
                    if (priorityValue is Int32)
                    {
                        priority = (Int32)priorityValue;
                    }
                    yield return new HandlerInfo() { Handler = handler, Priority = priority };
                }
            }
        }

        public static bool HasStartableWithExtendedProperty(IHandler handler)
        {
            var startable = handler.ComponentModel.ExtendedProperties["startable"];
            var isStartable = (bool?)startable;
            return isStartable.GetValueOrDefault();
        }


    }

    public static class JarvisStartableHelper
    {
        public static ComponentRegistration<T> WithStartablePriority<T>(
            this ComponentRegistration<T> registration,
            Int32 priority)
          where T : class
        {
            registration.ExtendedProperties(
                new Property(JarvisStartableFacility.PriorityExtendedPropertyKey,
                priority));
            return registration;
        }

        public static ComponentRegistration<T> WithStartablePriorityHighest<T>(this ComponentRegistration<T> registration)
            where T : class
        {
            registration.ExtendedProperties(
                new Property(JarvisStartableFacility.PriorityExtendedPropertyKey,
                JarvisStartableFacility.Priorities.Highest));
            return registration;
        }

        public static ComponentRegistration<T> WithStartablePriorityHigh<T>(this ComponentRegistration<T> registration)
        where T : class
        {
            registration.ExtendedProperties(
                new Property(JarvisStartableFacility.PriorityExtendedPropertyKey,
                JarvisStartableFacility.Priorities.High));
            return registration;
        }

        public static ComponentRegistration<T> WithStartablePriorityLow<T>(this ComponentRegistration<T> registration)
        where T : class
        {
            registration.ExtendedProperties(
                new Property(JarvisStartableFacility.PriorityExtendedPropertyKey,
                JarvisStartableFacility.Priorities.Low));
            return registration;
        }

        public static ComponentRegistration<T> WithStartablePriorityLowest<T>(this ComponentRegistration<T> registration)
          where T : class
        {
            registration.ExtendedProperties(
                new Property(JarvisStartableFacility.PriorityExtendedPropertyKey,
                JarvisStartableFacility.Priorities.Lowest));
            return registration;
        }
    }

}
