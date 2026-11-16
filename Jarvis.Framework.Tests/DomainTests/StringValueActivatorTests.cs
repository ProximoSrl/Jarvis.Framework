using Jarvis.Framework.Shared.Domain;
using Jarvis.Framework.Shared.Domain.Serialization;
using Jarvis.Framework.Tests.SharedTests.IdentitySupport;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using JsonReader = Newtonsoft.Json.JsonReader;
using JsonToken = Newtonsoft.Json.JsonToken;
using NUnit.Framework;
using System;

namespace Jarvis.Framework.Tests.DomainTests
{
    /// <summary>
    /// Creation of string values in <see cref="StringValueTypeConverter{T}"/>,
    /// <see cref="StringValueBsonSerializer"/> and <see cref="TypedStringValueBsonSerializer{T}"/>:
    /// serializers are called directly, nothing is registered globally in the Mongo driver.
    /// </summary>
    [TestFixture]
    public class StringValueActivatorTests
    {
        public class SampleValue : StringValue
        {
            public SampleValue(string value) : base(value)
            {
            }
        }

        public class DerivedSampleValue : SampleValue
        {
            public DerivedSampleValue(string value) : base(value)
            {
            }
        }

        public class OtherValue : LowercaseStringValue
        {
            public OtherValue(string value) : base(value)
            {
            }
        }

        public abstract class AbstractValue : StringValue
        {
            protected AbstractValue(string value) : base(value)
            {
            }
        }

        public class ConcreteValue : AbstractValue
        {
            public ConcreteValue(string value) : base(value)
            {
            }
        }

        public class PrivateConstructorValue : StringValue
        {
            private PrivateConstructorValue(string value) : base(value)
            {
            }
        }

        public class ThrowingValue : StringValue
        {
            public ThrowingValue(string value) : base(value)
            {
                throw new InvalidOperationException("invalid value " + value);
            }
        }

        /// <summary>
        /// Runs the deserialization with a reader positioned on the value of { v : value }.
        /// </summary>
        private static TResult Deserialize<TResult>(BsonValue value, Type nominalType, Func<BsonDeserializationContext, BsonDeserializationArgs, TResult> deserialize)
        {
            using var reader = new BsonDocumentReader(new BsonDocument("v", value));
            reader.ReadStartDocument();
            reader.ReadBsonType();
            reader.ReadName();
            var context = BsonDeserializationContext.CreateRoot(reader);
            var result = deserialize(context, new BsonDeserializationArgs { NominalType = nominalType });
            reader.ReadEndDocument();
            return result;
        }

        #region TypeConverter

        [Test]
        public void Type_converter_creates_the_typed_value()
        {
            var sut = new StringValueTypeConverter<SampleValue>();

            var first = sut.ConvertFrom(null, null, "Abc");
            var second = sut.ConvertFrom(null, null, "Def"); // second call uses the cached activator.

            Assert.That(first, Is.TypeOf<SampleValue>());
            Assert.That((string)(SampleValue)first, Is.EqualTo("Abc"));
            Assert.That((string)(SampleValue)second, Is.EqualTo("Def"));
        }

        [Test]
        public void Type_converters_of_different_types_do_not_mix_activators()
        {
            var sample = new StringValueTypeConverter<SampleValue>().ConvertFrom(null, null, "Abc");
            var other = new StringValueTypeConverter<OtherValue>().ConvertFrom(null, null, "Abc");

            Assert.That(sample, Is.TypeOf<SampleValue>());
            Assert.That(other, Is.TypeOf<OtherValue>());
            Assert.That((string)(OtherValue)other, Is.EqualTo("abc"));
        }

        [Test]
        public void Type_converter_allocates_only_the_value()
        {
            var sut = new StringValueTypeConverter<SampleValue>();
            object created = null;
            var baseline = IdentityAllocationTests.Measure(() => created = new SampleValue("Abc"));

            var allocated = IdentityAllocationTests.Measure(() => created = sut.ConvertFrom(null, null, "Abc"));

            Assert.That(created, Is.TypeOf<SampleValue>());
            Assert.That(baseline, Is.GreaterThan(0));
            Assert.That(allocated, Is.EqualTo(baseline), "No object[] must be allocated to call the constructor");
        }

