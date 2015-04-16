using System;
using System.Collections.Generic;
using Jarvis.Framework.Kernel.Engine;
using Jarvis.Framework.Kernel.Store;
using Jarvis.Framework.Shared.Commands;
using Jarvis.Framework.Shared.IdentitySupport;
using Jarvis.NEventStoreEx.CommonDomainEx;
using Jarvis.NEventStoreEx.CommonDomainEx.Persistence;
using Jarvis.NEventStoreEx.CommonDomainEx.Persistence.EventStore;

namespace Jarvis.Framework.Kernel.Commands
{
    public abstract class RepositoryCommandHandler<TAggregate, TCommand> : AbstractCommandHandler<TCommand>
        where TCommand : ICommand
        where TAggregate : class, IAggregateEx
    {
        // ReSharper disable MemberCanBeProtected.Global
        public IRepositoryEx Repository { get; set; }
        // ReSharper restore MemberCanBeProtected.Global
        private Guid _commitId;

        public IConstructAggregatesEx AggregateFactory { get; set; }
        TCommand _currentCommand;
        public override void Handle(TCommand cmd)
        {
            if (cmd.MessageId == Guid.Empty) 
            {
                Logger.ErrorFormat("Cmd {0} received with empty id.", cmd.GetType().Name);
                throw new Exception("Invalid command id");
            }

            _currentCommand = cmd;
            _commitId = cmd.MessageId;
            base.Handle(cmd);
            if (Logger.IsDebugEnabled) Logger.DebugFormat("Handled command type {0} id {1} with handler {2}", cmd.GetType().Name, cmd.MessageId, this.GetType().Name);
        }
        protected void FindAndModify(EventStoreIdentity id, Action<TAggregate> callback, bool createIfNotExists = false)
        {
            var aggregate = Repository.GetById<TAggregate>(id);
            if (!createIfNotExists && aggregate.Version == 0)
                throw new UninitializedAggregateException();
            callback(aggregate);
            Repository.Save(aggregate, _commitId, StoreCommandHeaders);
        }

        protected void Save(TAggregate aggregate)
        {
            Repository.Save(aggregate, _commitId, StoreCommandHeaders);
        }

        protected TAggregate CreateNewAggregate(IIdentity identity)
        {
            return (TAggregate)AggregateFactory.Build(typeof(TAggregate), identity, null);
        }

        protected void StoreCommandHeaders(IDictionary<string, object> headers)
        {
            foreach (var key in _currentCommand.AllContextKeys)
            {
                headers.Add(key, _currentCommand.GetContextData(key));
            }
        }
    }
}