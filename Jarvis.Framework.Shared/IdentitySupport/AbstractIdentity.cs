using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace Jarvis.Framework.Shared.IdentitySupport
{
    /// <summary>
    /// Base implementation of <see cref="IIdentity"/>, which implements
    /// equality and ToString once and for all.
    /// </summary>
    /// <typeparam name="TKey">The type of the key.</typeparam>
    [Serializable]
    public abstract class AbstractIdentity<TKey> : IIdentity
    {
        [BsonIgnore]
        [JsonIgnore]
        public TKey Id { get; set; }

        public virtual string AsString()
        {
            return Id.ToString();
        }

        public abstract string GetTag();

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;

            var identity = obj as AbstractIdentity<TKey>;

            if (identity != null)
            {
                return Equals(identity);
            }

            return false;
        }

        public override string ToString()
        {
            return string.Format("{0}-{1}", GetType().Name.Replace("Id", ""), Id);
        }

        public override int GetHashCode()
        {
            return (Id.GetHashCode());
        }

        public int GetConsistentHashCode()
        {
            // same as hash code, but works across multiple architectures 
            var type = typeof(TKey);
            if (type == typeof(string))
            {
                return CalculateStringHash(Id.ToString());
            }
            return Id.GetHashCode();
        }

        static AbstractIdentity()
        {
            var type = typeof(TKey);
            if (type == typeof(int) || type == typeof(long) || type == typeof(uint) || type == typeof(ulong))
                return;
            if (type == typeof(Guid) || type == typeof(string))
                return;
            throw new InvalidOperationException("Abstract identity inheritors must provide stable hash. It is not supported for:  " + type);
        }

        private static int CalculateStringHash(string value)
        {
            if (value == null) return 42;
            unchecked
            {
                var hash = 23;
                foreach (var c in value)
                {
                    hash = (hash * 31) + c;
                }
                return hash;
            }
        }

        public bool Equals(AbstractIdentity<TKey> other)
        {
            if (other != null)
            {
                // EqualityComparer avoids boxing the id, Id.Equals(Id) on a generic type
                // resolves to Object.Equals(object) and allocates for value types.
                return EqualityComparer<TKey>.Default.Equals(other.Id, Id) && other.GetTag() == GetTag();
            }

            return false;
        }

        public bool Equals(IIdentity other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;

            // Same concrete type means same tag, it is enough to compare the id without
            // generating the string representation of both identities.
            return this.GetType() == other.GetType()
                && other is AbstractIdentity<TKey> typed
                && EqualityComparer<TKey>.Default.Equals(Id, typed.Id);
        }

        public static bool operator ==(AbstractIdentity<TKey> left, AbstractIdentity<TKey> right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(AbstractIdentity<TKey> left, AbstractIdentity<TKey> right)
        {
            return !Equals(left, right);
        }
    }
}