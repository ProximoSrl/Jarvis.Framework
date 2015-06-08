using System;
using Jarvis.Framework.Shared.ReadModel;

namespace Jarvis.Framework.Tests.ProjectionEngineTests
{
    public class SampleReadModel : AbstractReadModel<string>
    {
        public Int64 Timestamp { get; set; }
    }

    public class SampleReadModel2 : AbstractReadModel<string>
    {
        public Int64 Timestamp { get; set; }
    }

    public class SampleReadModel3 : AbstractReadModel<string>
    {
        public Int64 Timestamp { get; set; }
    }
}