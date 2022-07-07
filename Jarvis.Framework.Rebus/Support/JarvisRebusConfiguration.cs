using System;
using System.Collections.Generic;
using System.Reflection;

namespace Jarvis.Framework.Bus.Rebus.Integration.Support
{
    /// <summary>
    /// Configuration for rebus, it is necessary to have it centralized
    /// because central rebus registration is done by the framework.
    /// </summary>
    [Serializable]
    public class JarvisRebusConfiguration
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="connectionString"></param>
        /// <param name="prefix"></param>
        public JarvisRebusConfiguration(
            String connectionString,
            String prefix)
        {
            ConnectionString = connectionString;
            Prefix = prefix;

            EndpointsMap = new Dictionary<string, string>();
            ExplicitSubscriptions = new List<ExplicitSubscription>();
        }

        public String InputQueue { get; set; }

        public String ErrorQueue { get; set; }

        public String TransportAddress { get; set; }

        public Int32 NumOfWorkers { get; set; }

        public Int32 MaxRetry { get; set; }

        public Dictionary<String, String> EndpointsMap { get; set; }

        /// <summary>
        /// Some configuration, like mongodb transport or specific configuration can be
        /// required by the users, this dictionary allows for extra configuration parameter
        /// to be stored in a single configuration service object.
        /// </summary>
        public Dictionary<String, String> Parameters { get; set; }

        public List<Assembly> AssembliesWithMessages { get; set; }

        public List<ExplicitSubscription> ExplicitSubscriptions { get; set; }

        /// <summary>
        /// <para>
        /// This is the priority for the startable component that will
        /// start the bus. Remember that bus registration is a two phase
        /// operation. In the first one the IBus is registered
        /// then the bus is started.
        /// </para>
        /// <para>
        /// <br />
        /// This should be a value taken from <see cref="JarvisStartableFacility.Priorities"/>
        /// </para>
        /// </summary>
        public Int32 StartBusPriority { get; set; }

        /// <summary>
        /// Prefix of the queue, each queue has a prefix in jarvis to differentiate
        /// between the queues of the various bounded contexts.
        /// </summary>
        public String Prefix { get; private set; }

        /// <summary>
        /// Needed to persist data in mongo (subscriptions, timeout).
        /// </summary>
        public String ConnectionString { get; private set; }

        /// <summary>
        /// Set to false, should be true only for test.
        /// </summary>
        public Boolean CentralizedConfiguration { get; set; }
    }

    public class ExplicitSubscription
    {
        public String MessageType { get; set; }

        public String Endpoint { get; set; }
    }
}
