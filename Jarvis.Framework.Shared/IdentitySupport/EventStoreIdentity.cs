using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;

namespace Jarvis.Framework.Shared.IdentitySupport
{
    public abstract class EventStoreIdentity : AbstractIdentity<long>
    {
        private static readonly ConcurrentDictionary<Type, String> classTags;
        private static readonly ConcurrentDictionary<string, string> tagToCorrectCaseMap;

        static EventStoreIdentity()
        {
            classTags = new ConcurrentDictionary<Type, string>();
            tagToCorrectCaseMap = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
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
            var span = id.AsSpan();

            //at least I want the prefix to be followed by a dash and at least a digit
            if (span.Length < idTag.Length + 2)
                return false;

            var tagSpan = span[..idTag.Length];

            //it must start with id tag (case-insensitive)
            if (!tagSpan.Equals(idTag.AsSpan(), StringComparison.OrdinalIgnoreCase))
                return false;

            //Check for separator after the prefix
            if (span[idTag.Length] != Separator)
                return false;

            //all subsequent chars should be digits.
            var numberSpan = span[(idTag.Length + 1)..];
            foreach (var ch in numberSpan)
            {
                if (!char.IsDigit(ch))
                    return false;
            }

            return true;
        }

        public static string GetTagForIdentityClass(Type identityType)
        {
            if (!classTags.TryGetValue(identityType, out string tag))
            {
                var tn = identityType.Name;
                if (!tn.EndsWith("Id"))
                {
                    throw new JarvisFrameworkIdentityException(string.Format("Wrong Identity class name: {0} Class name should end with Id", tn));
                }
                tag = tn[..^2];
                classTags.TryAdd(identityType, tag);
                tagToCorrectCaseMap.TryAdd(tag, tag);
            }
            return tag;
        }

        public override string GetTag()
        {
            return GetTagForIdentityClass(GetType());
        }

        protected EventStoreIdentity(long id)
        {
            if (id < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(id), "Id must be positive");
            }
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
            var span = identityAsString.AsSpan();
            var separatorIndex = span.IndexOf(Separator);

            if (separatorIndex == 0)
            {
                //identity starts with underscore .... error
                throw new JarvisFrameworkIdentityException(string.Format("Wrong Identity format: {0}", identityAsString));
            }

            // now proceed with standard parsing.
            ReadOnlySpan<char> tagSpan;
            ReadOnlySpan<char> idSpan;

            if (separatorIndex == -1)
            {
                //this is a special case, we can have a simple number as id, it must be a supported scenario.
                if (!long.TryParse(span, out var numericId))
                {
                    throw new JarvisFrameworkIdentityException(string.Format("invalid identity value {0}", identityAsString));
                }
                if (numericId < 0)
                {
                    throw new JarvisFrameworkIdentityException(string.Format("invalid identity value {0}, id must be positive", identityAsString));
                }
                Id = numericId;
                return;
            }

            //this is normal situation, we found the separator and a tag
            tagSpan = span[..separatorIndex];

            var thisTag = GetTag();
            if (!tagSpan.Equals(thisTag, StringComparison.OrdinalIgnoreCase))
            {
                throw new JarvisFrameworkIdentityException(string.Format("Invalid assigment. {0} tag is not valid for type {1} - Tag expected: {2}", tagSpan.ToString(), GetType().FullName, thisTag));
            }

            //ok tag prefix is valid.
            //Need to parse numeric part, must be a valid long positive.
            idSpan = span[(separatorIndex + 1)..];
            if (long.TryParse(idSpan, out var id))
            {
                if (id < 0)
                {
                    throw new JarvisFrameworkIdentityException(string.Format("invalid identity value {0}, id must be positive", identityAsString));
                }
                //this is the standard happy path to create identity.
                // Need to convert span to string only for the tag comparison
                Id = id;
                return;
            }

            //if we reach here the id is not valid
            throw new JarvisFrameworkIdentityException(string.Format("invalid identity value {0}", identityAsString));
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

        public static string Normalize(string id)
        {
            if (id == null || id.Length == 0) return null;

            var span = id.AsSpan();
            var separatorIndex = span.IndexOf(Separator);

            if (separatorIndex < 0)
            {
                // Numeric-only identity null is not a valid id
                return null;
            }

            var tagSpan = span[..separatorIndex];
            var spanLookup = tagToCorrectCaseMap.GetAlternateLookup<ReadOnlySpan<char>>();
            if (spanLookup.TryGetValue(tagSpan, out var correctCaseTag))
            {
                // If casing is already correct, return the same instance
                if (tagSpan.Equals(correctCaseTag, StringComparison.Ordinal))
                {
                    // happy path, casing is correct
                    return id;
                }

                // Fix only the prefix casing, keep separator + numeric part unchanged we need to allocate a new
                // string but this is necessaery.
                return correctCaseTag + id.Substring(separatorIndex);
            }
          
            // No matching type found, return null to indicate unknown identity type
            return null;
        }
    }
}