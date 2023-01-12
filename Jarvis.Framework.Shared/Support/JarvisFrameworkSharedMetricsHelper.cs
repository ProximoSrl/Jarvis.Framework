using App.Metrics;
using App.Metrics.Counter;
using App.Metrics.Timer;
using Jarvis.Framework.Shared.Commands;
using Jarvis.Framework.Shared.Exceptions;
using System;

namespace Jarvis.Framework.Shared.Support
{
    internal static class JarvisFrameworkSharedMetricsHelper
    {
        private static readonly CounterOptions ConcurrencyExceptions = new CounterOptions()
        {
            Name = "FrameworkConcurrencyExceptions",
            MeasurementUnit = Unit.Events
        };

        private static readonly CounterOptions DomainExceptions = new CounterOptions()
        {
            Name = "FrameworkDomainExceptions",
            MeasurementUnit = Unit.Events
        };

        private static readonly CounterOptions SecurityExceptions = new CounterOptions()
        {
            Name = "FrameworkSecurityExceptions",
            MeasurementUnit = Unit.Events
        };

        private static readonly CounterOptions CommandsExecuted = new CounterOptions()
        {
            Name = "FrameworkCommandsExecuted",
            MeasurementUnit = Unit.Commands
        };

        private static readonly TimerOptions CommandTimer = new TimerOptions
        {
            Name = "FrameworkCommandsExecution",
            MeasurementUnit = Unit.Requests,
            DurationUnit = TimeUnit.Milliseconds,
            RateUnit = TimeUnit.Milliseconds
        };

        public static void MarkConcurrencyException(ICommand command)
        {
            JarvisFrameworkMetricsHelper.Counter.Increment(ConcurrencyExceptions, 1, command.GetType().Name);
        }

        public static void MarkDomainException(ICommand command, DomainException exception)
        {
            JarvisFrameworkMetricsHelper.Counter.Increment(DomainExceptions, 1, command.GetType().Name);
        }

        public static void MarkSecurityException(ICommand command)
        {
            JarvisFrameworkMetricsHelper.Counter.Increment(SecurityExceptions, 1, command.GetType().Name);
        }

        public static void MarkCommandExecuted(ICommand command)
        {
            JarvisFrameworkMetricsHelper.Counter.Increment(CommandsExecuted, 1, command.GetType().Name);
        }


        public static TimerContext StartCommandTimer(ICommand command)
        {
            return JarvisFrameworkMetricsHelper.Timer.Time(CommandTimer, command.GetType().Name);
        }
    }
}
