using System;

namespace Jarvis.Framework.Shared.Claims
{
    public class Claim : IEquatable<Claim>
    {
        public bool Equals(Claim other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return string.Equals(Id, other.Id) && string.Equals(Value, other.Value);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((Claim)obj);
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }

        public static bool operator ==(Claim left, Claim right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(Claim left, Claim right)
        {
            return !Equals(left, right);
        }

        public string Id { get; private set; }
        public string Value { get; private set; }

        public Claim(string id, string value = "True")
        {
            if (string.IsNullOrWhiteSpace(id)) 
                throw new ArgumentNullException("id");

            Id = id;
            Value = value;
        }

        static public Claim For(string id, string value)
        {
            return new Claim(id, value);
        }
    }
}
