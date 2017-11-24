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
	/// Register in castle windsor all listeners for Saga an Message Handlers.
	/// This DOES NOT NEED THE IBus and ONLY register handlers in castle.
	/// </summary>
	public class ProcessManagerCastleListenerRegistration
	{
		private readonly IProcessManagerListener[] _listeners;
		private readonly IKernel _kernel;

		public ProcessManagerCastleListenerRegistration(IProcessManagerListener[] listeners, IKernel kernel)
		{
			_listeners = listeners;
			_kernel = kernel;
		}

		public void Subscribe()
		{
			foreach (var listener in _listeners)
			{
				var processManagerType = listener.GetProcessType();
				foreach (var message in listener.ListeningTo)
				{
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
							.LifeStyle.Transient);
				}

				//do not forget to register the process manager, it has dependencies.
				_kernel.Register(
					Component
						.For(processManagerType)
						.ImplementedBy(processManagerType)
						.LifestyleTransient()
				);
			}
		}
	}
}
