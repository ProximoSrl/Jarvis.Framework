using System;
using Castle.Core.Logging;
using Jarvis.Framework.Kernel.Engine;
using Jarvis.Framework.Shared.Messages;
using Jarvis.NEventStoreEx.CommonDomainEx;
using Jarvis.NEventStoreEx.CommonDomainEx.Persistence;
using Rebus;
using Rebus.Handlers;
using System.Threading.Tasks;

namespace Jarvis.Framework.Bus.Rebus.Integration.Adapters
{
    public class RebusSagaAdapter<TProcessManager, TMessage> : 
        IHandleMessages<TMessage> where TMessage : IMessage where TProcessManager : class, ISagaEx
    {
        private readonly ISagaRepositoryEx _repository;
        private readonly IProcessManagerListener<TProcessManager> _listener;
        private readonly ILogger _logger;
        public RebusSagaAdapter(ISagaRepositoryEx repository, IProcessManagerListener<TProcessManager> listener, ILogger logger)
        {
            _repository = repository;
            _listener = listener;
            _logger = logger;
        }

        public async Task Handle(TMessage message)
        {
            TProcessManager pm = null;
            try
            {
                var id = _listener.GetCorrelationId(message);
                if (String.IsNullOrEmpty(id))
                    return;
                pm = _repository.GetById<TProcessManager>(id);
                pm.Transition(message);
                _repository.Save(pm, message.MessageId, null);
            }
            catch (Exception ex)
            {
                _logger.ErrorFormat(ex, "Error dispatching message {0} [{1}] to saga {2}", 
                    message.GetType().Name, message.Describe(), pm == null ? "null" : pm.GetType().Name );
                throw;
            }
        }
    }
}