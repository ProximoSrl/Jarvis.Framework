using Jarvis.Framework.Shared.Domain;
using Jarvis.Framework.Shared.Domain.Serialization;

namespace Jarvis.Framework.Tests.DomainTests
{
    [MongoDB.Bson.Serialization.Attributes.BsonSerializer(typeof(StringValueBsonSerializer))]
    public class TypedStringValue : LowercaseStringValue
    {
        public TypedStringValue(string value) : base(value)
        {
        }
    }
}