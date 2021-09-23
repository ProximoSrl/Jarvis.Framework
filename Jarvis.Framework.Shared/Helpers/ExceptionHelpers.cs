using System;

namespace Jarvis.Framework.Shared.Helpers
{
    public static class ExceptionHelpers
    {
        public static Exception ExtractException(this Exception ex)
        {
            if (ex is AggregateException)
            {
                var aex = ((AggregateException)ex).Flatten();
                return aex.InnerException;
            }
            return ex;
        }
    }
}
