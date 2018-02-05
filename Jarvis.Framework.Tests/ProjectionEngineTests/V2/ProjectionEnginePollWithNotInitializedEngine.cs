using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Jarvis.Framework.Kernel.Events;
using Jarvis.Framework.Shared.IdentitySupport;
using Jarvis.Framework.Shared.Helpers;

namespace Jarvis.Framework.Tests.ProjectionEngineTests.V2
{
    [TestFixture("2")]
    public class ProjectionEnginePollWithNotInitializedEngine : ProjectionEngineBasicTestBase
    {
        public ProjectionEnginePollWithNotInitializedEngine(string pollingClientVersion)
            : base("2")
        {
        }

        protected override async Task OnStartPolling()
        {
            //Avoid calling start polling, the projection engine is still not started
            await Engine.StartWithManualPollAsync(false).ConfigureAwait(false);
        }

        [Test]
        public async Task manual_poll_should_not_throw()
        {
            await Engine.PollAsync().ConfigureAwait(false);
        }

        [Test]
        public async Task manual_update_should_not_throw()
        {
            await Engine.UpdateAsync().ConfigureAwait(false);
        }

        [Test]
        public async Task manual_updateAndWait_should_not_throw()
        {
            await Engine.UpdateAndWaitAsync().ConfigureAwait(false);
        }
    }
}
