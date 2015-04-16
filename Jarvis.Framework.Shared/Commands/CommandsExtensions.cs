using Jarvis.Framework.Shared.Events;
using Jarvis.Framework.Shared.Messages;

namespace Jarvis.Framework.Shared.Commands
{
    public static class CommandsExtensions
    {
        public static bool EnableDiagnostics { get; set; }

        static CommandsExtensions()
        {
            EnableDiagnostics = false;
        }

        public static ICommand WithDiagnosticDescription(this ICommand command, string description)
        {
            if (!EnableDiagnostics)
                return command;
            
            if (description != null)
            {
                command.SetContextData("triggered-by-description", description);
            }
            return command;
        }

        public static ICommand WithDiagnosticTriggeredByInfo(this ICommand command, IMessage message, string description = null)
        {
            if (!EnableDiagnostics)
                return command;

            command.SetContextData("triggered-by", message.GetType().FullName);
            command.SetContextData("triggered-by-id", message.MessageId.ToString());

            var @event = message as DomainEvent;
            if (@event != null)
            {
                command.SetContextData("triggered-by-aggregate", @event.AggregateId);
            }

            return command.WithDiagnosticDescription(description);
        }

        public static string ExtractUserId(this ICommand command)
        {
            return command.GetContextData("user.id");
        }
    }
}