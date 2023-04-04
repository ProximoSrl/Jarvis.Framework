using Jarvis.Framework.Shared.Messages;
using MongoDB.Bson;
using System;

namespace Jarvis.Framework.Shared.Commands.Tracking
{
    /// <summary>
    /// Class used to track <see cref="ICommand"/> execution status.
    /// </summary>
    public class TrackedMessageModel
    {
        public ObjectId Id { get; set; }

        public string MessageId { get; set; }

        /// <summary>
        /// If present indicates when the message will expire and will be deleted by the
        /// mongodb database. Until the value is null mongod will not evict the message.
        /// </summary>
        public DateTime? ExpireDate { get; set; }

        /// <summary>
        /// This is populated only if the command is an instance
        /// of <see cref="Command{TIdentity}"/>, in all other situation 
        /// is null.
        /// </summary>
        public String AggregateId { get; set; }

        /// <summary>
        /// <para>Identifies the Type of message (Command, Event, etc... for easier queries)</para>
        /// <para>It's nullable because this field was added at a later time</para>
        /// </summary>
        public TrackedMessageType? Type { get; set; }

        /// <summary>
        /// the type of the message in string format
        /// </summary>
        public String MessageType { get; set; }

        /// <summary>
        /// <para>
        /// Timestamp when message is "started", with bus it is the time the message is sent to the bus
        /// this is the timestamp the message is generated.
        /// </para>
        /// <para>
        /// This information is valid for:
        /// - Commands
        /// - Events
        /// </para>
        /// </summary>
        public DateTime StartedAt { get; set; }

        /// <summary>
        /// <para>
        /// This is an array because the command can have retries, due to conflicts. This property stores
        /// all the execution start time for the command
        /// </para>
        /// <para>
        /// This information is valid for:
        /// - Commands
        /// </para>
        /// </summary>
        public DateTime[] ExecutionStartTimeList { get; set; }

        /// <summary>
        /// <para>Last execution start time. </para>
        /// <para>
        /// This information is valid for:
        /// - Commands
        /// </para>
        /// </summary>
        public DateTime? LastExecutionStartTime { get; set; }

        /// <summary>
        /// <para>
        /// Set when the elaboration start, a command can then:
        /// - complete with success (when CompletedAt is set)
        /// - complete with a failure (when FailedAt is set)
        /// - pending: if this is set but this is not marked as completed or failed
        /// </para>
        /// <para>In case of retries, this value is greater than 1 </para>
        /// <para>
        /// This information is valid for:
        /// - Commands
        /// </para>
        /// </summary>
        public Int32 ExecutionCount { get; set; }

        /// <summary>
        /// <para>Time of completion of the command.</para>
        /// <para>
        /// This information is valid for:
        /// - Commands
        /// </para>
        /// </summary>
        public DateTime? CompletedAt { get; set; }

        /// <summary>
        /// <para>Time of final dispatch of the command, this is the last message.</para>
        /// <para>
        /// This information is valid for:
        /// - Commands
        /// </para>
        /// </summary>
        public DateTime? DispatchedAt { get; set; }

        public IMessage Message { get; set; }

        public string Description { get; set; }

        public string IssuedBy { get; set; }

        /// <summary>
        /// <para>Most recent error</para>
        /// <para>
        /// This information is valid for:
        /// - Commands
        /// </para>
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Needed to really understand source of full exception, <see cref="ErrorMessage"/>
        /// property is simply the message of the exception, but the full exception will be
        /// needed to better troubleshoot the problem.
        /// </summary>
        public string FullException { get; set; }

        /// <summary>
        /// <para>True when the command is completed.</para>
        /// <para>
        /// This information is valid for:
        /// - Commands
        /// </para>
        /// </summary>
        public Boolean? Completed { get; set; }

        /// <summary>
        /// <para>True if the command completed successfully.</para>
        /// <para>
        /// This information is valid for:
        /// - Commands
        /// </para>
        /// </summary>
        public Boolean? Success { get; set; }
    }
}
