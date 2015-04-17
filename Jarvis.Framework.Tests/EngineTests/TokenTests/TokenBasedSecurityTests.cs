using System;
using Jarvis.Framework.Kernel.Engine;
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
        Establish context = () => Create(new FileId(1));
        Because of = () => Aggregate.Lock();
        It is_locked = () => State.IsLocked.ShouldBeTrue();
    }

    [Subject("With a locked file")]
    public class when_unlock_file_with_grant : token_tests
    {
        Establish context = () =>
        {
            Create(new FileId(1));
            Aggregate.Lock();

            var e = RaisedEvent<FileLocked>();
            Aggregate.AddContextGrant(
                new GrantName("file-lock"),
                new Token(e.MessageId.ToString())
            );
        };

        Because of = () => Aggregate.UnLock();
        It should_unlock = () => State.IsLocked.ShouldBeFalse();
    }

    [Subject("With a locked file")]
    public class when_unlock_file_without_grant : token_tests
    {
        private static Exception _ex;
        Establish context = () =>
        {
            Create(new FileId(1));
            Aggregate.Lock();

            Aggregate.AddContextGrant(
                new GrantName("file-lock"),
                new Token("123")
            );
        };

        Because of = () => _ex = Catch.Exception(()=> Aggregate.UnLock());
        It should_throw_security_exception = () => _ex.ShouldNotBeNull();
    }
}