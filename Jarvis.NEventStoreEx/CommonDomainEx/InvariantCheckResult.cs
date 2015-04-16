using System;

namespace Jarvis.NEventStoreEx.CommonDomainEx
{
    public class InvariantCheckResult
    {
        public static InvariantCheckResult Success = 
            new InvariantCheckResult();

        public static InvariantCheckResult CreateForError(String errorMessage)
        {
            return new InvariantCheckResult(errorMessage);
        }

        private InvariantCheckResult()
        {
            Ok = true;
        }

        public static implicit operator Boolean(InvariantCheckResult result)
        {
            return result.Ok;
        }

        private InvariantCheckResult(string errorMessage)
        {
            Ok = false;
            ErrorMessage = errorMessage;
        }

        public Boolean Ok { get; private set; }

        public String ErrorMessage { get; private set; }
    }
}
