using Jarvis.Framework.Shared.Messages;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Jarvis.Framework.Shared.Commands.Tracking
{
    /// <summary>
    /// Interface used to track message traveling during
    /// queue, execution, etc, concrete instance of this interface
    /// is used also to send notifications
    /// </summary>
    public interface IMessagesTracker
    {
        /// <summary>
        /// A message (Command, Event, Something else) was sent to the bus,
        /// this is the first event that is raised.
        /// </summary>
        /// <param name="msg"></param>
        void Started(IMessage msg);

        /// <summary>
        /// <para>
        /// This is called from the real Command Handler adapted, it is the timestamp
        /// of the system when the message is going to be elaborated.
        /// </para>
        /// <para>
        /// It can be called multiple times, if command execution has conflicts and needs
        /// to have a retry.
        /// </para>
        /// </summary>
        /// <param name="command"></param>
        /// <param name="startAt"></param>
        void ElaborationStarted(ICommand command, DateTime startAt);

        /// <summary>
        /// Message was elaborated with success
        /// </summary>
        /// <param name="command"></param>
        /// <param name="completedAt"></param>
        void Completed(ICommand command, DateTime completedAt);

        /// <summary>
        /// Track a batch of commands as executed instantaneously.
        /// </summary>
        /// <param name="commands"></param>
        /// <param name="cancellationToken"></param>
        Task TrackBatchAsync(IReadOnlyCollection<ICommand> commands, CancellationToken cancellationToken = default);

        /// <summary>
        /// Dispatched is the status when the event related to the command is 
        /// dispatched by the INotifyCommitHandled in projection engine. This means
        /// that the command is executed then dispatched to the bus and if there
        /// is a Reply-to a reply command is sent.
        /// </summary>
        /// <param name="messageId"></param>
        /// <param name="dispatchedAt"></param>
        /// <returns></returns>
        bool Dispatched(Guid messageId, DateTime dispatchedAt);

        /// <summary>
        /// Drop the entire collection.
        /// </summary>
        void Drop();

        /// <summary>
        /// Message cannot be elaborated, some error prevents the message to be
        /// handled.
        /// </summary>
        /// <param name="command"></param>
        /// <param name="failedAt"></param>
        /// <param name="ex"></param>
        void Failed(ICommand command, DateTime failedAt, Exception ex);
    }
}
