using Jarvis.Framework.Shared.IdentitySupport;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using NUnit.Framework;
using System;

namespace Jarvis.Framework.Tests.SharedTests.IdentitySupport
{
    /// <summary>
    /// Identities stored without flat mapping are documents <c>{ "_id" : "Tag_42" }</c>: the class map
    /// serializer creates the identity with its parameterless constructor and populates it through the
    /// private <see cref="EventStoreIdentity.FullyQualifiedId"/> setter. The serializer is created
    /// explicitly from the class map, so the flat serializers registered globally by other tests do not
    /// interfere. The identity type is generated on the fly, so it does not end up in the test assembly.
    /// </summary>
    [TestFixture]
    public class EventStoreIdentityBsonCompatibilityTests
    {
        [Test]
        public void Non_flat_bson_document_is_deserialized_through_private_setter()
        {
            var typeName = "BsonCompat" + Guid.NewGuid().ToString("N") + "Id";
            var type = IdentityManagerRegistrationTests
                .BuildIdentityAssembly(typeName, withParameterlessConstructor: true)
                .GetType(typeName, throwOnError: true);
            var tag = typeName[..^2];

            var classMap = BsonClassMap.LookupClassMap(type);
            var serializer = (IBsonSerializer)Activator.CreateInstance(
                typeof(BsonClassMapSerializer<>).MakeGenericType(type), classMap);

            var document = new BsonDocument("_id", tag + "_42");
            EventStoreIdentity identity;
            using (var reader = new BsonDocumentReader(document))
            {
                var context = BsonDeserializationContext.CreateRoot(reader);
                identity = (EventStoreIdentity)serializer.Deserialize(context);
            }

            Assert.That(identity.Id, Is.EqualTo(42));
            Assert.That(identity.AsString(), Is.EqualTo(tag + "_42"));
        }
    }
}
