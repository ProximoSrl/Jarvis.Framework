using Jarvis.Framework.Shared.Commands;
using System;
using System.Collections.Generic;

namespace Jarvis.Framework.Shared.Messages
{
    public class CommandHandled : IMessage
    {
        public enum CommandResult
        {
            Handled,
            Failed
        }

        public CommandResult Result { get; private set; }
        public Guid CommandId { get; private set; }
        public string Text { get; private set; }
        public string IssuedBy { get; private set; }
        public string Error { get; private set; }

        public bool IsDomainException { get; private set; }

        public IDictionary<string, string> Context { get; private set; }

        private String GetContextData(String key, String defaultValue = "")
        {

            return (Context != null && Context.ContainsKey(key))
                ? Context[key]
                : defaultValue;
        }

        public string SagaId { get { return GetContextData(MessagesConstants.SagaIdHeader); } }

        public CommandHandled(
            string issuedBy,
            Guid commandId,
            CommandResult result,
            string message = null,
            string error = null,
            bool isDomainException = false)
        {
            IssuedBy = issuedBy;
            Result = result;
            CommandId = commandId;
            Text = message;
            Error = error;
            IsDomainException = isDomainException;
        }

        public void CopyHeaders(ICommand command)
        {
            Context = new Dictionary<String, String>();
            foreach (var key in command.AllContextKeys)
            {
                Context[key] = command.GetContextData(key);
            }
        }

        public Guid MessageId
        {
            get { return CommandId; }
        }

        public string Describe()
        {
            return String.Format("Command {0} handled, result {1}!", CommandId, Result);
        }

    }

    public class CommitProjected
    {
        public Guid CommitId { get; private set; }
        public string ClientId { get; set; }
        public string IssuedBy { get; private set; }

        public CommitProjected(string issuedBy, Guid commitId, string clientId)
        {
            IssuedBy = issuedBy;
            CommitId = commitId;
            ClientId = clientId;
        }
    }
}
