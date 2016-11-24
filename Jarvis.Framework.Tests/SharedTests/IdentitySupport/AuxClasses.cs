using Jarvis.Framework.Shared.IdentitySupport;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jarvis.Framework.Tests.SharedTests.IdentitySupport
{
    public class TestMapper : AbstractIdentityTranslator<TestId>
    {
        public TestMapper(IMongoDatabase db, IIdentityGenerator identityGenerator) :
            base(db, identityGenerator)
        {

        }

        public void Addalias(TestId id, String value)
        {
            base.AddAlias(id, value);
        }

        public TestId Map(String value)
        {
            return Translate(value);
        }
    }

    public class TestFlatMapper : AbstractIdentityTranslator<TestFlatId>
    {
        public TestFlatMapper(IMongoDatabase db, IIdentityGenerator identityGenerator) :
            base(db, identityGenerator)
        {

        }

        public new void ReplaceAlias(TestFlatId id, String value)
        {
            base.ReplaceAlias(id, value);
        }


        public TestFlatId Map(String value)
        {
            return Translate(value);
        }
    }

    public class TestId : EventStoreIdentity
    {
        public TestId(string id)
            : base(id)
        {
        }

        public TestId(long id)
            : base(id)
        {
        }
    }

    public class TestFlatId : EventStoreIdentity
    {
        public TestFlatId(string id)
            : base(id)
        {
        }

        public TestFlatId(long id)
            : base(id)
        {
        }
    }
}
