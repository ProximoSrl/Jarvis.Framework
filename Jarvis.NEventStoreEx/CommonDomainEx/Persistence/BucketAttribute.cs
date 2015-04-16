using System;

namespace Jarvis.NEventStoreEx.CommonDomainEx.Persistence
{
    public class BucketAttribute : Attribute
    {
        public string BucketId { get; private set; }

        public BucketAttribute(string bucketId)
        {
            BucketId = bucketId;
        }
    }
}
