using System;

namespace Jarvis.Framework.Shared.Messages
{
	public class CommandHandled
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

		public CommandHandled(string issuedBy, Guid commandId, CommandResult result, string message = null, string error = null)
		{
			IssuedBy = issuedBy;
			Result = result;
			CommandId = commandId;
			Text = message;
		    Error = error;
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
