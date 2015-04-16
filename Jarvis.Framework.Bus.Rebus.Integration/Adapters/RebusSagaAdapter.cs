using Jarvis.Framework.Kernel.Engine;
using Jarvis.Framework.Shared.Messages;
using Jarvis.NEventStoreEx.CommonDomainEx;
using Jarvis.NEventStoreEx.CommonDomainEx.Persistence;
using Rebus;

namespace Jarvis.Framework.Bus.Rebus.Integration.Adapters
{
    public class RebusSagaAdapter<TProcessManager, TMessage> : 
        IHandleMessages<TMessage> where TMessage : IMessage where TProcessManager : class, ISagaEx
    {
        readonly ISagaRepositoryEx _repository;
        readonly IProcessManagerListener<TProcessManager> _listener;
        public RebusSagaAdapter(ISagaRepositoryEx repository, IProcessManagerListener<TProcessManager> listener)
        {
            _repository = repository;
            _listener = listener;
        }

        public void Handle(TMessage message)
        {
            var id = _listener.GetCorrelationId(message);
            var pm = _repository.GetById<TProcessManager>(id);
            pm.Transition(message);
            _repository.Save(pm, message.MessageId, null);
        }
    }
}