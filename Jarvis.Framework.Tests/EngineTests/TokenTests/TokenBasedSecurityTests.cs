using Jarvis.Framework.TestHelpers;
using Machine.Specifications;
using NUnit.Framework;

namespace Jarvis.Framework.Tests.EngineTests.TokenTests
{
    [Subject("Token based security")]
    public abstract class token_tests : AggregateSpecification<FileAggregate, FileAggregateState>
    {
    }

    [Subject("lock")]
    public class lock_file : token_tests
    {
        Establish context = () =>
        {
            Create(new FileId(1));
        };

        Because of = () =>
        {
            Aggregate.Lock();
        };

        It is_locked = () =>
        {
            State.IsLocked.ShouldBeTrue();
        };
    }
}