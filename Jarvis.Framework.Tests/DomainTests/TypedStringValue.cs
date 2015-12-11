using Jarvis.Framework.Shared.Domain;
using Jarvis.Framework.Shared.Domain.Serialization;

namespace Jarvis.Framework.Tests.DomainTests
{
    [MongoDB.Bson.Serialization.Attributes.BsonSerializer(typeof(StringValueBsonSerializer))]
    public class TypedStringValueLowerCase : LowercaseStringValue
    {
        public TypedStringValueLowerCase(string value) : base(value)
        {
        }
    }

    [MongoDB.Bson.Serialization.Attributes.BsonSerializer(typeof(StringValueBsonSerializer))]
    public class TypedStringValue : StringValue
    {
        public TypedStringValue(string value) : base(value)
        {
        }
    }

}