        [Test]
        public void Type_converter_can_convert_from_string()
        {
            var sut = new StringValueTypeConverter<SampleValue>();

            Assert.That(sut.CanConvertFrom(typeof(string)), Is.True);
            Assert.That(sut.CanConvertFrom(typeof(int)), Is.False);
        }

        #endregion

        #region StringValueJsonConverter

        /// <summary>
        /// Reader positioned on a fixed token that never allocates, a real json reader allocates
        /// to parse the text.
        /// </summary>
        private sealed class FixedTokenReader : JsonReader
        {
            public FixedTokenReader(JsonToken token, object value)
            {
                SetToken(token, value);
            }

            public override bool Read() => false;
        }

        [Test]
        public void Json_converter_creates_the_requested_type()
        {
            var sut = new StringValueJsonConverter();
            var reader = new FixedTokenReader(JsonToken.String, "Abc");

            var sample = sut.ReadJson(reader, typeof(SampleValue), null, null);
            var other = sut.ReadJson(reader, typeof(OtherValue), null, null);

            Assert.That(sample, Is.TypeOf<SampleValue>());
            Assert.That((string)(SampleValue)sample, Is.EqualTo("Abc"));
            Assert.That(other, Is.TypeOf<OtherValue>());
            Assert.That((string)(OtherValue)other, Is.EqualTo("abc"));
        }

        [Test]
        public void Json_converter_returns_null_when_token_is_not_a_string()
        {
            var sut = new StringValueJsonConverter();

            var result = sut.ReadJson(new FixedTokenReader(JsonToken.Null, null), typeof(SampleValue), null, null);

            Assert.That(result, Is.Null);
        }

        [Test]
        public void Json_converter_allocates_only_the_value()
        {
            var sut = new StringValueJsonConverter();
            var reader = new FixedTokenReader(JsonToken.String, "Abc");
            object created = null;
            var baseline = IdentityAllocationTests.Measure(() => created = new SampleValue("Abc"));

            var allocated = IdentityAllocationTests.Measure(() => created = sut.ReadJson(reader, typeof(SampleValue), null, null));

            Assert.That(created, Is.TypeOf<SampleValue>());
            Assert.That(baseline, Is.GreaterThan(0));
            Assert.That(allocated, Is.EqualTo(baseline), "No object[] must be allocated to call the constructor");
        }

        #endregion

        #region StringValueBsonSerializer

        [Test]
        public void Untyped_serializer_creates_the_nominal_type()
        {
            var sut = new StringValueBsonSerializer(typeof(SampleValue));

            var first = Deserialize(new BsonString("Abc"), typeof(SampleValue), sut.Deserialize);
            var second = Deserialize(new BsonString("Def"), typeof(SampleValue), sut.Deserialize);
            var other = Deserialize(new BsonString("Abc"), typeof(OtherValue), sut.Deserialize);

            Assert.That(first, Is.TypeOf<SampleValue>());
            Assert.That((string)(SampleValue)first, Is.EqualTo("Abc"));
            Assert.That((string)(SampleValue)second, Is.EqualTo("Def"));
            Assert.That(other, Is.TypeOf<OtherValue>());
            Assert.That((string)(OtherValue)other, Is.EqualTo("abc"));
        }

        [Test]
        public void Untyped_serializer_reads_null()
        {
            var sut = new StringValueBsonSerializer(typeof(SampleValue));

            var result = Deserialize(BsonNull.Value, typeof(SampleValue), sut.Deserialize);

            Assert.That(result, Is.Null);
        }

        #endregion

        #region TypedStringValueBsonSerializer

