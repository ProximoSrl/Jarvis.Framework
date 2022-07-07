﻿using Castle.Core.Logging;
using Castle.MicroKernel.Registration;
using Castle.Windsor;
using Jarvis.Framework.Bus.Rebus.Integration.Adapters;
using Jarvis.Framework.Kernel.Commands;
using Jarvis.Framework.Kernel.Engine;
using Rebus.Handlers;
using System.Linq;
using System.Reflection;

namespace Jarvis.Framework.Bus.Rebus.Integration.Support
{
    /// <summary>
    /// Registra nel bus i Command handlers
    /// </summary>
    public class CommandHandlersRegistration
    {
        public ILogger Logger { get; set; } = NullLogger.Instance;

        private readonly IWindsorContainer _container;
        private readonly Assembly[] _assemblies;

        public CommandHandlersRegistration(IWindsorContainer container, params Assembly[] assemblies)
        {
            this._container = container;
            this._assemblies = assemblies;
        }

        /// <summary>
        /// Permette la registrazione dei command handlers presenti negli assembly indicati
        /// </summary>
        public void Register()
        {
            foreach (var assembly in _assemblies)
            {
                RegisterAssembly(assembly);
            }
        }

        /// <summary>
        /// Registra i command handler presenti nell'assembly indicato
        /// </summary>
        /// <param name="assembly">assembly in cui ricercare i command handler</param>
        private void RegisterAssembly(Assembly assembly)
        {
            var types = assembly.GetTypes().Where(x => typeof(ICommandHandler).IsAssignableFrom(x) && x.IsClass && !x.IsAbstract).ToList();

            foreach (var type in types)
            {
                // estrae il command..
                var commandHandlerServiceType = type.GetInterfaces().Single(x =>
                    x.IsGenericType
                    && x.GetGenericTypeDefinition() == typeof(ICommandHandler<>));

                var commandType = commandHandlerServiceType.GetGenericArguments()[0];

                var messageHandlerServiceType = typeof(IHandleMessages<>).MakeGenericType(commandType);
                var messageHandlerType = typeof(MessageHandlerToCommandHandlerAdapter<>).MakeGenericType(commandType);

                if (Logger.IsDebugEnabled) Logger.DebugFormat("Handler {0} -> {1}", commandType, messageHandlerType.FullName);

                //First Check if the real command handler is installed.
                if (!_container.Kernel.HasComponent(commandHandlerServiceType))
                {
                    _container.Register(
                        Component
                            .For(commandHandlerServiceType)
                            .ImplementedBy(type)
                            .LifestyleTransient()
                        );
                }

                //then register the handler for rebus.
                _container.Register(
                    Component
                        .For(messageHandlerServiceType)
                        .ImplementedBy(messageHandlerType)
                        .LifestyleTransient()
                    );
            }
        }
    }
}
