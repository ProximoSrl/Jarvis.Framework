using MongoDB.Driver;
using MongoDB.Driver.Core.Events;
using MongoDB.Driver.Linq;
using System;

namespace Jarvis.Framework.Shared.Support
{
    /// <summary>
    /// To avoid direct dependency from Application insight this is the interface
    /// that should be implemented. 
    /// </summary>
    public interface IMongoQueryInterceptorConsumer
    {
        void TrackMongoOperation(
            Boolean succeeded,
            CommandStartedEventInfo commandStartedEvent,
            CommandSucceededEvent? commandSucceeded,
            CommandFailedEvent? commandFailedEvent);
    }

    public class NullMongoQueryInterptorConsumer : IMongoQueryInterceptorConsumer
    {
        public static IMongoQueryInterceptorConsumer Instance = new NullMongoQueryInterptorConsumer();

        private NullMongoQueryInterptorConsumer()
        {
        }

        public void TrackMongoOperation(
            bool succeeded,
            CommandStartedEventInfo commandStartedEvent,
            CommandSucceededEvent? commandSucceeded,
            CommandFailedEvent? commandFailedEvent)
        {
        }
    }

    public static class JarvisFrameworkMongoClientConfigurationOptions
    {
        internal static Action<MongoClientSettings> ConfigureClientSettings = (settings) =>
        {
            CustomClientSettingsConfiguration(settings);
        };

        internal static Action<MongoClientSettings> CustomClientSettingsConfiguration = (settings) => { };

        public static void SetCustomClientConfiguration(Action<MongoClientSettings> customClientSettingsConfiguration)
        {
            CustomClientSettingsConfiguration = customClientSettingsConfiguration;
        }
    }
}
