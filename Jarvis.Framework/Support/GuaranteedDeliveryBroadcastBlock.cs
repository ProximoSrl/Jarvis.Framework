using App.Metrics;
using Jarvis.Framework.Shared.Exceptions;
using Jarvis.Framework.Shared.Support;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Jarvis.Framework.Kernel.Support
{
    /// <summary>
    /// An helper that creates a broadcast block that will send the message to all the targets
    /// using TPL dataflow
    /// </summary>
    public static class GuaranteedDeliveryBroadcastBlock
    {
        /// <summary>
        /// Create a broadcast using a list of TargetBlocks.
        /// </summary>
        /// <exception cref="JarvisFrameworkEngineException"></exception>
        public static ActionBlock<T> Create<T>(
            IEnumerable<ITargetBlock<T>> targets,
            String commitPollingClientId,
            Int32 boundedCapacity,
            Int32 secondsToWaitBeforeThrowError = 10,
            string meterName = null)
        {
            var options = new ExecutionDataflowBlockOptions();
            if (boundedCapacity > 0)
            {
                options.BoundedCapacity = boundedCapacity;
            }
            meterName = meterName ?? $"GuaranteedDeliveryBroadcastBlock-{commitPollingClientId}";
            var meter = JarvisFrameworkMetric.Meter(meterName, Unit.Items, TimeUnit.Seconds);
            var actionBlock = new ActionBlock<T>(
                async item =>
                {
                    foreach (var target in targets)
                    {
                        Int32 errorCount = 0;
                        while (!(await target.SendAsync(item).ConfigureAwait(false)))
                        {
                            //message was not sent to the target, we need to wait a little bit and retry, if we fail too many times we raise an exception.
                            await Task.Delay(5000);
                            if (errorCount > secondsToWaitBeforeThrowError)
                            {
                                throw new JarvisFrameworkEngineException("GuaranteedDeliveryBroadcastBlock" + meterName + ": Unable to send message to a target id " + commitPollingClientId + "  of type " + item.GetType());
                            }
                            errorCount++;
                        }
                    }
                    meter.Mark(1);
                }, options);

            actionBlock.Completion.ContinueWith(_ =>
            {
                foreach (var target in targets)
                {
                    target.Complete();
                }
            });
            return actionBlock;
        }
    }
}
