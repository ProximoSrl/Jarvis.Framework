using MongoDB.Driver;
using MongoDB.Driver.Core.Events;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace Jarvis.Framework.Shared.Support
{
    /// <summary>
    /// To avoid direct dependency from Application insight this is the interface
    /// that should be implemneted 
    /// </summary>
    public interface IMongoQueryInterceptorConsumer
    {
        void TrackMongoOperation(Boolean succeeded, String commandType, String commandDescription, TimeSpan duration, Exception exception);
    }

    public class NullMongoQueryInterptorConsumer : IMongoQueryInterceptorConsumer
    {
        public static IMongoQueryInterceptorConsumer Instance = new NullMongoQueryInterptorConsumer();

        private NullMongoQueryInterptorConsumer()
        {
        }

        public void TrackMongoOperation(Boolean succeeded, String commandType, String commandDescription, TimeSpan duration, Exception exception)
        {
        }
    }

    /// <summary>
    /// Based on https://stackoverflow.com/questions/41806029/how-to-track-mongodb-requests-from-a-console-application
    /// </summary>
    public static class MongoQueryInterceptorExtension
    {
        public static IMongoQueryInterceptorConsumer MongoQueryInterptorConsumer = NullMongoQueryInterptorConsumer.Instance;

        private static readonly ConcurrentDictionary<String, IMongoClient> _mongoClientCache = new ConcurrentDictionary<string, IMongoClient>();

        /// <summary>
        /// Creates a mongo client and enable interception through <see cref="MongoQueryInterptorConsumer"/> class.
        /// </summary>
        /// <param name="url"></param>
        /// <param name="enableInterception"></param>
        /// <returns></returns>
        public static IMongoClient CreateClient(this MongoUrl url, Boolean enableInterception)
        {
            var connectionKey = url.ToString() + enableInterception;
            if (!_mongoClientCache.TryGetValue(connectionKey, out var client))
            {
                if (enableInterception)
                {
                    var settings = MongoClientSettings.FromUrl(url);
                    client = CreateClient(settings);
                }
                else
                {
                    client = new MongoClient(url);
                }

                _mongoClientCache.TryAdd(connectionKey, client);
            }
            return client;
        }

        private static readonly ConcurrentDictionary<Int64, String> _requests = new ConcurrentDictionary<Int64, String>();

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

        public static IMongoClient CreateClient(this MongoClientSettings settings)
        {
            settings.ClusterConfigurator = clusterConfigurator =>
            {
                clusterConfigurator.Subscribe<CommandStartedEvent>(e => 
                {
                    if (MongoQueryInterptorConsumer == NullMongoQueryInterptorConsumer.Instance)
                        return;

                    if (e.OperationId.HasValue)
                    {
                        _requests.TryAdd(e.OperationId.Value, e.Command.ToString());
                    }
                });

                clusterConfigurator.Subscribe<CommandSucceededEvent>(e =>
                {
                    if (MongoQueryInterptorConsumer == NullMongoQueryInterptorConsumer.Instance)
                        return;

                    if (!_requests.TryRemove(e.OperationId ?? -1, out var command))
                    {
                        command = $"Command unknown: operation id {e.OperationId}";
                    }
                    MongoQueryInterptorConsumer.TrackMongoOperation(true, e.CommandName, command, e.Duration, null);
                });

                clusterConfigurator.Subscribe<CommandFailedEvent>(e =>
                {
                    if (MongoQueryInterptorConsumer == NullMongoQueryInterptorConsumer.Instance)
                        return;

                    if (!_requests.TryRemove(e.OperationId ?? -1, out var command))
                    {
                        command = $"Command unknown: operation id {e.OperationId}";
                    }
                    MongoQueryInterptorConsumer.TrackMongoOperation(false, e.CommandName, command, e.Duration, e.Failure);
                });
            };
            return new MongoClient(settings);
        }
    }
}
