namespace Jarvis.Framework.Tests.DomainTests
{
    [BsonSerializer(typeof(TypedStringValueBsonSerializer<TypedStringValueLowerCase>))]
    public class TypedStringValueLowerCase : LowercaseStringValue
    {
        public TypedStringValueLowerCase(string value) : base(value)
        {
        }
    }

    [BsonSerializer(typeof(TypedStringValueBsonSerializer<TypedStringValue>))]
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