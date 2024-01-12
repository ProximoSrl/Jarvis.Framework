using Jarvis.Framework.Kernel.Events;
using Jarvis.Framework.Kernel.ProjectionEngine;
using Jarvis.Framework.Shared.IdentitySupport;
using Jarvis.Framework.Shared.Messages;
using Jarvis.Framework.Tests.EngineTests;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Threading;
using System.Threading.Tasks;

namespace Jarvis.Framework.Tests.ProjectionEngineTests.V2
{
	//[TestFixture("1")]
	[TestFixture("2")]
	public class PollerCanStopAndRestart : AbstractV2ProjectionEngineTests
	{
		public PollerCanStopAndRestart(String pollingClientVersion) : base(pollingClientVersion)
		{
		}

		protected override void RegisterIdentities(IdentityManager identityConverter)
		{
			identityConverter.RegisterIdentitiesFromAssembly(typeof(SampleAggregateId).Assembly);
		}

		protected override string GetConnectionString()
		{
			return ConfigurationManager.ConnectionStrings["engine"].ConnectionString;
		}

		protected override IEnumerable<IProjection> BuildProjections()
		{
			var writer = new CollectionWrapper<SampleReadModel, string>(StorageFactory, new NotifyToNobody());
			yield return new Projection(writer);
		}

		protected override Task OnStartPolling()
		{
			return Engine.StartAsync(100);
		}

		[Test]
		public async Task stop_and_restart_polling_should_work()
		{
			var aggregate = await Repository.GetByIdAsync<SampleAggregate>(new SampleAggregateId(1)).ConfigureAwait(false);
			aggregate.Create();
			await Repository.SaveAsync(aggregate, Guid.NewGuid().ToString(), h => { }).ConfigureAwait(false);

			Boolean checkpointPassed = WaitForCheckpoint(1);
			NUnit.Framework.Legacy.ClassicAssert.IsTrue(checkpointPassed, "Automatic poller does not work.");

			Engine.Stop();

			aggregate = await Repository.GetByIdAsync<SampleAggregate>(new SampleAggregateId(2)).ConfigureAwait(false);
			aggregate.Create();
			await Repository.SaveAsync(aggregate, Guid.NewGuid().ToString(), h => { }).ConfigureAwait(false);

			checkpointPassed = WaitForCheckpoint(2);
			NUnit.Framework.Legacy.ClassicAssert.IsFalse(checkpointPassed, "Automatic poller is still working after stop.");

			await Engine.StartAsync(100).ConfigureAwait(false);

			checkpointPassed = WaitForCheckpoint(2);
			NUnit.Framework.Legacy.ClassicAssert.IsTrue(checkpointPassed, "Automatic poller is not restarted correctly.");
		}

		private Boolean WaitForCheckpoint(Int64 checkpointToken)
		{
			DateTime startTime = DateTime.Now;
			Boolean passed = false;
			while (
				!(passed = _statusChecker.IsCheckpointProjectedByAllProjection(checkpointToken))
				&& DateTime.Now.Subtract(startTime).TotalMilliseconds < 7000)

			{
				Thread.Sleep(100);
			}
			return passed;
		}
	}
}