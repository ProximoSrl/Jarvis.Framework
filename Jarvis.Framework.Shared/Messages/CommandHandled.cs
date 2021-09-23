﻿using Jarvis.Framework.Shared.Commands;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.Options;
using System;
using System.Collections.Generic;

namespace Jarvis.Framework.Shared.Messages
{
    public class CommandHandled : IMessage
    {
        public enum CommandResult
        {
            Handled = 0,
            Failed = 1
        }

        public CommandResult Result { get; private set; }
        public Guid CommandId { get; private set; }
        public Guid MessageId { get; private set; }

        public string Text { get; private set; }
        public string IssuedBy { get; private set; }
        public string Error { get; private set; }

        public bool IsDomainException { get; private set; }

        [BsonDictionaryOptions(Representation = DictionaryRepresentation.ArrayOfArrays)]
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
            MessageId = Guid.NewGuid();
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

        public string Describe()
        {
            return String.Format("Command {0} handled, result {1}!", CommandId, Result);
        }
    }

    public class CommitProjected : IMessage
    {
        public Guid CommitId { get; private set; }
        public string ClientId { get; set; }
        public string IssuedBy { get; private set; }

        public Guid MessageId { get; private set; }

        public CommitProjected(string issuedBy, Guid commitId, string clientId)
        {
            IssuedBy = issuedBy;
            CommitId = commitId;
            ClientId = clientId;
            MessageId = Guid.NewGuid();
        }

        public string Describe()
        {
            return $"Projected commit {CommitId}";
        }
    }
}
