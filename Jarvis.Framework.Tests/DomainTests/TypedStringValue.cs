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

    public class TypedStringValueWithoutAttribute : StringValue
    {
        public TypedStringValueWithoutAttribute(string value) : base(value)
        {
        }
    }

    public class TypedStringValueWithoutAttributeLowerCase : StringValue
    {
        public TypedStringValueWithoutAttributeLowerCase(string value) : base(value)
        {
        }
    }

}