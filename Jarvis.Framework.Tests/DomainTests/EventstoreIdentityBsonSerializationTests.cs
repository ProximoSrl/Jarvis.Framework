namespace Jarvis.Framework.Tests.DomainTests
{
    [TestFixture]
    [Category("mongo_serialization")]
    public class DomainEventIdentityBsonSerializationTests
    {
        [BsonSerializer(typeof(TypedEventStoreIdentityBsonSerializer<SampleId>))]
        public class SampleId : EventStoreIdentity
        {
            public SampleId(long id)
                : base(id)
            {
            }

            public SampleId(string id)
                : base(id)
            {
            }
        }

        public class SampleEvent : DomainEvent
        {
            public string Value { get; private set; }

            public SampleEvent(string value)
            {
                MessageId = Guid.Parse("cfbb68da-b598-417c-84c7-e951e8a36b8e");
                Value = value;
            }
        }

        [SetUp]
        public void SetUp()
        {
            var identityConverter = new IdentityManager(new InMemoryCounterService());
            MongoFlatIdSerializerHelper.IdentityConverter = identityConverter;
            identityConverter.RegisterIdentitiesFromAssembly(typeof(SampleId).Assembly);

            BsonClassMap.RegisterClassMap<DomainEvent>(map =>
            {
                map.AutoMap();
                //map.MapProperty(x => x.AggregateId).SetSerializer(new TypedEventStoreIdentityBsonSerializer<EventStoreIdentity>());
            });
        }

        [TearDown]
        public void TearDown()
        {
            BsonClassMapHelper.Unregister<DomainEvent>();

            // class map cleanup???
            MongoFlatIdSerializerHelper.IdentityConverter = null;
        }

        [Test]
        public void should_serialize_event()
        {
            var e = new SampleEvent("this is a test").AssignIdForTest(new SampleId(1));
            var json = e.ToJson();
            Debug.WriteLine(json);

            Assert.AreEqual(
                "{ \"MessageId\" : \"cfbb68da-b598-417c-84c7-e951e8a36b8e\", \"AggregateId\" : \"Sample_1\", \"Value\" : \"this is a test\" }",
                json
            );
        }

        [Test]
        public void should_deserialize_event()
        {
            var json = "{ \"MessageId\" : \"cfbb68da-b598-417c-84c7-e951e8a36b8e\", \"AggregateId\" : \"Sample_1\", \"Value\" : \"this is a test\" }";
            var e = BsonSerializer.Deserialize<SampleEvent>(json);

            Assert.AreEqual(new SampleId("Sample_1"), e.AggregateId);
        }
    }

    [TestFixture]
    [Category("mongo_serialization")]
    public class EventstoreIdentityBsonSerializationTests
    {
        [BsonSerializer(typeof(TypedEventStoreIdentityBsonSerializer<SampleId>))]
        public class SampleId : EventStoreIdentity
        {
            public SampleId(long id)
                : base(id)
            {
            }

            public SampleId(string id)
                : base(id)
            {
            }
        }

        public class SampleNoAttributeId : EventStoreIdentity
        {
            public SampleNoAttributeId(long id)
                : base(id)
            {
            }

            public SampleNoAttributeId(string id)
                : base(id)
            {
            }
        }

        public class ClassWithSampleIdentity
        {
            public SampleId Value { get; set; }
        }

        public class ClassWithIdentityBaseClass
        {
            public IIdentity Value { get; set; }
        }

        public class ClassWithIdentityBaseClassAndAttribute
        {
            [BsonSerializer(typeof(GenericIdentityBsonSerializer))]
            public IIdentity Value { get; set; }
        }

        public class ClassWithEventStoreIdentityBaseClassAndAttribute
        {
            [BsonSerializer(typeof(TypedEventStoreIdentityBsonSerializer<EventStoreIdentity>))]
            public EventStoreIdentity Value { get; set; }
        }

        public class StateWithIdentity : JarvisAggregateState
        {
            public SampleId Value { get; set; }
        }

        public class ClassWithArrayIdentity
        {
            [BsonSerializer(typeof(IdentityArrayBsonSerializer<SampleId>))]
            public SampleId[] Value { get; set; }
        }

        public class ClassWithDictionaryOfObject
        {
            [BsonSerializer(typeof(Shared.IdentitySupport.Serialization.DictionaryAsObjectJarvisSerializer<Dictionary<String, Object>, String, Object>))]
            public Dictionary<String, Object> Value { get; set; }
        }


        [OneTimeSetUp]
        public void SetUp()
        {
            var identityConverter = new IdentityManager(new InMemoryCounterService());
            MongoFlatIdSerializerHelper.IdentityConverter = identityConverter;

            identityConverter.RegisterIdentitiesFromAssembly(typeof(SampleId).Assembly);

            BsonSerializer.RegisterSerializationProvider(new EventStoreIdentitySerializationProvider());
            BsonSerializer.RegisterSerializationProvider(new StringValueSerializationProvider());
        }

        [OneTimeTearDown]
        public void TestFixtureTearDown()
        {
            MongoFlatIdSerializerHelper.IdentityConverter = null;
        }

        [Test]
        public void should_serialize()
        {
            var instance = new ClassWithSampleIdentity { Value = new SampleId("Sample_1") };
            var json = instance.ToJson();

            Assert.AreEqual("{ \"Value\" : \"Sample_1\" }", json);
        }

        [Test]
        public void should_serialize_with_base_class()
        {
            var instance = new ClassWithIdentityBaseClass { Value = new SampleId("Sample_1") };
            var json = instance.ToJson();

            Assert.AreEqual("{ \"Value\" : \"Sample_1\" }", json);
        }

        [Test]
        public void should_serialize_with_base_class_and_attribute()
        {
            var instance = new ClassWithIdentityBaseClassAndAttribute { Value = new SampleId("Sample_1") };
            var json = instance.ToJson();

            Assert.AreEqual("{ \"Value\" : \"Sample_1\" }", json);
        }

        [Test]
        public void should_serialize_with_event_store_base_class_and_attribute()
        {
            var instance = new ClassWithEventStoreIdentityBaseClassAndAttribute { Value = new SampleId("Sample_1") };
            var json = instance.ToJson();

            Assert.AreEqual("{ \"Value\" : \"Sample_1\" }", json);
        }


        [Test]
        public void should_serialize_array()
        {
            var instance = new ClassWithArrayIdentity
            {
                Value = new SampleId[] {
                    new SampleId("Sample_1"),
                    new SampleId("Sample_2"),
                }
            };
            var json = instance.ToJson();

            Assert.AreEqual("{ \"Value\" : [\"Sample_1\", \"Sample_2\"] }", json);
        }

        [Test]
        public void should_deserialize()
        {
            var instance = BsonSerializer.Deserialize<ClassWithSampleIdentity>("{ Value:\"Sample_1\"}");
            Assert.AreEqual("Sample_1", (string)instance.Value);
        }

        [Test]
        public void should_deserialize_array()
        {
            var instance = BsonSerializer.Deserialize<ClassWithArrayIdentity>("{ \"Value\" : [\"Sample_1\", \"Sample_2\"] }");
            Assert.That(instance.Value, Is.EquivalentTo(new SampleId[] {
                    new SampleId("Sample_1"),
                    new SampleId("Sample_2"),
                }));
        }

        [Test]
        public void should_serialize_null()
        {
            var instance = new ClassWithSampleIdentity();
            var json = instance.ToJson();
            Assert.AreEqual("{ \"Value\" : null }", json);
        }

        //TODO NSTORE: Snapshot uses string identities, this test probably should be deleted.
        //[Test]
        //public void should_serialize_flat_with_aggregateSnapshot()
        //{
        //    var instance = new SnapshotInfo(new Sa)
        //    {
        //        Id = new SampleId(1),
        //        Version = 1,
        //        State = new StateWithIdentity() { Value = new SampleId(2) }
        //    };
        //    var json = instance.ToJson();
        //    Assert.That(json.Contains("\"_t\""), Is.False);
        //}

        [Test]
        public void should_deserialize_null()
        {
            var instance = BsonSerializer.Deserialize<ClassWithSampleIdentity>("{ Value: null}");
            Assert.IsNull(instance.Value);
        }

        [Test]
        public void can_convert_to_bson_value()
        {
            EventStoreIdentityCustomBsonTypeMapper.Register<SampleId>();
            var val = BsonValue.Create(new SampleId(1));
            Assert.IsInstanceOf<BsonString>(val);
        }

        [Test]
        public void should_serialize_flat_in_dictionary()
        {
            var instance = new ClassWithDictionaryOfObject();
            instance.Value = new Dictionary<string, object>() { { "test", new SampleId(1) } };
            var json = instance.ToJson();
            Assert.AreEqual("{ \"Value\" : { \"test\" : \"Sample_1\" } }", json);
        }

        [Test]
        public void base_smoke_dictionary()
        {
            var instance = new ClassWithDictionaryOfObject();
            instance.Value = new Dictionary<string, object>() { { "test", "blabla" } };
            var json = instance.ToJson();
            Assert.AreEqual("{ \"Value\" : { \"test\" : \"blabla\" } }", json);
        }
    }
}
