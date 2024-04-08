using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Core.Events;
using MongoDB.Driver.Linq;
using System;
using System.Collections.Concurrent;

namespace Jarvis.Framework.Shared.Support
{
    /// <summary>
    /// Based on https://stackoverflow.com/questions/41806029/how-to-track-mongodb-requests-from-a-console-application
    /// </summary>
    public static class MongoQueryInterceptorExtension
    {
        public static IMongoQueryInterceptorConsumer MongoQueryInterptorConsumer = NullMongoQueryInterptorConsumer.Instance;

        private static readonly ConcurrentDictionary<String, IMongoClient> _mongoClientCache = new ConcurrentDictionary<string, IMongoClient>();

        /// <summary>
        /// Allow caller to know how many Clients were created to undersatnd if we have problem with client creation.
        /// </summary>
        public static int NumberOfCreatedClient => _mongoClientCache.Count;

        public static MongoClientSettings CreateMongoClientSettings(this MongoUrl url)
        {
            var settings = MongoClientSettings.FromUrl(url);
            return ConfigureSettings(settings);
        }

        public static MongoClientSettings CreateMongoClientSettings(this string url)
        {
            var settings = MongoClientSettings.FromConnectionString(url);
            return ConfigureSettings(settings);
        }

        private static MongoClientSettings ConfigureSettings(MongoClientSettings settings)
        {
            JarvisFrameworkMongoClientConfigurationOptions.ConfigureClientSettings(settings);
            return settings;
        }

        public static object _lock = new object();

        /// <summary>
        /// Creates a mongo client and enable interception through <see cref="MongoQueryInterptorConsumer"/> class.
        /// </summary>
        /// <param name="url"></param>
        /// <param name="enableInterception"></param>
        /// <returns></returns>
        public static IMongoClient CreateClient(this MongoUrl url, Boolean enableInterception)
        {
            var connectionKey = GetConnectionKey(url.ToString(), enableInterception);
            if (!_mongoClientCache.TryGetValue(connectionKey, out var client))
            {
                lock (_lock)
                {
                    if (!_mongoClientCache.TryGetValue(connectionKey, out client))
                    {
                        if (enableInterception)
                        {
                            client = CreateClientWithInterceptionEnabled(url.CreateMongoClientSettings());
                        }
                        else
                        {
                            client = new MongoClient(url.CreateMongoClientSettings());
                        }

                        _mongoClientCache[connectionKey] = client;
                    }
                }
            }
            return client;
        }

        private static readonly ConcurrentDictionary<Int64, CommandStartedEventInfo> _requests = new ConcurrentDictionary<Int64, CommandStartedEventInfo>();

        private static string GetConnectionKey(string connectionString, bool? enableIntercept)
        {
            var connBuilder = new MongoUrlBuilder(connectionString);
            connBuilder.DatabaseName = "admin"; //we do not want a client for different database name
            return connBuilder.ToMongoUrl().Url + enableIntercept;
        }

        /// <summary>
        /// Useful for client code to understand if there is some leak of the dictinoary (maybe mongo driver fail to 
        /// call close for some calls)
        /// </summary>
        public static Int32 GetActualRequestQueue => _requests.Count;

        /// <summary>
        /// Clear all request queue in memory, it will break interceptor for all ongoing query but
        /// can be useful to cleanup memory.
        /// </summary>
        public static void PurgetActualRequestQueue()
        {
            _requests.Clear();
        }

        /// <summary>
        /// Create a client enabling interception. Even if interception does
        /// nothing if MongoQueryInterptorConsumer property is null, we still
        /// generate burden on driver infrastructrure.
        /// </summary>
        /// <param name="settings"></param>
        /// <returns></returns>
        public static IMongoClient CreateClientWithInterceptionEnabled(this MongoClientSettings settings)
        {
            settings.ClusterConfigurator = clusterConfigurator =>
            {
                clusterConfigurator.Subscribe<CommandStartedEvent>(e =>
                {
                    if (MongoQueryInterptorConsumer == NullMongoQueryInterptorConsumer.Instance)
                        return;

                    if (e.OperationId.HasValue)
                    {
                        _requests.TryAdd(e.OperationId.Value, new CommandStartedEventInfo
                        {
                            //Need to perform a ToString because the real implementation of e.Command is disposable and
                            //will be disposed at the time that completion event are called.
                            Command = e.Command.ToString(),
                            CommandName = e.CommandName,
                            DatabaseNamespace = e.DatabaseNamespace
                        });
                    }
                });

                clusterConfigurator.Subscribe<CommandSucceededEvent>(e =>
                {
                    if (MongoQueryInterptorConsumer == NullMongoQueryInterptorConsumer.Instance)
                        return;

                    CommandStartedEventInfo commandStartedEvent = GetStartedEvent( e.OperationId);

                    MongoQueryInterptorConsumer.TrackMongoOperation(true, commandStartedEvent, e, null);
                });

                clusterConfigurator.Subscribe<CommandFailedEvent>(e =>
                {
                    if (MongoQueryInterptorConsumer == NullMongoQueryInterptorConsumer.Instance)
                        return;

                    CommandStartedEventInfo commandStartedEvent = GetStartedEvent(e.OperationId);

                    MongoQueryInterptorConsumer.TrackMongoOperation(false, commandStartedEvent, null, e);
                });
            };
            return new MongoClient(settings);
        }

        private static CommandStartedEventInfo GetStartedEvent(long? operationId)
        {
            CommandStartedEventInfo commandStartedEvent = null;
            if (_requests.TryRemove(operationId ?? -1, out var commandSe))
            {
                commandStartedEvent = commandSe;
            }

            return commandStartedEvent;
        }
    }

    public class CommandStartedEventInfo
    {
        public string Command { get; set; }

        public DatabaseNamespace DatabaseNamespace { get; set; }
        public string CommandName { get; internal set; }
    }
}
