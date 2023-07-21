using MongoDB.Driver;
using MongoDB.Driver.Core.Events;
using MongoDB.Driver.Linq;
using System;
using System.Collections.Concurrent;

namespace Jarvis.Framework.Shared.Support
{
    /// <summary>
    /// To avoid direct dependency from Application insight this is the interface
    /// that should be implemented. 
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

    public static class JarvisFrameworkMongoClientConfigurationOptions
    {
        internal static Action<MongoClientSettings> ConfigureClientSettings = (settings) => 
        {
            if (!JarvisFrameworkGlobalConfiguration.IsMongodbLinq3Enabled)
            {
                settings.LinqProvider = LinqProvider.V2;
            }
            CustomClientSettingsConfiguration(settings);
        };

        internal static Action<MongoClientSettings> CustomClientSettingsConfiguration = (settings) => { };

        public static void SetCustomClientConfiguration(Action<MongoClientSettings> customClientSettingsConfiguration)
        {
            CustomClientSettingsConfiguration = customClientSettingsConfiguration;
        }
    }
}
