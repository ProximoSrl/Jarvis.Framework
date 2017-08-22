using Jarvis.Framework.Kernel.Events;
using Jarvis.Framework.Kernel.ProjectionEngine;
using MongoDB.Driver;
using System.Threading.Tasks;

namespace Jarvis.Framework.Tests.ProjectionsTests
{
    [ProjectionInfo("MyProjection")]
    public class MyProjection :
        AbstractProjection,
        IEventHandler<InsertEvent>,
        IEventHandler<UpdateEvent>,
        IEventHandler<DeleteEvent>
    {
        readonly ICollectionWrapper<MyReadModel, string> _collection;

        private readonly IndexKeysDefinition<MyReadModel> IndexKeys =
            Builders<MyReadModel>.IndexKeys.Ascending(x => x.Text);

        public const string IndexName = "MyIndex";

        public MyProjection(ICollectionWrapper<MyReadModel, string> collection)
        {
            _collection = collection;
        }

        public override Task SetUpAsync()
        {
            return _collection.CreateIndexAsync(IndexName, IndexKeys);
        }

        public override Task DropAsync()
        {
            return _collection.DropAsync();
        }

        public Task On(InsertEvent e)
        {
            return _collection.InsertAsync(e, new MyReadModel()
            {
                Id = e.AggregateId,
                Text = e.Text
            });
        }

        public Task On(UpdateEvent e)
        {
            return _collection.FindAndModifyAsync(e, x => x.Id == e.AggregateId, m =>
            {
                m.Text = e.Text;
            });
        }

        public Task On(DeleteEvent delete)
        {
            return _collection.DeleteAsync(delete, delete.AggregateId);
        }
    }
}