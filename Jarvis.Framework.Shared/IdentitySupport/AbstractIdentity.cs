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
        /// <summary>
        /// Constructor for inheritors that assign the id themselves, only classes of this
        /// assembly can do it (<see cref="EventStoreIdentity"/>).
        /// </summary>
        private protected AbstractIdentity()
        {
        }

        /// <summary>
        /// Creates the identity with its value, it is the only way an identity defined
        /// outside this assembly can assign the id.
        /// </summary>
        protected AbstractIdentity(TKey id)
        {
            Id = id;
        }

        /// <summary>
        /// Value of the identity. An identity is immutable: inheritors outside this assembly
        /// pass the value to the constructor and cannot change it afterwards. Inside this assembly
        /// an inheritor that caches values derived from the id (<see cref="EventStoreIdentity"/>)
        /// is responsible for refreshing them when it assigns the id.
        /// </summary>
        [BsonIgnore]
        [JsonIgnore]
        public TKey Id { get; private protected set; }

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

        /// <summary>
        /// Reached by <see cref="Equals(object)"/>, == and != (so by Dictionary / HashSet keys
        /// too): compares id and tag. Virtual so an inheritor whose tag depends only on the
        /// concrete type can compare type and id without asking the tag (<see cref="EventStoreIdentity"/>).
        /// </summary>
        public virtual bool Equals(AbstractIdentity<TKey> other)
        {
            if (other != null)
            {
                // EqualityComparer avoids boxing the id, Id.Equals(Id) on a generic type
                // resolves to Object.Equals(object) and allocates for value types.
                return EqualityComparer<TKey>.Default.Equals(other.Id, Id) && other.GetTag() == GetTag();
            }

            return false;
        }

        /// <summary>
        /// Two identities are equal when they have the same concrete type and the same string
        /// representation: an inheritor can override GetTag() / AsString() using instance state,
        /// so two instances with the same id can have different representations. An inheritor
        /// whose representation depends only on type and id can override this method with a
        /// faster comparison (<see cref="EventStoreIdentity"/>).
        /// </summary>
        public virtual bool Equals(IIdentity other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return this.GetType() == other.GetType() && this.AsString() == other.AsString();
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