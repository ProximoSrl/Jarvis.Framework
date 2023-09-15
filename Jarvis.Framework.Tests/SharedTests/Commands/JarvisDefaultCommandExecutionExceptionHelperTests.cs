using Castle.Core.Logging;
using Jarvis.Framework.Shared.Commands;
using Jarvis.Framework.Shared.Exceptions;
using Jarvis.Framework.Shared.Messages;
using Jarvis.Framework.Tests.EngineTests;
using NStore.Core.Streams;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Security;

namespace Jarvis.Framework.Tests.SharedTests.Commands
{
    [TestFixture]
    public class JarvisDefaultCommandExecutionExceptionHelperTests
    {
        private const int MaximumNumberOfRetry = 10;
        private JarvisDefaultCommandExecutionExceptionHelper sut;
        private Boolean retry;
        private ICommand command;
        private CommandHandled replyCommand;

        [SetUp]
        public void SetUp()
        {
            command = Substitute.For<ICommand>();
            sut = new JarvisDefaultCommandExecutionExceptionHelper(NullLogger.Instance, 200, MaximumNumberOfRetry);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Retry_when_concurrency_exception_is_thrown(Boolean wrapInAggregateException)
        {
            Exception cex = PrepareConcurrencyException(wrapInAggregateException);

            var result = sut.Handle(cex, command, 5, out retry, out replyCommand);
            Assert.IsTrue(result, "Concurrency exception is handled, no need to rethrow");
            Assert.IsTrue(retry, "We should retry execution.");
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Retry_when_too_manyconcurrency_exception_is_thrown(Boolean wrapInAggregateException)
        {
            Exception cex = PrepareConcurrencyException(wrapInAggregateException);
            var result = sut.Handle(cex, command, MaximumNumberOfRetry + 1, out retry, out replyCommand);
            Assert.IsTrue(result, "Concurrency exception is handled, no need to rethrow");
            Assert.IsFalse(retry, "Too many retry, we should stop retrying execution.");
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Domain_exception_should_continue_executing_without_retrying(Boolean wrapInAggregateException)
        {
            Exception dex = PrepareDomainException(wrapInAggregateException);

            var result = sut.Handle(dex, command, 5, out retry, out replyCommand);
            Assert.IsTrue(result, "DomainException is handled, no need to rethrow");
            Assert.IsFalse(retry, "We should not retry execution, a domain exception will always fail upon retrying.");
            Assert.IsNotNull(replyCommand, "Domain exception should reply notification");
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Security_exception_should_continue_executing_without_retrying(Boolean wrapInAggregateException)
        {
            Exception dex = PrepareSecurityException(wrapInAggregateException);

            var result = sut.Handle(dex, command, 5, out retry, out replyCommand);
            Assert.IsTrue(result, "Security is handled, no need to rethrow");
            Assert.IsFalse(retry, "We should not retry execution, a Security exception will always fail upon retrying.");
            Assert.IsNotNull(replyCommand, "Security exception should reply notification");
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Unknown_exception_should_block_execution(Boolean wrapInAggregateException)
        {
            Exception dex = PrepareGenericException(wrapInAggregateException);

            var result = sut.Handle(dex, command, 5, out retry, out replyCommand);
            Assert.IsFalse(result, "Generic exception is not handled, should rethrow");
            Assert.IsFalse(retry, "We should not retry execution, unknown exception should fail the handler (and optionally use the retry of the bus or handler.");
        }

        private static Exception PrepareConcurrencyException(bool wrapInAggregateException)
        {
            Exception cex = new ConcurrencyException("TEST");
            if (wrapInAggregateException)
                cex = new AggregateException(cex);
            return cex;
        }

        private static Exception PrepareDomainException(bool wrapInAggregateException)
        {
            Exception cex = new DomainException(new SampleAggregateId(1));
            if (wrapInAggregateException)
                cex = new AggregateException(cex);
            return cex;
        }

        private static Exception PrepareGenericException(bool wrapInAggregateException)
        {
            Exception cex = new ApplicationException("TEST");
            if (wrapInAggregateException)
                cex = new AggregateException(cex);
            return cex;
        }

        private static Exception PrepareSecurityException(bool wrapInAggregateException)
        {
            Exception sex = new SecurityException("TEST");
            if (wrapInAggregateException)
                sex = new AggregateException(sex);
            return sex;
        }
    }
}
