using System;
using System.Collections.Generic;
using System.Reflection;
using System.Timers;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;
using MongoDB.Bson.Serialization;
using MongoDB.Bson;
using System.Linq;
using Ponder;
using Rebus.Logging;
using MongoDB.Bson.Serialization.Serializers;
using System.Linq.Expressions;
using Jarvis.Framework.Shared.Helpers;

namespace Rebus.MongoDb
{
    /// <summary>
    /// MongoDB implementation of Rebus' <see cref="IStoreSagaData"/>. Will store saga data as they are serialized by the
    /// default BSON serializer, with the exception that the <see cref="ISagaData.Revision"/> property is serialized with
    /// "_rev" as the property name.
    /// </summary>
    public class MongoDbSagaPersister : IStoreSagaData
    {
        const string IdElementName = "_id";

        static readonly string RevisionMemberName;
        static ILog log;

        static MongoDbSagaPersister()
        {
            RebusLoggerFactory.Changed += f => log = f.GetCurrentClassLogger();

            // try to use our own naming convention
            RevisionMemberName = NamingConvention.RevisionMemberName;
            
            ConventionRegistry.Register("SagaDataConventionPack",
                                        NamingConvention,
                                        t => typeof (ISagaData).IsAssignableFrom(t));
        }



        class SagaDataNamingConvention : IConventionPack, IMemberMapConvention
        {
            public SagaDataNamingConvention()
            {
                RevisionMemberName = Reflect.Path<ISagaData>(d => d.Revision);
            }

            public IEnumerable<IConvention> Conventions
            {
                get { yield return this; }
            }

            public string Name { get; private set; }

            public string RevisionMemberName { get; private set; }

            public void Apply(BsonMemberMap memberMap)
            {
                if (memberMap.MemberName == RevisionMemberName)
                {
                    memberMap.SetElementName("_rev");
                }
            }

            public string GetElementName(PropertyInfo propertyInfo)
            {
                return propertyInfo.Name == RevisionMemberName
                           ? "_rev"
                           : propertyInfo.Name;
            }
        }

        readonly Dictionary<Type, string> collectionNames = new Dictionary<Type, string>();
        readonly IMongoDatabase database;
        readonly Timer indexRecreationTimer = new Timer();

        /// <summary>
        /// We keep track whether the index has been declared recently in order to minimize the risk that someone
        /// accidentally removes the unique constraint behind our back
        /// </summary>
        volatile bool indexEnsuredRecently;
        readonly object indexEnsuredRecentlyLock = new object();

        bool allowAutomaticSagaCollectionNames;
        static readonly SagaDataNamingConvention NamingConvention = new SagaDataNamingConvention();

        /// <summary>
        /// Constructs the persister which will connect to the Mongo database pointed to by the connection string.
        /// This also means that the connection string must include the database name.
        /// </summary>
        public MongoDbSagaPersister(string connectionString)
        {
            log.Info("Connecting to Mongo");
            database = MongoHelper.GetDatabase(connectionString);

            // flick the bool once in a while
            indexRecreationTimer.Elapsed += delegate { indexEnsuredRecently = false; };
            indexRecreationTimer.Interval = TimeSpan.FromMinutes(1).TotalMilliseconds;
            indexRecreationTimer.Start();
        }

        /// <summary>
        /// Tells the persister that it's ok that it comes up with collection names for saga data by itself. This
        /// lowers the friction, but since the saga data type name is used to come up with a collection name, it
        /// would cause weird behaviour if you renamed a saga data class.
        /// </summary>
        public MongoDbSagaPersister AllowAutomaticSagaCollectionNames()
        {
            log.Info("Saga persister will figure out Mongo collection names by convention");
            allowAutomaticSagaCollectionNames = true;
            return this;
        }

        /// <summary>
        /// Tells the persister to store saga data of the specified type in the collection with the given name
        /// </summary>
        public MongoDbSagaPersister SetCollectionName<TSagaData>(string collectionName) where TSagaData : ISagaData
        {
            var sagaDataType = typeof(TSagaData);

            if (collectionNames.ContainsKey(sagaDataType))
            {
                var errorMessage =
                    string.Format("Attempted to set the collection name of saga data of type {0} to {1}, but it was" +
                                  " already set to {2}! It is not permitted to set the collection name twice, because" +
                                  " this is most likely be an indication that some initialization part of your code is" +
                                  " running twice, which could have unintended consequences",
                                  sagaDataType, collectionName, collectionNames[sagaDataType]);

                throw new InvalidOperationException(errorMessage);
            }

            log.Info("Saga data of type {0} will be stored in collection named {1}", sagaDataType, collectionName);

            collectionNames.Add(sagaDataType, collectionName);

            return this;
        }

