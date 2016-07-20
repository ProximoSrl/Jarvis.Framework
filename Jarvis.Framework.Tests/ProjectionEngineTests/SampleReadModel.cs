using System;
using Jarvis.Framework.Shared.ReadModel;
using Jarvis.Framework.Tests.SharedTests;
using Jarvis.Framework.Tests.SharedTests.IdentitySupport;

namespace Jarvis.Framework.Tests.ProjectionEngineTests
{
    public class SampleReadModel : AbstractReadModel<string>
    {
        public Int64 Timestamp { get; set; }

        public Boolean IsInRebuild { get; set; }
    }

    public class SampleReadModel2 : AbstractReadModel<string>
    {
        public Int64 Timestamp { get; set; }
        public Boolean IsInRebuild { get; set; }
    }

    public class SampleReadModel3 : AbstractReadModel<string>
    {
        public Int64 Timestamp { get; set; }
        public Boolean IsInRebuild { get; set; }
    }

    public class SampleReadModel4 : AbstractReadModel<string>
    {
        public Int64 Timestamp { get; set; }
        public String Name { get; set; }

        public Int32 Value { get; set; }

    }

    public class SampleReadModelTestId : AbstractReadModel<TestId>
    {
        public String Value { get; set; }
    }
}