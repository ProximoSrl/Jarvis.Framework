using Jarvis.Framework.Shared.HealthCheck;
using NUnit.Framework;

namespace Jarvis.Framework.Tests.SharedTests
{
    [TestFixture]
    public class HealthCheckResultTests
    {
        /// <summary>
        /// We had a bug when Unhealty always uses string.format to format message
        /// but sometimes the user simply pass a single string with ex.tostring() that
        /// result in a bad FormatException.
        /// </summary>
        [Test]
        public void Can_log_single_message()
        {
            Assert.DoesNotThrow(() => JarvisFrameworkHealthCheckResult.Unhealthy("wrong { string }"));
        }
    }
}