        /// <summary>
        /// Inserts the given saga data, once in a while also ensuring that synchronous indexes with unique
        /// constraints are created for the given saga data property paths.
        /// </summary>
        public void Insert(ISagaData sagaData, string[] sagaDataPropertyPathsToIndex)
        {
            var collection = database.GetCollection<BsonDocument>(GetCollectionName(sagaData.GetType()));
            collection.Settings.WriteConcern = WriteConcern.Acknowledged;
            EnsureIndexHasBeenCreated(sagaDataPropertyPathsToIndex, collection);

            sagaData.Revision++;
            var sagaDataBson = BsonDocument.Create(sagaData);
            try
            {
                collection.InsertOne(sagaDataBson);
            }
            catch (MongoWriteConcernException ex)
            {
                // in case of race conditions, we get a duplicate key error because the upsert
                // cannot proceed to insert a document with the same _id as an existing document
                // ... therefore, we map the MongoSafeModeException to our own OptimisticLockingException
                throw new OptimisticLockingException(sagaData, ex);
            }
        }

        /// <summary>
        /// Updates the given saga data with an optimistic lock, once in a while also ensuring that synchronous
        /// indexes with unique constraints are created for the given saga data property paths.
        /// </summary>
        public void Update(ISagaData sagaData, string[] sagaDataPropertyPathsToIndex)
        {
            var collection = database.GetCollection<BsonDocument>(GetCollectionName(sagaData.GetType()));
            collection.Settings.WriteConcern = WriteConcern.Acknowledged;

            EnsureIndexHasBeenCreated(sagaDataPropertyPathsToIndex, collection);

            var revisionElementName = GetRevisionElementName(sagaData);

            var criteria = Builders<BsonDocument>.Filter.And(Builders<BsonDocument>.Filter.Eq(IdElementName, sagaData.Id),
                                     Builders<BsonDocument>.Filter.Eq(revisionElementName, sagaData.Revision));

            sagaData.Revision++;
            var docData = BsonDocument.Create(sagaData);
            try
            {
                var safeModeResult = collection.FindOneAndReplace(
                    criteria,
                    docData);
            }
            catch (MongoWriteConcernException ex)
            {
                // in case of race conditions, we get a duplicate key error because the upsert
                // cannot proceed to insert a document with the same _id as an existing document
                // ... therefore, we map the MongoSafeModeException to our own OptimisticLockingException
                throw new OptimisticLockingException(sagaData, ex);
            }
        }

        /// <summary>
        /// Deletes the given saga data from the underlying Mongo collection. Throws and <see cref="OptimisticLockingException"/>
        /// if not exactly one saga data document was deleted.
        /// </summary>
        public void Delete(ISagaData sagaData)
        {
            var collection = database.GetCollection<BsonDocument>(GetCollectionName(sagaData.GetType()));
            collection.Settings.WriteConcern = WriteConcern.Acknowledged;
            var revisionElementName = GetRevisionElementName(sagaData);

            var query = Builders<BsonDocument>.Filter.And(Builders<BsonDocument>.Filter.Eq(IdElementName, sagaData.Id),
                                  Builders<BsonDocument>.Filter.Eq(revisionElementName, sagaData.Revision));

            try
            {
                var safeModeResult = collection.DeleteMany(query);
            }
            catch (MongoWriteConcernException ex)
            {
                // in case of race conditions, we get a duplicate key error because the upsert
                // cannot proceed to insert a document with the same _id as an existing document
                // ... therefore, we map the MongoSafeModeException to our own OptimisticLockingException
                throw new OptimisticLockingException(sagaData, ex);
            }
        }

        /// <summary>
        /// Queries the underlying Mongo collection for a saga data of the given type with the
        /// given value at the specified property path. Returns null if none could be found.
        /// </summary>
        public T Find<T>(string sagaDataPropertyPath, object fieldFromMessage) where T : class, ISagaData
        {
            var collection = database.GetCollection<T>(GetCollectionName(typeof(T)));

            if (sagaDataPropertyPath == "Id")
            {
                return collection.Find(Builders<T>.Filter.Eq("_id", fieldFromMessage))
                    .FirstOrDefault();
            }

            var bsonValue = fieldFromMessage != null
                ? fieldFromMessage
                : BsonNull.Value;

            var query = Builders<T>.Filter.Eq(MapSagaDataPropertyPath(sagaDataPropertyPath, typeof (T)), bsonValue);

            return collection.Find(query).FirstOrDefault();
        }

