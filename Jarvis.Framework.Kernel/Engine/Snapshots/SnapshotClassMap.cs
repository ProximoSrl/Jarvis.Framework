using System;
using System.Linq;
using System.Reflection;
using MongoDB.Bson.Serialization;

namespace Jarvis.Framework.Kernel.Engine.Snapshots
{
    public static class SnapshotRegistration
    {
#pragma warning disable S125 // Sections of code should not be "commented out"
        public static void AutomapAggregateState(Assembly assembly)
        {
            //var stateTypes = assembly.GetTypes()
                //    .Where(x => typeof(AggregateState).IsAssignableFrom(x) && x.IsClass && !x.IsAbstract);

            //TODO: VErify if we want to change discriminator.

            //// automap dello stato
            //foreach (var state in stateTypes)
            //{
            //    var st = typeof(AggregateSnapshot<>).MakeGenericType(new[] { state });
            //    if (!BsonClassMap.IsClassMapRegistered(st))
            //    {
            //        var cma = new SnapshotClassMap(st);
            //        BsonClassMap.RegisterClassMap(cma);
            //    }
            //}
        }
#pragma warning restore S125 // Sections of code should not be "commented out"
    }

#pragma warning disable S125
    //public class SnapshotClassMap : BsonClassMap
    // Sections of code should not be "commented out"
    //{
    //    public SnapshotClassMap(Type classType)
    //        : base(classType)
    //    {
    //        var name = classType.Name;
    //        if (classType.IsGenericType)
    //        {
    //            name = classType.GetGenericArguments().First().FullName;
    //        }
    //        AutoMap();
    //        SetDiscriminator(name);
    //    }
    //}
#pragma warning restore S125 // Sections of code should not be "commented out"
}

