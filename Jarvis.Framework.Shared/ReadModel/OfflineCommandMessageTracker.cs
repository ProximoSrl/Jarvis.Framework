using Jarvis.Framework.Shared.Commands;
using Jarvis.Framework.Shared.Messages;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jarvis.Framework.Shared.ReadModel
{
    public class OfflineCommandInfo
    {
        public OfflineCommandInfo(ICommand command, Boolean success, DateTime completionDate)
        {
            Id = command.MessageId.ToString();
            Command = command;
            Completed = true;
            Success = success;
            CompletionDate = completionDate;
            ConflictingCommands = new List<ConflictingCommandInfo>();
        }

        public String Id { get; private set; }

        public ICommand Command { get; private set; }

        public Boolean Completed { get; private set; }

        public Boolean Success { get; private set; }

        public DateTime CompletionDate { get; private set; }

        public OfflineCommandSynchronizingStatus SynchronizingStatus { get; set; }

        /// <summary>
        /// True if this command was marked as to skip for syncronization.
        /// </summary>
        public Boolean SkipSynchronization { get; set; }

        /// <summary>
        /// Store a list of conflicting commands, that are loaded from the
        /// main site.
        /// </summary>
        public List<ConflictingCommandInfo> ConflictingCommands { get; set; }
    }

    /// <summary>
    /// This is the dto of a command that was executed in the online
    /// system and that generate conflicts with offline command
    /// </summary>
    public class ConflictingCommandInfo
    {
        public String Id { get; set; }

        public String AggregateId { get; set; }

        public String Describe { get; set; }

        public DateTime TimeStamp { get; set; }

        public String UserId { get; set; }
    }

    public enum OfflineCommandSynchronizingStatus
    {
        NotSynchronized = 0,
        Synchronizing = 1,
        SynchronizationOk = 2,
        SynchronizationFailed = 3,
    }

    /// <summary>
    /// An interface to manage <see cref="OfflineCommandInfo" /> class and to 
    /// manage command synchronization.
    /// </summary>
    public interface IOfflineCommandManager
    {
        /// <summary>
        /// Return all the offline commands executed by the system.
        /// </summary>
        /// <returns></returns>
        List<OfflineCommandInfo> GetOfflineCommandExecuted();

        /// <summary>
        /// Command was sent to the remote system, we are waiting for syncronization.
        /// </summary>
        /// <param name="id"></param>
        Boolean MarkCommandAsSynchronizing(String id);

        /// <summary>
        /// Mark a given command as syncronized.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="success">true if the execution was successfu</param>
        Boolean MarkCommandAsSynchronized(String id, Boolean success);

        /// <summary>
        /// Command was sent to the remote system, we are waiting for syncronization.
        /// </summary>
        /// <param name="id">Id of the command.</param>
        /// <param name="skipSynchronization">True if command should me marked as to skip
        /// false if you want to Synchronize the command.</param>
        Boolean SetSkipSynchronizationFlag(String id, Boolean skipSynchronization);

        /// <summary>
        /// Return a specific command id.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        OfflineCommandInfo GetById(String id);

        /// <summary>
        /// Set all conflicting commands.
        /// </summary>
        /// <param name="commands"></param>
        /// <param name="id"></param>
        void SetConflictingCommands(String id, IEnumerable<ConflictingCommandInfo> commands);
    }

    /// <summary>
    /// A decorator of a standard IMessageTracker that will 
    /// be used to save more information for offline commands
    /// that are executed.
    /// </summary>
    public class OfflineCommandMessageTracker : IMessagesTracker, IOfflineCommandManager
    {
        private readonly IMessagesTracker _originalTracker;
        private IMongoCollection<OfflineCommandInfo> _offlineCommandCollection;

        public OfflineCommandMessageTracker(IMessagesTracker originalTracker, IMongoDatabase logDb)
        {
            _originalTracker = originalTracker;
            _offlineCommandCollection = logDb.GetCollection<OfflineCommandInfo>("OfflineCommands");

        }

        #region IMessageTracker interface

        public void Completed(ICommand command, DateTime completedAt)
        {
            _originalTracker.Completed(command, completedAt);
            _offlineCommandCollection.FindOneAndReplace(
               Builders<OfflineCommandInfo>.Filter.Eq(m => m.Id, command.MessageId.ToString()),
               new OfflineCommandInfo(command, true, completedAt),
               new FindOneAndReplaceOptions<OfflineCommandInfo, OfflineCommandInfo>() { IsUpsert = true });
        }

        public bool Dispatched(Guid messageId, DateTime dispatchedAt)
        {
            return _originalTracker.Dispatched(messageId, dispatchedAt);
        }

        public void Drop()
        {
            _originalTracker.Drop();
        }

        public void ElaborationStarted(ICommand command, DateTime startAt)
        {
            _originalTracker.ElaborationStarted(command, startAt);
        }

        public void Failed(ICommand command, DateTime failedAt, Exception ex)
        {
            _originalTracker.Failed(command, failedAt, ex);
            _offlineCommandCollection.FindOneAndReplace(
                Builders<OfflineCommandInfo>.Filter.Eq(m => m.Id, command.MessageId.ToString()),
                new OfflineCommandInfo(command, false, failedAt),
                new FindOneAndReplaceOptions<OfflineCommandInfo, OfflineCommandInfo>() { IsUpsert = true });
        }

        public void Started(IMessage msg)
        {
            _originalTracker.Started(msg);
        }

        #endregion

        #region IOfflineCommandManager

        public List<OfflineCommandInfo> GetOfflineCommandExecuted()
        {
           return _offlineCommandCollection.AsQueryable()
                .OrderBy(m => m.CompletionDate)
                .ToList();
        }

        public Boolean MarkCommandAsSynchronizing(string id)
        {
            var result = _offlineCommandCollection.UpdateOne(
              Builders<OfflineCommandInfo>.Filter.Eq(m => m.Id, id),
              Builders<OfflineCommandInfo>.Update
                  .Set(m => m.SynchronizingStatus, OfflineCommandSynchronizingStatus.Synchronizing));

            return result.ModifiedCount == 1;
        }

        public Boolean MarkCommandAsSynchronized(String id, Boolean success)
        {
            var newStatus = success ? OfflineCommandSynchronizingStatus.SynchronizationOk : OfflineCommandSynchronizingStatus.SynchronizationFailed;
            var result =  _offlineCommandCollection.UpdateOne(
                Builders<OfflineCommandInfo>.Filter.Eq(m => m.Id, id),
                Builders<OfflineCommandInfo>.Update
                    .Set(m => m.SynchronizingStatus, newStatus));

            return result.ModifiedCount == 1;
        }

   
        public OfflineCommandInfo GetById(String id)
        {
            return _offlineCommandCollection.AsQueryable()
                 .Where(c => c.Id == id)
                 .FirstOrDefault();
        }

        public void SetConflictingCommands(String id, IEnumerable<ConflictingCommandInfo> commands)
        {
            _offlineCommandCollection.UpdateOne(
             Builders<OfflineCommandInfo>.Filter.Eq(m => m.Id, id),
             Builders<OfflineCommandInfo>.Update
                 .Set(m => m.ConflictingCommands, commands.ToList()));
        }

        public Boolean SetSkipSynchronizationFlag(string id, bool skipSynchronization)
        {
            var result =  _offlineCommandCollection.UpdateOne(
               Builders<OfflineCommandInfo>.Filter.Eq(m => m.Id, id),
               Builders<OfflineCommandInfo>.Update
                   .Set(m => m.SkipSynchronization, skipSynchronization));

            return result.ModifiedCount == 1;
        }
        #endregion
    }
}
