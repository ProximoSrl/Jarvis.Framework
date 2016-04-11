//using MongoDB.Bson;
//using MongoDB.Driver;

//namespace Jarvis.Framework.Kernel.Store
//{
//    public static class MongoDbSnapshotsHelper 
//    {
//        public static void Drop(IMongoDatabase eventStoreDb)
//        {
//            var script = new BsonJavaScript(
//@"db.Snapshots.remove({});
//var cursor = db.Streams.find({'SnapshotRevision' : { $gt : 0}}).sort({_id:1});
//var doc = null;

//do{
//    doc = cursor.hasNext() ? cursor.next() : null;
//    if(doc){
//        doc.Unsnapshotted = doc.HeadRevision;
//        doc.SnapshotRevision = 0;
//        db.Streams.save(doc);
//    }
//}while(doc != null);");

//            eventStoreDb.Eval(script);
//        }
//    }
//}
