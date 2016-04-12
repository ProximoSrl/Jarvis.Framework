using Jarvis.Framework.Shared.Storage;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

/// <summary>
/// http://www.nunit.org/index.php?p=setupFixture&r=2.4
/// </summary>
[SetUpFixture]
public class GlobalSetup
{
    [SetUp]
    public void SetUp()
    {
        MongoRegistration.RegisterMongoConversions(
            "NEventStore.Persistence.MongoDB"
            );
    }
}
