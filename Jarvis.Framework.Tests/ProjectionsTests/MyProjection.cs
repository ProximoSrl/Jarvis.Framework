using Jarvis.Framework.Kernel.Events;
using Jarvis.Framework.Kernel.ProjectionEngine;
using MongoDB.Driver;


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

        private IndexKeysDefinition<MyReadModel> IndexKeys = Builders<MyReadModel>.IndexKeys.Ascending(x => x.Text);
        public const string IndexName = "MyIndex";

        public MyProjection(ICollectionWrapper<MyReadModel, string> collection)
        {
            _collection = collection;
        }

        public override void SetUp()
        {
            _collection.CreateIndex(IndexName, IndexKeys);
        }

        public override void Drop ()
        {
            _collection.Drop();
        }

        public void On(InsertEvent e)
        {
            _collection.Insert(e, new MyReadModel()
            {
                Id = e.AggregateId,
                Text = e.Text
            });
        }

        public void On(UpdateEvent e)
        {
            _collection.FindAndModify(e, x => x.Id == e.AggregateId, m =>
            {
                m.Text = e.Text;
            });
        }

        public void On(DeleteEvent delete)
        {
            _collection.Delete(delete, delete.AggregateId);
        }
    }
}