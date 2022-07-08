using Castle.Core.Logging;
using Jarvis.Framework.Kernel.Engine;
using Jarvis.Framework.Shared.Exceptions;
using Jarvis.Framework.Shared.Messages;
using NStore.Core.Streams;
using NStore.Domain;
using Rebus.Handlers;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Jarvis.Framework.Rebus.Adapters
{
    /// <summary>
    /// This is the saga adapter, that dispatch all messages to the saga, it is asyncronous
    /// because it poll the events from the bus and then dispatch to saga
    /// IMPORTANT: we need to be resilient to the concurrency exception on save.
    /// </summary>
    /// <typeparam name="TProcessManager"></typeparam>
    /// <typeparam name="TState"></typeparam>
    /// <typeparam name="TMessage"></typeparam>
    public class RebusSagaAdapter<TProcessManager, TState, TMessage> : IHandleMessages<TMessage>
        where TMessage : IMessage
        where TState : class, new()
        where TProcessManager : ProcessManager<TState>
    {
        private readonly ILogger _logger;
        private readonly IRepository _repository;
        private readonly IProcessManagerListener<TProcessManager, TState> _listener;
        private readonly int _numberOfConcurrencyExceptionBeforeRandomSleeping = 5;

        public RebusSagaAdapter(IRepository repository, IProcessManagerListener<TProcessManager, TState> listener, ILogger logger)
        {
            _repository = repository;
            _listener = listener;
            _logger = logger;
            _repository = repository;
        }

        public async Task Handle(TMessage message)
        {
            TProcessManager pm = null;
            int i = 0;
            bool done = false;
            while (!done && i < 100)
            {
                try
                {
                    var id = _listener.GetCorrelationId(message);
                    if (string.IsNullOrEmpty(id))
                        return;
                    if (i > 0)
                    {
                        _repository.Clear(); //remember to clear, we are retrying.
                    }
                    pm = await _repository.GetByIdAsync<TProcessManager>(id).ConfigureAwait(false);
                    pm.MessageReceived(message);
                    await _repository.SaveAsync(pm, message.MessageId.ToString(), null).ConfigureAwait(false);
                    done = true;
                }
                catch (ConcurrencyException ex)
                {
                    // retry
                    if (_logger.IsInfoEnabled) _logger.InfoFormat(ex, $"Saga [{pm?.GetType()?.Name}]: concurrency exception dispatching {message.GetType().FullName} {message.MessageId} [{message.Describe()}]. Retry count: {i}");
                    // increment the retries counter and maybe add a delay
                    if (i++ > _numberOfConcurrencyExceptionBeforeRandomSleeping)
                    {
                        Thread.Sleep(new Random(DateTime.Now.Millisecond).Next(i * 10));
                    }
                }
                catch (DomainException ex)
                {
                    //Strange, saga should not raise domain exception, but just in case, stop execution.
                    done = true;
                    _logger.ErrorFormat(ex, $"Saga [{pm?.GetType()?.Name}]: DomainException dispatching {message.GetType()} [MessageId: {message.MessageId}] : {message.Describe()} : {ex.Message}", ex);
                }
                catch (Exception ex)
                {
                    _logger.ErrorFormat(ex, $"Saga [{pm?.GetType()?.Name}]: Generic Exception on command {message.GetType()} [MessageId: {message.MessageId}] : {message.Describe()} : {ex.Message}", ex);
                    throw; //rethrow exception.
                }
            }
            if (!done)
            {
                _logger.ErrorFormat($"Saga [{pm?.GetType()?.Name}]: Too many conflict on command {message.GetType()} [MessageId: {message.MessageId}] : {message.Describe()}");
            }
            if (_logger.IsDebugEnabled) _logger.DebugFormat($"Saga [{pm?.GetType()?.Name}]: Handled {message.GetType().FullName} {message.MessageId} {message.Describe()}");
        }
    }
}