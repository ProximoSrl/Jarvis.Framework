using System;
using System.Collections.Generic;
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

    public class SampleReadModelTest : AbstractReadModel<String>
    {
        public String Value { get; set; }

		public Int32 Counter { get; set; }
	}

    public class SampleReadModelPollableTest : PollableReadModel<TestId>
    {
        public String Value { get; set; }
    }

    public class SampleReadModelWithCollections : AbstractReadModel<String>
    {
        public SampleReadModelWithCollections()
        {
            Childs = new HashSet<string>();
            Parents = new HashSet<string>();
        }

        public HashSet<String> Childs { get; set; }

        public HashSet<String> Parents { get; set; }
    }
}