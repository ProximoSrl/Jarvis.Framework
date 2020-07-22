using Castle.MicroKernel;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Jarvis.Framework.Shared.Messaging
{
    /// <summary>
    /// This class implements both the receiver and the sender part, it is all based on mongodb
    /// </summary>
    public class MongoDbNotifierManager : AbstractNotifierManager, IJarvisNotificationBus
    {
        private IMongoCollection<NotificationMessage> _notificationCollection { get; set; }

        private readonly string _pollerId;

        public MongoDbNotifierManager(
            IKernel kernel,
            IMongoDatabase notificationDatabase,
            String collectionName,
            String pollerId) : base(kernel)
        {
            _notificationCollection = notificationDatabase.GetCollection<NotificationMessage>(collectionName);

            //Create a specific poller index that will index all the documents that does have not a value in polled list
            _notificationCollection.Indexes.CreateOne(new CreateIndexModel<NotificationMessage>(
                Builders<NotificationMessage>.IndexKeys
                    .Ascending(n => n.TimeStamp)
                    .Ascending("PolledList"),
                new CreateIndexOptions<NotificationMessage>()
                {
                    Name = "PollerFor" + _pollerId,
                    //PartialFilterExpression = Builders<NotificationMessage>.Filter.Nin("PolledList", _pollerId),
                }));
            _pollerId = pollerId;
        }

        public Task Publish(object message)
        {
            return _notificationCollection.InsertOneAsync(new NotificationMessage(message));
        }

        private Timer _timer;
        private Int64 _pollerGate = 0;

        public override Task StartPollingAsync()
        {
            if (_timer != null)
            {
                _timer = new Timer(TimerCallBack, null, 0, 300);
            }
            return Task.CompletedTask;
        }

        public Boolean ForcePoll() 
        {
            return InnerPolling();
        }

        private void TimerCallBack(object state)
        {
            InnerPolling();
        }

        private Boolean InnerPolling()
        {
            if (Interlocked.CompareExchange(ref _pollerGate, 1, 0) == 0)
            {
                try
                {
                    NotificationMessage notificationMessage = null;
                    do
                    {
                        notificationMessage = _notificationCollection.FindOneAndUpdate(
                            Builders<NotificationMessage>.Filter.Ne("PolledList", _pollerId),
                            Builders<NotificationMessage>.Update.Push("PolledList", _pollerId),
                            new FindOneAndUpdateOptions<NotificationMessage, NotificationMessage>()
                            {
                                Sort = Builders<NotificationMessage>.Sort.Ascending(f => f.TimeStamp),
                            });
                        if (notificationMessage != null)
                        {
                            Consume(notificationMessage.Message).Wait();
                        }
                    } while (notificationMessage != null);
                }
                catch (Exception ex)
                {
                    Logger.ErrorFormat(ex, "Error while polling notification manager");
                }
                finally
                {
                    Interlocked.Exchange(ref _pollerGate, 0);
                }
                return true;
            }

            return false;
        }

        public override Task StopPollingAsync()
        {
            if (_timer != null)
            {
                _timer.Dispose();
                _timer = null;
            }

            return Task.CompletedTask;
        }

        private class NotificationMessage
        {
            public NotificationMessage(object message)
            {
                Id = ObjectId.GenerateNewId();
                TimeStamp = DateTime.UtcNow;
                Message = message;
            }

            public ObjectId Id { get; private set; }

            public DateTime TimeStamp { get; private set; }

            public Object Message { get; private set; }

            [BsonElement]
            private String[] PolledList { get; set; } = Array.Empty<string>();
        }
    }
}
