using System.Collections.Generic;
using Castle.Core.Logging;
using Jarvis.Framework.Kernel.Events;

namespace Jarvis.Framework.Kernel.ProjectionEngine
{
    public class InitializeReadModelDb : IInitializeReadModelDb
    {
        readonly IEnumerable<IProjection> _projections;
	    readonly ILogger _logger;
        public InitializeReadModelDb(IEnumerable<IProjection> projections, ILogger logger)
        {
            _projections = projections;
            _logger = logger;

            _logger.Debug("Projections:");
            foreach (var projection in _projections)
            {
                _logger.DebugFormat("\t{0}", projection.GetType().FullName);
            }

            _logger.Debug("----------------------------------");

        }

        public void Init(bool drop)
        {
            foreach (var projection in _projections)
            {
                if (drop)
                {
                    projection.Drop();
                }
                projection.SetUp();
            }
        }
    }
}