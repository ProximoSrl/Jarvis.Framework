using Castle.Core.Logging;
using Jarvis.Framework.Shared;
using Jarvis.Framework.Shared.Claims;
using Jarvis.Framework.Shared.Commands;
using Jarvis.Framework.Shared.Logging;
using Jarvis.Framework.Shared.Messages;
using Jarvis.Framework.Shared.Support;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Threading.Tasks;

namespace Jarvis.Framework.Kernel.Commands
{
    public abstract class AbstractCommandHandler<TCommand> : ICommandHandler<TCommand> where TCommand : ICommand
    {
        public ILogger Logger { get; set; }

        public ILoggerThreadContextManager LoggerThreadContextManager { get; set; }

        /// <summary>
        /// Security context provider is used to check security of the current command (if necessary and if the 
        /// real command handler want to check security).
        /// </summary>
        public ISecurityContextManager SecurityContextManager { get; set; }

        protected AbstractCommandHandler()
        {
            Logger = NullLogger.Instance;
            LoggerThreadContextManager = NullLoggerThreadContextManager.Instance;
            SecurityContextManager = NullSecurityContextManager.Instance;
        }

        public virtual async Task HandleAsync(TCommand cmd)
        {
            var claims = await GetCurrentClaimsAsync(cmd).ConfigureAwait(false);
            using (var x = SecurityContextManager.SetCurrentClaims(claims))
            {
                await OnCheckSecurityAsync(cmd).ConfigureAwait(false);
                if (JarvisFrameworkGlobalConfiguration.MetricsEnabled)
                {
                    using (var context = SharedMetricsHelper.CommandTimer.NewContext(cmd.GetType().Name))
                    {
                        await Execute(cmd).ConfigureAwait(false);
                        SharedMetricsHelper.CommandCounter.Increment(cmd.GetType().Name, context.Elapsed.Milliseconds);
                    }
                }
                else
                {
                    await Execute(cmd).ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// This should be overridden by concrete AbstractCommandHandler 
        /// </summary>
        /// <param name="cmd"></param>
        /// <returns></returns>
        protected virtual Task<IEnumerable<Claim>> GetCurrentClaimsAsync(TCommand cmd)
        {
            return Task.FromResult<IEnumerable<Claim>>(Array.Empty<Claim>());
        }

        /// <summary>
        /// Check basic security model based on attribute, it is virtual because concrete class
        /// can add custom security not based on claims model.
        /// </summary>
        /// <param name="cmd"></param>
        /// <returns></returns>
        protected Task OnCheckSecurityAsync(TCommand cmd)
        {
            var allCustomAttributes = cmd.GetType().GetCustomAttributes(true);
            var claims = SecurityContextManager.GetCurrentClaims();
            foreach (ClaimAttribute claimAttribute in allCustomAttributes.Where(_ => (_ is ClaimAttribute)))
            {
                var claimMatcher = claimAttribute.Build();
                if (!claimMatcher.Matches(claims))
                    throw new SecurityException($"Claim missing {claimAttribute.Describe()} for command user {cmd.GetContextData(MessagesConstants.UserId)}");
            }
            return Task.CompletedTask;
        }

        protected abstract Task Execute(TCommand cmd);

        public async Task HandleAsync(ICommand command)
        {
            using (LoggerThreadContextManager.MarkCommandExecution(command))
            {
                await HandleAsync((TCommand)command).ConfigureAwait(false);
            }
        }
    }
}