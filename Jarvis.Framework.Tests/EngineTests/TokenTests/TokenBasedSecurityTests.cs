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
        protected static Token LockToken = new Token("user_1_lock");
    }

    [Subject("lock")]
    public class lock_file : token_tests
    {
        Establish context = () => Create(new FileId(1));
        Because of = () => Aggregate.Lock(LockToken);
        It is_locked = () => State.IsLocked.ShouldBeTrue();
    }

    [Subject("With a locked file")]
    public class when_unlock_file_with_grant : token_tests
    {
        Establish context = () =>
        {
            Create(new FileId(1));
            Aggregate.Lock(LockToken);

            Aggregate.AddContextGrant(
                FileAggregate.LockGrant,
                LockToken
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
            Aggregate.Lock(LockToken);

            Aggregate.AddContextGrant(
                FileAggregate.LockGrant,
                new Token("123")
            );
        };

        Because of = () => _ex = Catch.Exception(()=> Aggregate.UnLock());
        It should_throw_security_exception = () =>
        {
            _ex.ShouldNotBeNull();
            _ex.ShouldBeAssignableTo<MissingGrantException>();

        };
    }  
    
    [Subject("With a locked file")]
    public class when_a_new_lock_is_requested : token_tests
    {
        private static Exception _ex;
        Establish context = () =>
        {
            Create(new FileId(1));
            Aggregate.Lock(new Token("user_2_lock"));
        };

        Because of = () => _ex = Catch.Exception(()=> Aggregate.Lock(LockToken));

        private It should_throw_security_exception = () =>
        {
            _ex.ShouldNotBeNull();
            _ex.ShouldBeAssignableTo<GrantViolationException>();
        };
    }
}