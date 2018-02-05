using System;

namespace Jarvis.Framework.Shared.Domain
{
    [Serializable]
#pragma warning disable S4035 // Classes implementing "IEquatable<T>" should be sealed
	public abstract class StringValue : IEquatable<StringValue>
#pragma warning restore S4035 // Classes implementing "IEquatable<T>" should be sealed
	{
        string _value;

        public bool Equals(StringValue other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;

            // NB!
            if (other.GetType() != this.GetType()) return false;

            return string.Equals(Value, other.Value);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((StringValue)obj);
        }

        public override int GetHashCode()
        {
            return (Value != null ? Value.GetHashCode() : 0);
        }

        public static bool operator ==(StringValue left, StringValue right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(StringValue left, StringValue right)
        {
            return !Equals(left, right);
        }

        protected string Value
        {
            get { return _value; }
            private set { _value = Normalize(value); }
        }

        protected virtual string Normalize(string value)
        {
            return value;
        }

        public static implicit operator string(StringValue id)
        {
            return id?.Value;
        }

        protected StringValue(string value)
        {
            Value = value;
        }

        public bool IsValid()
        {
            return !String.IsNullOrWhiteSpace(this._value);
        }

        public override string ToString()
        {
            return _value;
        }
    }
}
