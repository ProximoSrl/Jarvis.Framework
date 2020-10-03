using System;
using Fasterflect;
using Jarvis.Framework.Kernel.Engine;
using Jarvis.Framework.Shared.IdentitySupport;
using NStore.Core.Snapshots;

namespace Jarvis.Framework.TestHelpers
{
	public static class TestAggregateFactory
    {
        public static T Create<T, TId, TState>(TState initialState, IIdentity identity, Int32 version)
            where T : AggregateRoot<TState, TId>
            where TState : JarvisAggregateState, new()
			where TId : EventStoreIdentity
        {
            if (identity == null)
                throw new ArgumentNullException(nameof(identity));

            var ctor = typeof(T).Constructor(Flags.Default, new Type[] { });
            if (ctor == null)
                throw new MissingMethodException(string.Format("{0} missing default ctor", typeof(T).FullName));

            var aggregate = (T)ctor.CreateInstance();

            //To restore the state from the test we need to pass from the snapshot
            if (version > 0)
            {
                SnapshotInfo snapshot = new SnapshotInfo(
                    identity.ToString(),
                    version,
                    initialState,
                    initialState.VersionSignature);

                aggregate.Init(identity.ToString());
                var restoreResult = ((ISnapshottable)aggregate).TryRestore(snapshot);
                if (!restoreResult)
                    throw new Machine.Specifications.SpecificationException("Unable to restore snapshot into the aggregate under test");
            }
            else if (initialState != null)
            {
                throw new Machine.Specifications.SpecificationException("If you specify a not null initialState version should be greater than zero. Pass null as state if you want to create a new empty aggregate!!");
            }
            aggregate.Init(identity.AsString());
            return aggregate;
        }
    }
}