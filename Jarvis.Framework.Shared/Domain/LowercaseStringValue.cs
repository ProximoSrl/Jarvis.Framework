using System;

namespace Jarvis.Framework.Shared.Domain
{
    [Serializable]
    public abstract class LowercaseStringValue : StringValue
    {
        protected LowercaseStringValue(string value) : base(value)
        {
        }

        protected override string Normalize(string value)
        {
            return value?.ToLowerInvariant();
        }
    }
}