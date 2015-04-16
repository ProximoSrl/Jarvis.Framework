using System;
using System.Linq;
using System.Reflection;
using MongoDB.Bson.Serialization;

namespace Jarvis.Framework.Kernel.Engine.Snapshots
{
    public static class SnapshotRegistration
    {
        public static void AutomapAggregateState(Assembly assembly)
        {
            var stateTypes = assembly.GetTypes()
                .Where(x => typeof(AggregateState).IsAssignableFrom(x) && x.IsClass && !x.IsAbstract);

            // automap dello stato
            foreach (var state in stateTypes)
            {
                var st = typeof(AggregateSnapshot<>).MakeGenericType(new[] { state });
                if (!BsonClassMap.IsClassMapRegistered(st))
                {
                    var cma = new SnapshotClassMap(st);
                    BsonClassMap.RegisterClassMap(cma);
                }
            }
        }
    }

    public class SnapshotClassMap : BsonClassMap
    {
        public SnapshotClassMap(Type classType)
            : base(classType)
        {
            var name = classType.Name;
            if (classType.IsGenericType)
            {
                name = classType.GetGenericArguments().First().FullName;
            }
            AutoMap();
            SetDiscriminator(name);
        }
    }
}
