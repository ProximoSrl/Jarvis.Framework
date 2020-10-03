using Castle.Core.Logging;
using NStore.Core.Logging;

namespace Jarvis.Framework.Shared.Logging
{
	/// <summary>
	/// We already have a wrapper around logger library, implemented
	/// by castle library. To satisfy the need of NStore library for
	/// a logger factory, we can simply rewrap the <see cref="ILogger"/>
	/// interface with a concrete implementation of NStoreILogging interface
	/// with the class <see cref="NStoreCastleLogger"/>
	/// </summary>
	public class NStoreCastleLoggerFactory : INStoreLoggerFactory
	{
		private readonly ILoggerFactory _factory;

		public NStoreCastleLoggerFactory(ILoggerFactory factory)
		{
			_factory = factory;
		}

		public INStoreLogger CreateLogger(string categoryName)
		{
			return new NStoreCastleLogger(_factory.Create(categoryName));
		}
	}
}
