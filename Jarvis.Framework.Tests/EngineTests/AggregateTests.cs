using Jarvis.Framework.Kernel.Engine;
using NUnit.Framework;
using Castle.Windsor;

// ReSharper disable InconsistentNaming
namespace Jarvis.Framework.Tests.EngineTests
{
	[TestFixture]
    public class AggregateTests
    {
        AggregateFactoryEx _factory;

        [SetUp]
        public void Setup()
        {
            WindsorContainer container = new WindsorContainer();
            _factory = new AggregateFactoryEx(container.Kernel);
        }

        //TODO: NSTORE - check only AggregateFactory castle creation 
        //all aggregate tests are done in NStore
    }
}
