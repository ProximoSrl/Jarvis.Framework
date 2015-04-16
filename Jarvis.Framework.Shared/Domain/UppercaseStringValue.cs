namespace Jarvis.Framework.Shared.Domain
{
    public abstract class UppercaseStringValue : StringValue
    {
        protected UppercaseStringValue(string value)
            : base(value)
        {
        }

        protected override string Normalize(string value)
        {
            return value == null ? null : value.ToUpperInvariant();
        }
    }
}