        string GetCollectionName(Type sagaDataType)
        {
            if (collectionNames.ContainsKey(sagaDataType))
            {
                return collectionNames[sagaDataType];
            }

            if (allowAutomaticSagaCollectionNames)
            {
                return GenerateAutoSagaCollectionName(sagaDataType);
            }

            throw new InvalidOperationException(
                string.Format(
                    @"This MongoDB saga persister doesn't know where to store sagas of type {0}.

You must specify a collection for each saga type on the persister, e.g. like so:

    new MongoDbSagaPersister(ConnectionString)
        .SetCollectionName<MySagaData>(""my_sagas"")
        .SetCollectionName<MyOtherSagaData>(""my_other_sagas"");

if you create the persister manually, or like this if you're using the fluent configuration API:

    Configure.With(adapter)
        (...)
        .Sagas(s => s.StoreInMongoDb(ConnectionString)
                        .SetCollectionName<SomeKindOfSaga>(""some_kind_of_saga"")
                        .SetCollectionName<AnotherKindOfSaga>(""another_kind_of_saga""))
        (...)

Alternatively, if you're more into the ""convention over configuration"" thing, trade a little bit of control for reduced friction, and let the persister come up with a name by itself:

    Configure.With(adapter)
        (...)
        .Sagas(s => s.StoreInMongoDb(ConnectionString)
                        .AllowAutomaticSagaCollectionNames())
        (...)

which will make the persister use the type of the saga to come up with collection names automatically - for sagas of the type {0}, the collection would be named '{1}'.
",
                    sagaDataType, GenerateAutoSagaCollectionName(sagaDataType)));
        }

        static string GenerateAutoSagaCollectionName(Type sagaDataType)
        {
            return string.Format("sagas_{0}", sagaDataType.Name);
        }

        void EnsureIndexHasBeenCreated(IEnumerable<string> sagaDataPropertyPathsToIndex, IMongoCollection<BsonDocument> collection)
        {
            if (!indexEnsuredRecently)
            {
                lock (indexEnsuredRecentlyLock)
                {
                    if (!indexEnsuredRecently)
                    {
                        var propertyPathsToIndex = sagaDataPropertyPathsToIndex.ToList();

                        log.Info("Re-declaring indexes with unique constraints for the following paths: {0}", string.Join(", ", propertyPathsToIndex));

                        //collection.ResetIndexCache();

                        //                        var indexes = collection.GetIndexes();

                        foreach (var propertyToIndex in propertyPathsToIndex.Except(new[] { "Id" }))
                        {
                            var indexDefinition = Builders<BsonDocument>.IndexKeys.Ascending(propertyToIndex);

                            //if (IndexAlreadyExists(indexes, propertyToIndex))
                            //{
                            //    AssertIndexIsCorrect(indexes, propertyToIndex);
                            //}

                            collection.Indexes.CreateOne(indexDefinition, new CreateIndexOptions() { Background = false, Unique = true});
                        }

                        //collection.ReIndex();

                        indexEnsuredRecently = true;
                    }
                }
            }
        }

        /// <summary>
        /// Asks the BSON serializer what is the Mongo element name for the revision
        /// property of saga data of the given type
        /// </summary>
        static string GetRevisionElementName(ISagaData sagaData)
        {
            var revisionElementName = "_rev";

            var classmap = BsonClassMap.LookupClassMap(sagaData.GetType());
            var revision = classmap.GetMemberMap(RevisionMemberName);
            if (revision != null)
            {
                revisionElementName = revision.ElementName;
            }

            return revisionElementName;
        }

        string MapSagaDataPropertyPath(string sagaDataPropertyPath, Type sagaDataType)
        {
            var propertyInfo = sagaDataType.GetProperty(sagaDataPropertyPath, BindingFlags.Public | BindingFlags.Instance);

            if (propertyInfo == null)
                return sagaDataPropertyPath;

            return NamingConvention.GetElementName(propertyInfo);
        }

      
    }
}