        [Test]
        public void Typed_serializer_creates_the_value()
        {
            var sut = new TypedStringValueBsonSerializer<SampleValue>();

            var first = Deserialize(new BsonString("Abc"), typeof(SampleValue), sut.Deserialize);
            var second = Deserialize(new BsonString("Def"), typeof(SampleValue), sut.Deserialize);

            Assert.That(first, Is.TypeOf<SampleValue>());
            Assert.That((string)first, Is.EqualTo("Abc"));
            Assert.That((string)second, Is.EqualTo("Def"));
            Assert.That(second, Is.Not.SameAs(first));
        }

        [Test]
        public void Typed_serializer_allocates_as_the_untyped_one()
        {
            // Both create the value with a compiled activator, the reader allocates the same in
            // the two measures: reflection based creation would allocate more.
            var typed = new TypedStringValueBsonSerializer<SampleValue>();
            var untyped = new StringValueBsonSerializer(typeof(SampleValue));
            var value = new BsonString("Abc");
            object created = null;

            var untypedAllocated = IdentityAllocationTests.Measure(
                () => created = Deserialize(value, typeof(SampleValue), untyped.Deserialize));
            var typedAllocated = IdentityAllocationTests.Measure(
                () => created = Deserialize(value, typeof(SampleValue), typed.Deserialize));

            Assert.That(created, Is.TypeOf<SampleValue>());
            Assert.That(typedAllocated, Is.EqualTo(untypedAllocated));
        }

        [Test]
        public void Typed_serializer_applies_normalization_of_the_value()
        {
            var sut = new TypedStringValueBsonSerializer<OtherValue>();

            var result = Deserialize(new BsonString("ABC"), typeof(OtherValue), sut.Deserialize);

            Assert.That((string)result, Is.EqualTo("abc"));
        }

        [Test]
        public void Typed_serializer_reads_null()
        {
            var sut = new TypedStringValueBsonSerializer<SampleValue>();

            var result = Deserialize(BsonNull.Value, typeof(SampleValue), sut.Deserialize);

            Assert.That(result, Is.Null);
        }

        [Test]
        public void Typed_serializer_honors_a_derived_nominal_type()
        {
            // nominal type different from T: the activator of T must not be used.
            var sut = new TypedStringValueBsonSerializer<SampleValue>();

            var result = Deserialize(new BsonString("Abc"), typeof(DerivedSampleValue), sut.Deserialize);

            Assert.That(result, Is.TypeOf<DerivedSampleValue>());
            Assert.That((string)result, Is.EqualTo("Abc"));
        }

        [Test]
        public void Typed_serializer_of_abstract_type_creates_the_concrete_nominal_type()
        {
            var sut = new TypedStringValueBsonSerializer<AbstractValue>();

            var result = Deserialize(new BsonString("Abc"), typeof(ConcreteValue), sut.Deserialize);

            Assert.That(result, Is.TypeOf<ConcreteValue>());
        }

        [Test]
        public void Typed_serializer_of_abstract_type_fails_when_nominal_type_is_abstract()
        {
            var sut = new TypedStringValueBsonSerializer<AbstractValue>();

            Assert.Throws<MissingMethodException>(
                () => Deserialize(new BsonString("Abc"), typeof(AbstractValue), sut.Deserialize));
        }

        [Test]
        public void Typed_serializer_requires_a_public_constructor()
        {
            // As before the typed activator: Activator.CreateInstance does not use private constructors.
            var sut = new TypedStringValueBsonSerializer<PrivateConstructorValue>();

            Assert.Throws<MissingMethodException>(
                () => Deserialize(new BsonString("Abc"), typeof(PrivateConstructorValue), sut.Deserialize));
        }

        [Test]
        public void Typed_serializer_does_not_wrap_constructor_exception()
        {
            var sut = new TypedStringValueBsonSerializer<ThrowingValue>();

            var ex = Assert.Throws<InvalidOperationException>(
                () => Deserialize(new BsonString("Abc"), typeof(ThrowingValue), sut.Deserialize));

            Assert.That(ex.Message, Is.EqualTo("invalid value Abc"));
        }

        #endregion
    }
}
