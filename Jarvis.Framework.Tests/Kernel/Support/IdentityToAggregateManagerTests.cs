using Jarvis.Framework.Kernel.Engine;
using Jarvis.Framework.Kernel.Support;
using Jarvis.Framework.Tests.EngineTests;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Jarvis.Framework.Tests.Kernel.Support
{
	[TestFixture]
	public class IdentityToAggregateManagerTests
	{
		private IdentityToAggregateManager _sut;

		[SetUp]
		public void SetUp()
		{
			_sut = new IdentityToAggregateManager();
		}

		[Test]
		public void Verify_basic_scan()
		{
			_sut.ScanAssemblyForAggregateRoots(Assembly.GetExecutingAssembly());
			var type = _sut.GetAggregateFromId(new SampleAggregateId(1));
			Assert.That(type, Is.EqualTo(typeof(SampleAggregate)));
		}
	}
}
