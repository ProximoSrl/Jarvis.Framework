using Jarvis.Framework.Kernel.Commands;
using Jarvis.Framework.Shared.Claims;
using Jarvis.Framework.Shared.Commands;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously

namespace Jarvis.Framework.Tests.Kernel.Commands
{
    [TestFixture]
    public class AbstractCommandHandlerSecurityTests
    {
        [Test]
        public async Task Verify_basic_security_with_correct_claim()
        {
            var handler = new SimpleCommandHandler();
            handler.SecurityContextManager = Substitute.For<ISecurityContextManager>();
            handler.SecurityContextManager.GetCurrentClaims().ReturnsForAnyArgs(new Claim[] {
                new Claim("pippo", "true"),
            });

            //invoke the handler, evereything should go ok
            await handler.HandleAsync(new SimpleCommand()).ConfigureAwait(false);
        }

        [Test]
        public async Task Verify_claim_with_wrong_value_throws()
        {
            var handler = new SimpleCommandHandler();
            handler.SecurityContextManager = Substitute.For<ISecurityContextManager>();
            handler.SecurityContextManager.GetCurrentClaims().ReturnsForAnyArgs(new Claim[] {
                new Claim("pippo", "false"),
            });

            //invoke the handler, evereything should go ok
            Assert.ThrowsAsync<SecurityException>(async () => await handler.HandleAsync(new SimpleCommand()).ConfigureAwait(false));
        }

        [Test]
        public async Task Verify_missing_claim_throws()
        {
            var handler = new SimpleCommandHandler();
            handler.SecurityContextManager = Substitute.For<ISecurityContextManager>();
            handler.SecurityContextManager.GetCurrentClaims().ReturnsForAnyArgs(new Claim[] {
                new Claim("paperone", "true"),
            });

            //invoke the handler, evereything should go ok
            Assert.ThrowsAsync<SecurityException>(async () => await handler.HandleAsync(new SimpleCommand()).ConfigureAwait(false));
        }

        [Test]
        public async Task Verify_required_role()
        {
            var handler = new AdminCommandHandler();
            handler.SecurityContextManager = Substitute.For<ISecurityContextManager>();
            handler.SecurityContextManager.GetCurrentClaims().ReturnsForAnyArgs(new Claim[] {
                new Claim("role", "admin"),
            });

            //invoke the handler, evereything should go ok
            await handler.HandleAsync(new AdminCommand()).ConfigureAwait(false);
        }

        [Test]
        public async Task Verify_required_role_with_multiple_roles()
        {
            var handler = new AdminCommandHandler();
            handler.SecurityContextManager = Substitute.For<ISecurityContextManager>();
            handler.SecurityContextManager.GetCurrentClaims().ReturnsForAnyArgs(new Claim[] {
                new Claim("role", "pluto"),
                new Claim("role", "admin"),
            });

            //invoke the handler, evereything should go ok
            await handler.HandleAsync(new AdminCommand()).ConfigureAwait(false);
        }

        private class SimpleCommandHandler : AbstractCommandHandler<SimpleCommand>
        {
            public override Task ClearAsync(CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }

            protected override Task Execute(SimpleCommand cmd, CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }
        }

        [RequiredClaim("pippo", "true")]
        private class SimpleCommand : Command
        {
        }

        private class AdminCommandHandler : AbstractCommandHandler<AdminCommand>
        {
            public override Task ClearAsync(CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }

            protected override Task Execute(AdminCommand cmd, CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }
        }

        [RequiredRole("admin")]
        private class AdminCommand : Command
        {
        }
    }

#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
}
