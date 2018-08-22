using Jarvis.Framework.Shared.Commands;
using Jarvis.Framework.Shared.Exceptions;
using Jarvis.Framework.Shared.Messages;
using Metrics;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading.Tasks;

namespace Jarvis.Framework.Shared.Support
{
    public static class SharedMetricsHelper
    {
        private static readonly Counter ConcurrencyExceptions = Metric.Counter("Concurrency Exceptions", Unit.Events);
        private static readonly Counter DomainExceptions = Metric.Counter("Domain Exceptions", Unit.Events);
        private static readonly Counter SecurityExceptions = Metric.Counter("Security Exceptions", Unit.Events);

        private static readonly Timer CommandTimer = Metric.Timer("Commands Execution", Unit.Commands);
        private static readonly Counter CommandCounter = Metric.Counter("CommandsDuration", Unit.Custom("ms"));

        public static void MarkConcurrencyException()
        {
            ConcurrencyExceptions.Increment();
        }

        public static void MarkDomainException(ICommand command, DomainException exception)
        {
            RaiseDomainExceptionEventHappened(command, exception);
            DomainExceptions.Increment();
        }

        public static void MarkSecurityException(ICommand command)
        {
            RaiseSecurityExceptionEventHappened( command);
            SecurityExceptions.Increment();
        }

        public static TimerContext StartCommandTimer(ICommand command)
        {
            return CommandTimer.NewContext(command.GetType().Name);
        }

        /// <summary>
        /// A command was executed correctly, increment the execution counter.
        /// </summary>
        /// <param name="command"></param>
        /// <param name="elapsed"></param>
        public static void IncrementCommandCounter(ICommand command, TimeSpan elapsed)
        {
            RaiseCommandExecutedEventHappened(command);
            CommandCounter.Increment(command.GetType().Name, elapsed.Milliseconds);
        }

        /// <summary>
        /// This is the only event that is raised when something interesting happened.
        /// </summary>
        public static event EventHandler<EventHappenedEventArgs> EventHappened;

        private static void RaiseDomainExceptionEventHappened(ICommand command, DomainException exception)
        {
            EventHappened?.Invoke(null, new EventHappenedEventArgs("DomainException", new Dictionary<String, String>()
            {
                ["CommandId"] = command.MessageId.ToString(),
                ["UserId"] = command.GetContextData(MessagesConstants.UserId),
                ["Exception"] = exception.Message,
            }));
        }

        private static void RaiseSecurityExceptionEventHappened(ICommand command)
        {
            EventHappened?.Invoke(null, new EventHappenedEventArgs("SecurityException", new Dictionary<String, String>()
            {
                ["CommandId"] = command.MessageId.ToString(),
                ["UserId"] = command.GetContextData(MessagesConstants.UserId)
            }));
        }

        private static void RaiseCommandExecutedEventHappened(ICommand command)
        {
            EventHappened?.Invoke(null, new EventHappenedEventArgs("CommandExecuted", new Dictionary<String, String>()
            {
                ["CommandType"] = command.GetType().Name,
                ["UserId"] = command.GetContextData(MessagesConstants.UserId)
            }));
        }
    }

    /// <summary>
    /// Some events are interesting for external user, we can use Metrics.NET reporting
    /// capabilities but reporting only return aggregate data. We want to be informed
    /// when something interesting happened in the software.
    /// </summary>
    public class EventHappenedEventArgs
    {
        public EventHappenedEventArgs(string eventType, IDictionary<String, String> eventProperties)
        {
            EventType = eventType;
            EventProperties = eventProperties;
        }

        public String EventType { get; private set; }

        public IDictionary<String, String> EventProperties { get; private set; }
    }
}
