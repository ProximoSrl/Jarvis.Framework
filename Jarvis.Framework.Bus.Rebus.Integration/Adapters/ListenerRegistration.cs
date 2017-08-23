using System;
using System.Collections.Generic;
using Castle.MicroKernel;
using Castle.MicroKernel.Registration;
using Jarvis.Framework.Kernel.Engine;
using Rebus;
using System.Linq;
using Rebus.Bus;
using Rebus.Handlers;
using System.Threading.Tasks;

namespace Jarvis.Framework.Bus.Rebus.Integration.Adapters
{
	/// <summary>
	/// Register all listener for Saga an Message Handler.
	/// </summary>
	public class ListenerRegistration
	{
		readonly IProcessManagerListener[] _listeners;
		readonly IBus _bus;
		readonly IKernel _kernel;

		public ListenerRegistration(IProcessManagerListener[] listeners, IBus bus, IKernel kernel)
		{
			_listeners = listeners;
			_bus = bus;
			_kernel = kernel;
		}

		public void SubscribeSync() 
		{
			SubscribeAsync().Wait();
		}

		public async Task SubscribeAsync()
		{
			var subscribeToMessages = new HashSet<Type>();
			foreach (var listener in _listeners)
			{
				var processManagerType = listener.GetProcessType();
				foreach (var message in listener.ListeningTo)
				{
					subscribeToMessages.Add(message);

					// adapter
					var handlerType = typeof(IHandleMessages<>).MakeGenericType(message);
					var handlerImpl = typeof(RebusSagaAdapter<,,>).MakeGenericType(
						processManagerType,
						processManagerType.BaseType.GetGenericArguments()[0], //this is the state of the process manager
						message);

					_kernel.Register(
						Component
							.For(handlerType)
							.ImplementedBy(handlerImpl)
							.LifeStyle.Transient
					);
				}
				//do not forget to register the process manager, it has dependencies.
				_kernel.Register(
					Component
						.For(processManagerType)
						.ImplementedBy(processManagerType)
						.LifestyleTransient()
				);
			}

			//then should scan Castle for each component that registered IHandleMessage
			foreach (var handler in _kernel.GetAssignableHandlers(typeof(Object)))
			{
				var messageHandlers = handler.ComponentModel.Services
					.Where(s => s.IsGenericType && s.GetGenericTypeDefinition() == typeof(IHandleMessages<>));
				foreach (var handlerType in messageHandlers)
				{
					subscribeToMessages.Add(handlerType.GenericTypeArguments[0]);
				}
			}

			foreach (var type in subscribeToMessages)
			{
				// subscription
				await _bus.Subscribe(type).ConfigureAwait(false);
			}
		}
	}
}
