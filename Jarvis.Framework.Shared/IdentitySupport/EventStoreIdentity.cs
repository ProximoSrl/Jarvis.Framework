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
                // no tag in the input, the string representation must be rebuilt on demand.
                _fullyQualifiedId = null;
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
                Id = id;

                // The cached string must always follow the id: if the input is already in canonical
                // form we reuse it as the cached representation, so no new string is ever built,
                // otherwise the cache is cleared and the string is rebuilt on demand.
                _fullyQualifiedId = IsCanonical(tagSpan, thisTag, idSpan) ? identityAsString : null;
                return;
            }

            //if we reach here the id is not valid
            throw new JarvisFrameworkIdentityException(string.Format("invalid identity value {0}", identityAsString));
        }

        /// <summary>
        /// True if the input is exactly what <see cref="Format(string, long)"/> would produce:
        /// tag with the same casing, only ascii digits and no leading zero (unless the value is "0").
        /// </summary>
        private static bool IsCanonical(ReadOnlySpan<char> tagSpan, string thisTag, ReadOnlySpan<char> idSpan)
        {
            if (!tagSpan.Equals(thisTag, StringComparison.Ordinal))
                return false;

            if (idSpan.Length == 0 || (idSpan[0] == '0' && idSpan.Length > 1))
                return false;

            foreach (var ch in idSpan)
            {
                if (!char.IsAsciiDigit(ch))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Used by <see cref="IdentityManager"/> after it created this identity from
        /// <paramref name="identityAsString"/> with the numeric constructor: if the string is
        /// canonical it becomes the cached representation, so the identity does not rebuild an
        /// identical string on the first ToString(). A non canonical string (different tag casing,
        /// leading zeros, sign, spaces) is ignored. The caller guarantees that the string was
        /// parsed to the tag and the id of this instance.
        /// </summary>
        /// <param name="identityAsString">The string the identity was parsed from.</param>
        /// <param name="separatorIndex">Index of <see cref="Separator"/> in the string.</param>
        internal void SeedStringRepresentation(string identityAsString, int separatorIndex)
        {
            var span = identityAsString.AsSpan();
            if (IsCanonical(span[..separatorIndex], GetTag(), span[(separatorIndex + 1)..]))
            {
                _fullyQualifiedId = identityAsString;
            }
        }

        public static string Format(Type type, long value)
        {
            return Format(GetTagForIdentityClass(type), value);
        }

        public static string Format(string tag, long value)
        {
            // Interpolation formats the long directly in the handler buffer: no boxing of
            // separator and value and no format string parsing, only the result string is allocated.
            return $"{tag}{Separator}{value}";
        }

        public static implicit operator string(EventStoreIdentity id)
        {
            return id?.ToString();
        }

        /// <summary>
        /// Cached full string representation (Tag_123). The id is assigned by the constructors,
        /// <see cref="Assign(string)"/> updates this field explicitly together with the id.
        /// No locking: two threads can compute the string at the same time, but the result is
        /// identical and reference assignment is atomic, so the worst case is a duplicate string.
        /// </summary>
        private string _fullyQualifiedId;

        public override string ToString()
        {
            return _fullyQualifiedId ??= Format(GetTag(), Id);
        }

        /// <summary>
        /// Full string representation of the identity (Tag_123). There is no setter because an
        /// identity is immutable: serialization must go through the flat identity serializers
        /// (<see cref="MongoFlatMapper.EnableFlatMapping(bool)"/>), that build the identity with
        /// its constructor, a serializer cannot populate an existing instance.
        /// </summary>
        [BsonId]
        [JsonProperty("Id")]
        public virtual string FullyQualifiedId
        {
            get { return ToString(); }
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

        /// <summary>
        /// Extracts the tag from an identity in string form (ex: Document_42) without allocating:
        /// the string returned in <paramref name="tag"/> is the unique cached instance of the tag
        /// with the correct casing (the same returned by <see cref="GetTagForIdentityClass(Type)"/>),
        /// whatever the casing used in <paramref name="id"/>.
        /// </summary>
        /// <param name="id">Full identity, tag + separator + numeric part. A string can be
        /// passed directly, null or empty values simply return false.</param>
        /// <param name="tag">Cached tag instance, null if the method returns false.</param>
        /// <returns>True if the id has the form tag_digits and the tag belongs to a known
        /// identity type. A type is known after it was registered in the IdentityManager or
        /// after its tag was requested at least once. The numeric part is checked only to be
        /// composed by digits, overflow is not verified.</returns>
        public static bool TryGetTag(ReadOnlySpan<char> id, out string tag)
        {
            tag = null;

            var separatorIndex = id.IndexOf(Separator);
            if (separatorIndex < 1 || separatorIndex == id.Length - 1)
            {
                // no separator, no tag or no numeric part
                return false;
            }

            foreach (var ch in id[(separatorIndex + 1)..])
            {
                if (!char.IsAsciiDigit(ch))
                    return false;
            }

            var spanLookup = tagToCorrectCaseMap.GetAlternateLookup<ReadOnlySpan<char>>();
            return spanLookup.TryGetValue(id[..separatorIndex], out tag);
        }
    }
}