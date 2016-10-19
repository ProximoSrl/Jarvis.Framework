using System;
using System.Linq;
using System.Reflection;
using MongoDB.Bson.Serialization;
using Jarvis.Framework.Kernel.ProjectionEngine.Unfolder;

namespace Jarvis.Framework.Kernel.Engine.Snapshots
{
    public static class SnapshotRegistration
    {
        static SnapshotRegistration()
        {
            if (!BsonClassMap.IsClassMapRegistered(typeof(EventUnfolderMemento)))
            {
                var cma = new SnapshotClassMap(typeof(EventUnfolderMemento));
                BsonClassMap.RegisterClassMap<EventUnfolderMemento>();
            }
        }

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

        public static void AutomapAggregateQueryModel(Assembly assembly)
        {
            var inMemoryProjectionTypes = assembly.GetTypes()
               .Where(x => typeof(BaseAggregateQueryModel).IsAssignableFrom(x) && x.IsClass && !x.IsAbstract);

            // automap dello stato
            foreach (var projectionType in inMemoryProjectionTypes)
            {
                if (!BsonClassMap.IsClassMapRegistered(projectionType))
                {
                    var classMap = new BsonClassMap(projectionType);
                    classMap.AutoMap();
                    BsonClassMap.RegisterClassMap(classMap);
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
