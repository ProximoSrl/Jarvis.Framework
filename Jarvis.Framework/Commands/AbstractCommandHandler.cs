using Castle.Core.Logging;
using Jarvis.Framework.Shared;
using Jarvis.Framework.Shared.Claims;
using Jarvis.Framework.Shared.Commands;
using Jarvis.Framework.Shared.Events;
using Jarvis.Framework.Shared.Logging;
using Jarvis.Framework.Shared.Messages;
using Jarvis.Framework.Shared.Support;
using NStore.Domain;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Threading;
using System.Threading.Tasks;

namespace Jarvis.Framework.Kernel.Commands
{
    public static class  CommandHandlerMonitor
    {
        /// <summary>
        /// Added the ability
        /// </summary>
        private static ConcurrentDictionary<ICommand, DateTime  > ExecutingCommands { get; set; } = new ConcurrentDictionary<ICommand, DateTime>();

        public static void AddCommand(ICommand command)
        {
            ExecutingCommands[command] = DateTime.UtcNow;
        }

        public static void ReleaseCommand(ICommand command)
        {
            ExecutingCommands[command] = DateTime.MinValue;
            ExecutingCommands.TryRemove(command, out _);
        }

        public static int GetExecutingCommandCount() => ExecutingCommands.Count;

        public static (ICommand Command, DateTime StartedAt)[] GetExecutingCommands() => ExecutingCommands.Select(k => (k.Key, k.Value)).ToArray();
    }

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
            CommandHandlerMonitor.AddCommand(cmd);
            try
            {
                var claims = await GetCurrentClaimsAsync(cmd).ConfigureAwait(false);
                using (var x = SecurityContextManager.SetCurrentClaims(claims))
                {
                    await OnCheckSecurityAsync(cmd).ConfigureAwait(false);

                    using (var context = JarvisFrameworkSharedMetricsHelper.StartCommandTimer(cmd))
                    {
                        await Execute(cmd).ConfigureAwait(false);
                        JarvisFrameworkSharedMetricsHelper.MarkCommandExecuted(cmd);
                    }
                }
            }
            finally
            {
                //Remove from the list of executing command.
                CommandHandlerMonitor.ReleaseCommand(cmd);
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
            try
            {
                LoggerThreadContextManager.MarkCommandExecution(command);
                await HandleAsync((TCommand)command).ConfigureAwait(false);
            }
            finally
            {
                LoggerThreadContextManager.ClearMarkCommandExecution();
            }
        }

        /// <summary>
        /// Version 6.5.3 made this method virtual so specific command hanlder can change how headers
        /// are stored, this is especially useful to avoid storing plain command in the same commit 
        /// and instead using some proxy.
        /// </summary>
        /// <param name="headersAccessor"></param>
        /// <param name="currentCommand"></param>
        protected virtual void StoreCommandHeaders(IHeadersAccessor headersAccessor, ICommand currentCommand)
        {
            foreach (var key in currentCommand.AllContextKeys)
            {
                headersAccessor.Add(key, currentCommand.GetContextData(key));
            }
            OnStoreCommandDataIntoHeaders(headersAccessor, currentCommand);
            headersAccessor.Add(ChangesetCommonHeaders.Timestamp, DateTime.UtcNow);
            OnStoreCommandHeaders(headersAccessor);
        }

        /// <summary>
        /// Added in 6.5.3 allows for a special store of command information into commit header. This
        /// is different from <see cref="OnStoreCommandHeaders(IHeadersAccessor)"/> because this method
        /// can prevent / change storage of plain command into headers.
        /// </summary>
        /// <param name="headersAccessor"></param>
        /// <param name="currentCommand"></param>
        protected virtual void OnStoreCommandDataIntoHeaders(IHeadersAccessor headersAccessor, ICommand currentCommand)
        {
            headersAccessor.Add(ChangesetCommonHeaders.Command, currentCommand);
        }

        protected virtual void OnStoreCommandHeaders(IHeadersAccessor headersAccessor)
        {
            //Do nothing, give the ability to the concrete handler to add headers to this changeset.
        }

        public abstract Task ClearAsync();
    }
}