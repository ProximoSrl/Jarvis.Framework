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
    public interface IMongoQueryInterptorConsumer
    {
        void TrackMongoOperation(Boolean succeeded, String commandType, String commandDescription, TimeSpan duration, Exception exception);
    }

    public class NullMongoQueryInterptorConsumer : IMongoQueryInterptorConsumer
    {
        public static IMongoQueryInterptorConsumer Instance = new NullMongoQueryInterptorConsumer();

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
        public static IMongoQueryInterptorConsumer MongoQueryInterptorConsumer = NullMongoQueryInterptorConsumer.Instance;

        public static IMongoClient CreateClient(this MongoUrl url)
        {
            var settings = MongoClientSettings.FromUrl(url);
            return CreateClient(settings);
        }

        private static  ConcurrentDictionary<Int64, String> _requests = new ConcurrentDictionary<Int64, String>();

        public static IMongoClient CreateClient(this MongoClientSettings settings)
        {
            settings.ClusterConfigurator = clusterConfigurator =>
            {
                clusterConfigurator.Subscribe<CommandStartedEvent>(e => 
                {
                    if (e.OperationId.HasValue)
                    {
                        _requests.TryAdd(e.OperationId.Value, e.Command.ToString());
                    }
                });

                clusterConfigurator.Subscribe<CommandSucceededEvent>(e =>
                {
                    if (!_requests.TryRemove(e.OperationId ?? -1, out var command))
                    {
                        command = $"Command unknown: operation id {e.OperationId}";
                    }
                    MongoQueryInterptorConsumer.TrackMongoOperation(true, e.CommandName, command, e.Duration, null);
                });

                clusterConfigurator.Subscribe<CommandFailedEvent>(e =>
                {
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
