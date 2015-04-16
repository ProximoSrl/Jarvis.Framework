using System;
using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;

namespace Jarvis.Framework.Shared.IdentitySupport
{
    public abstract class EventStoreIdentity : AbstractIdentity<long>
    {
        public static string GetTagForIdentityClass<T>() where T : EventStoreIdentity
        {
            return GetTagForIdentityClass(typeof(T));
        }

        public static bool Match<T>(string id) where T : EventStoreIdentity
        {
            return id.StartsWith(GetTagForIdentityClass<T>() + Separator);
        }

        public static string GetTagForIdentityClass(Type identityType)
        {
            var tn = identityType.Name;
            if (!tn.EndsWith("Id"))
                throw new Exception(string.Format("Wrong Identity class name: {0} Class name should end with Id", tn));

            return tn.Remove(tn.Length - 2);
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
            Assign(id);
        }

        protected virtual void Assign(string identityAsString)
        {
            int pos = identityAsString.IndexOf(Separator);
            if (pos == -1)
            {
                long parsed;
                if (long.TryParse(identityAsString, out parsed))
                {
                    this.Id = parsed;
                    return;
                }

                throw new Exception(string.Format("invalid identity value {0}", identityAsString));
            }
            var tag = identityAsString.Substring(0, pos);

            if (tag != GetTag())
                throw new Exception(string.Format("Invalid assigment. {0} is not of type {1}", identityAsString, GetType().FullName));

            var id = identityAsString.Substring(pos + 1);
            this.Id = long.Parse(id);
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
            return id != null ? id.ToString() : null;
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
    }
}