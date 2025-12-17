using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;

namespace Jarvis.Framework.Shared.IdentitySupport
{
    public abstract class EventStoreIdentity : AbstractIdentity<long>
    {
        private static readonly ConcurrentDictionary<Type, String> classTags;

        static EventStoreIdentity()
        {
            classTags = new ConcurrentDictionary<Type, string>();
        }

        public static string GetTagForIdentityClass<T>() where T : EventStoreIdentity
        {
            return GetTagForIdentityClass(typeof(T));
        }

        public static bool Match<T>(string id) where T : EventStoreIdentity
        {
            if (String.IsNullOrEmpty(id))
                return false;

            string idTag = GetTagForIdentityClass<T>();

            //at least I want the prefix to be followed by a dash and at least a digit
            if (id.Length < idTag.Length + 2)
                return false;

            //it must start with id tag
            if (!id.StartsWith(idTag, StringComparison.OrdinalIgnoreCase))
                return false;

            //Check for separator after the prefix
            if (id[idTag.Length] != Separator)
                return false;

            //all subsequent chars should be digits.
            for (int i = idTag.Length + 1; i < id.Length; i++)
            {
                if (!char.IsDigit(id[i]))
                    return false;
            }

            return true;
        }

        public static string GetTagForIdentityClass(Type identityType)
        {
            String tag;
            if (!classTags.TryGetValue(identityType, out tag))
            {
                var tn = identityType.Name;
                if (!tn.EndsWith("Id"))
                    throw new JarvisFrameworkIdentityException(string.Format("Wrong Identity class name: {0} Class name should end with Id", tn));
                tag = tn.Remove(tn.Length - 2);
                classTags.TryAdd(identityType, tag);
            }
            return tag;
        }

        public override string GetTag()
        {
            return GetTagForIdentityClass(GetType());
        }

        protected EventStoreIdentity(long id)
        {
            this.Id = id;
        }

        protected EventStoreIdentity(string id)
        {
            if (String.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id));
            }
            Assign(id);
        }

        /// <summary>
        /// Assign the identity to the object, actually this method performs an extended validation
        /// of the string passed as parameter, rejecting anything that has not a correct format for this
        /// specific identity.
        /// </summary>
        /// <param name="identityAsString"></param>
        /// <exception cref="JarvisFrameworkIdentityException"></exception>
        protected void Assign(string identityAsString)
        {
            int pos = identityAsString.IndexOf(Separator);

            if (pos == 0)
            {
                //identity starts with underscore .... error
                throw new JarvisFrameworkIdentityException(string.Format("Wrong Identity format: {0}", identityAsString));
            }

            // now proceed with standard parsing.
            string tag = null;
            string idString = null;
            if (pos == -1)
            {
                //this is a special case, we can have a simple number as id, not pretty much sure if that should be supported
                tag = GetTag();
                idString = identityAsString;
            }
            else if (pos > 0)
            {
                //this is normal situation, we found the separator and a tag
                tag = identityAsString.Substring(0, pos);
                idString = identityAsString.Substring(pos + 1);
            }


            //Need to parse numeric part, must be a valid long positive.
            if (ulong.TryParse(idString, out var id))
            {
                //this is the standard happy path to create identity.
                Assign(tag, id);
                return;
            }

            //if we reach here the id starts with underscore or is not valid
            throw new JarvisFrameworkIdentityException(string.Format("invalid identity value {0}", identityAsString));
        }

        protected virtual void Assign(string tag, ulong value)
        {
            var thisTag = GetTag();
            if (!tag.Equals(thisTag, StringComparison.OrdinalIgnoreCase))
            {
                throw new JarvisFrameworkIdentityException(string.Format("Invalid assigment. {0} tag is not valid for type {1} - Tag expected: {2}", tag, GetType().FullName, thisTag));
            }
            Id = (long) value;
        }

        public static string Format(Type type, long value)
        {
            return Format(GetTagForIdentityClass(type), value);
        }

        public static string Format(string tag, long value)
        {
            return string.Format("{0}{1}{2}", tag, Separator, value);
        }

        public static implicit operator string(EventStoreIdentity id)
        {
            return id?.ToString();
        }

        public override string ToString()
        {
            return Format(GetTag(), Id);
        }

        [BsonId]
        [JsonProperty("Id")]
        public virtual string FullyQualifiedId
        {
            get { return ToString(); }
            set { Assign(value); }
        }

        public const char Separator = '_';

        public override string AsString()
        {
            return FullyQualifiedId;
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }
    }
}