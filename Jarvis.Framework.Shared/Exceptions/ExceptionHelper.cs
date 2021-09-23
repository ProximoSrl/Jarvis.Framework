﻿using System;
using System.Text;

namespace Jarvis.Framework.Shared.Exceptions
{
    public static class ExceptionHelper
    {
        /// <summary>
        /// Useful for AggregateException where we need to dump the inner exception.
        /// </summary>
        /// <param name="ex"></param>
        /// <returns></returns>
        public static String GetExceptionDescription(this Exception ex)
        {
            if (ex == null)
                return "";

            if (ex is AggregateException aggregateException)
            {
                var flattened = (aggregateException).Flatten();
                StringBuilder sb = new StringBuilder();
                sb.Append($"Aggregate Exception: {flattened.Message}");
                foreach (var exception in flattened.InnerExceptions)
                {
                    sb.AppendLine(exception.ToString());
                }
                return sb.ToString();
            }
            return ex.ToString();
        }
    }
}
