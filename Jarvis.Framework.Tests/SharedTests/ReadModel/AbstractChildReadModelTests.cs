using Jarvis.Framework.Shared.ReadModel;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jarvis.Framework.Tests.SharedTests.ReadModel
{
	[TestFixture]
	public class AbstractChildReadModelTests
	{
		[Test]
		public void Verify_replace_helper_dischard_duplicates()
		{
			var rm1 = new ReadModelTest("test1", 42);
			var rm2 = new ReadModelTest("test1", 42);

			List<ReadModelTest> sut = new List<ReadModelTest>();
			sut.AddOrReplace(rm1);
			sut.AddOrReplace(rm2);

			Assert.That(sut.Count, Is.EqualTo(1));
		}

		[Test]
		public void Verify_replace_helper_works_with_unique_ids()
		{
			var rm1 = new ReadModelTest("test1", 42);
			var rm2 = new ReadModelTest("test2", 42);

			List<ReadModelTest> sut = new List<ReadModelTest>();
			sut.AddOrReplace(rm1);
			sut.AddOrReplace(rm2);

			Assert.That(sut.Count, Is.EqualTo(2));
		}

		public class ReadModelTest : AbstractChildReadModel<String>
		{
			public ReadModelTest(string id, int value)
			{
				Id = id;
				Value = value;
			}

			public Int32 Value { get; set; }
		}
	}
}
