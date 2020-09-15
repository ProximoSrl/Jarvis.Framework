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
            protected override Task Execute(SimpleCommand cmd)
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
            protected override Task Execute(AdminCommand cmd)
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
