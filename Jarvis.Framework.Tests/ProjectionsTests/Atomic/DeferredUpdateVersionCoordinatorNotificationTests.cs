using Jarvis.Framework.Kernel.ProjectionEngine.Atomic;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

namespace Jarvis.Framework.Tests.ProjectionsTests.Atomic
{
    [TestFixture]
    public class DeferredUpdateVersionCoordinatorNotificationTests
    {
        private static readonly MethodInfo ResetMethod = typeof(DeferredUpdateVersionCoordinator)
            .GetMethod("Reset", BindingFlags.NonPublic | BindingFlags.Static);

        [SetUp]
        public void SetUp()
        {
            ResetMethod.Invoke(null, null);
        }

        [TearDown]
        public void TearDown()
        {
            ResetMethod.Invoke(null, null);
        }

        [Test]
        public void Notification_includes_the_persisted_readmodel_type()
        {
            Type observed = null;
            DeferredUpdateVersionCoordinator.DeferredUpdatesPersisted += type => observed = type;

            DeferredUpdateVersionCoordinator.NotifyDeferredUpdatesPersisted(typeof(DeferredUpdateVersionCoordinatorNotificationTests));

            Assert.That(observed, Is.EqualTo(typeof(DeferredUpdateVersionCoordinatorNotificationTests)));
        }

        [Test]
        public void Persistence_lifecycle_notifications_preserve_type_and_order()
        {
            var observed = new List<string>();
            DeferredUpdateVersionCoordinator.DeferredUpdatesPersisting += type => observed.Add($"persisting:{type.Name}");
            DeferredUpdateVersionCoordinator.DeferredUpdatesPersisted += type => observed.Add($"persisted:{type.Name}");

            var type = typeof(DeferredUpdateVersionCoordinatorNotificationTests);
            DeferredUpdateVersionCoordinator.NotifyDeferredUpdatesPersisting(type);
            DeferredUpdateVersionCoordinator.NotifyDeferredUpdatesPersisted(type);

            Assert.That(observed, Is.EqualTo(new[]
            {
                $"persisting:{type.Name}",
                $"persisted:{type.Name}",
            }));
        }

        [Test]
        public void Failed_persistence_has_a_distinct_terminal_notification()
        {
            var observed = new List<string>();
            DeferredUpdateVersionCoordinator.DeferredUpdatesPersisting += type => observed.Add($"persisting:{type.Name}");
            DeferredUpdateVersionCoordinator.DeferredUpdatesPersisted += type => observed.Add($"persisted:{type.Name}");
            DeferredUpdateVersionCoordinator.DeferredUpdatesPersistenceFailed += type => observed.Add($"failed:{type.Name}");

            var type = typeof(DeferredUpdateVersionCoordinatorNotificationTests);
            DeferredUpdateVersionCoordinator.NotifyDeferredUpdatesPersisting(type);
            DeferredUpdateVersionCoordinator.NotifyDeferredUpdatesPersistenceFailed(type);

            Assert.That(observed, Is.EqualTo(new[]
            {
                $"persisting:{type.Name}",
                $"failed:{type.Name}",
            }));
        }

        [Test]
        public void Throwing_subscriber_does_not_block_other_subscribers()
        {
            var notified = false;
            DeferredUpdateVersionCoordinator.DeferredUpdatesPersisted += _ => throw new InvalidOperationException("subscriber failure");
            DeferredUpdateVersionCoordinator.DeferredUpdatesPersisted += _ => notified = true;

            Assert.DoesNotThrow(() =>
                DeferredUpdateVersionCoordinator.NotifyDeferredUpdatesPersisted(typeof(DeferredUpdateVersionCoordinatorNotificationTests)));
            Assert.That(notified, Is.True);
        }

        [Test]
        public async Task Async_persisting_notification_is_awaited_before_returning()
        {
            var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            DeferredUpdateVersionCoordinator.DeferredUpdatesPersistingAsync += async _ =>
            {
                entered.TrySetResult(true);
                await release.Task;
            };

            var notification = DeferredUpdateVersionCoordinator.NotifyDeferredUpdatesPersistingAsync(
                typeof(DeferredUpdateVersionCoordinatorNotificationTests));

            await entered.Task;
            Assert.That(notification.IsCompleted, Is.False,
                "Persistence must not start until every distributed intent handler has completed.");

            release.TrySetResult(true);
            await notification;
        }

        [Test]
        public async Task Throwing_async_subscriber_does_not_block_other_subscribers()
        {
            var notified = false;
            DeferredUpdateVersionCoordinator.DeferredUpdatesPersistedAsync +=
                _ => throw new InvalidOperationException("subscriber failure");
            DeferredUpdateVersionCoordinator.DeferredUpdatesPersistedAsync += _ =>
            {
                notified = true;
                return Task.CompletedTask;
            };

            Assert.DoesNotThrowAsync(async () =>
                await DeferredUpdateVersionCoordinator.NotifyDeferredUpdatesPersistedAsync(
                    typeof(DeferredUpdateVersionCoordinatorNotificationTests)));
            Assert.That(notified, Is.True);
        }

        [Test]
        public void Reset_removes_subscribers()
        {
            var notificationCount = 0;
            DeferredUpdateVersionCoordinator.DeferredUpdatesPersisting += _ => notificationCount++;
            DeferredUpdateVersionCoordinator.DeferredUpdatesPersisted += _ => notificationCount++;
            DeferredUpdateVersionCoordinator.DeferredUpdatesPersistenceFailed += _ => notificationCount++;

            ResetMethod.Invoke(null, null);
            DeferredUpdateVersionCoordinator.NotifyDeferredUpdatesPersisting(typeof(DeferredUpdateVersionCoordinatorNotificationTests));
            DeferredUpdateVersionCoordinator.NotifyDeferredUpdatesPersisted(typeof(DeferredUpdateVersionCoordinatorNotificationTests));
            DeferredUpdateVersionCoordinator.NotifyDeferredUpdatesPersistenceFailed(typeof(DeferredUpdateVersionCoordinatorNotificationTests));

            Assert.That(notificationCount, Is.Zero);
        }
    }
}
