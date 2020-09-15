namespace Jarvis.Framework.Tests.DomainTests
{
    [TestFixture]
    [Category("mongo_serialization")]
    public class StringValueMongoTests
    {
        [Test]
        public void should_serialize_lowercase()
        {
            var instance = new ClassWithTypedStringValueLowerCase { Value = new TypedStringValueLowerCase("abc_123") };
            var json = instance.ToJson();

            Assert.AreEqual("{ \"Value\" : \"abc_123\" }", json);
        }

        [Test]
        public void should_serialize_lowercase_null()
        {
            var instance = new ClassWithTypedStringValueLowerCase { Value = null };
            var json = instance.ToJson();

            Assert.AreEqual("{ \"Value\" : null }", json);
        }

        [Test]
        public void should_serialize_lowercase_null_value()
        {
            var instance = new ClassWithTypedStringValueLowerCase { Value = new TypedStringValueLowerCase(null) };
            var json = instance.ToJson();

            Assert.AreEqual("{ \"Value\" : null }", json);
        }

        [Test]
        public void should_serialize()
        {
            var instance = new ClassWithTypedStringValue { Value = new TypedStringValue("aBC_123") };
            var json = instance.ToJson();

            Assert.AreEqual("{ \"Value\" : \"aBC_123\" }", json);
        }

        [Test]
        public void should_deserialize_lowercase()
        {
            var instance = BsonSerializer.Deserialize<ClassWithTypedStringValueLowerCase>("{ Value:\"abc_123\"}");
            Assert.AreEqual("abc_123", (string)instance.Value);
        }

        [Test]
        public void should_deserialize()
        {
            var instance = BsonSerializer.Deserialize<ClassWithTypedStringValue>("{ Value:\"ABC_123\"}");
            Assert.AreEqual("ABC_123", (string)instance.Value);
        }

        [Test]
        public void should_serialize_null()
        {
            var instance = new ClassWithTypedStringValueLowerCase();
            var json = instance.ToJson();
            Assert.AreEqual("{ \"Value\" : null }", json);
        }

        [Test]
        public void should_deserialize_null()
        {
            var instance = BsonSerializer.Deserialize<ClassWithTypedStringValueLowerCase>("{ Value: null}");
            Assert.IsNull(instance.Value);
        }

        [Test]
        public void can_convert_to_bson_value_lowercase()
        {
            StringValueCustomBsonTypeMapper.Register<TypedStringValueLowerCase>();
            var val = BsonValue.Create(new TypedStringValueLowerCase("abc"));
            Assert.IsInstanceOf<BsonString>(val);
        }

        [Test]
        public void can_convert_to_bson_value()
        {
            StringValueCustomBsonTypeMapper.Register<TypedStringValue>();
            var val = BsonValue.Create(new TypedStringValue("ABC"));
            Assert.IsInstanceOf<BsonString>(val);
        }
    }

    ////WE cannot run this test because registration of class map is global and creates problem for other tests
    //[TestFixture]
    //[Explicit]
    //public class StringValueMongoTestsFlatMapper
    //{
    //    [OneTimeSetUp]
    //    public void TestFixtureSetUp()
    //    {
    //        MongoFlatMapper.EnableFlatMapping(true);
    //    }

    //    [Test]
    //    public void should_serialize_lowercase()
    //    {
    //        var instance = new ClassWithTypedStringValueLowerCaseWithoutAttribute { Value = new TypedStringValueWithoutAttributeLowerCase("abc_123") };
    //        var json = instance.ToJson();

    //        Assert.AreEqual("{ \"Value\" : \"abc_123\" }", json);
    //    }

    //    [Test]
    //    public void should_serialize()
    //    {
    //        var instance = new ClassWithTypedStringValueWithoutAttribute { Value = new TypedStringValueWithoutAttribute("aBC_123") };
    //        var json = instance.ToJson();

    //        Assert.AreEqual("{ \"Value\" : \"aBC_123\" }", json);
    //    }

    //    [Test]
    //    public void should_deserialize_lowercase()
    //    {
    //        var instance = BsonSerializer.Deserialize<ClassWithTypedStringValueLowerCaseWithoutAttribute>("{ Value:\"abc_123\"}");
    //        Assert.AreEqual("abc_123", (string)instance.Value);
    //    }

    //    [Test]
    //    public void should_deserialize()
    //    {
    //        var instance = BsonSerializer.Deserialize<ClassWithTypedStringValueWithoutAttribute>("{ Value:\"ABC_123\"}");
    //        Assert.AreEqual("ABC_123", (string)instance.Value);
    //    }

    //    [Test]
    //    public void should_serialize_null()
    //    {
    //        var instance = new ClassWithTypedStringValueLowerCaseWithoutAttribute();
    //        var json = instance.ToJson();
    //        Assert.AreEqual("{ \"Value\" : null }", json);
    //    }

    //    [Test]
    //    public void should_deserialize_null()
    //    {
    //        var instance = BsonSerializer.Deserialize<ClassWithTypedStringValueLowerCaseWithoutAttribute>("{ Value: null}");
    //        Assert.IsNull(instance.Value);
    //    }
    //}
}
