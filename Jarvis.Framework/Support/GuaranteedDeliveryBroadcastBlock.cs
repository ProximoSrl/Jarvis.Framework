using App.Metrics;
using Jarvis.Framework.Shared.Exceptions;
using Jarvis.Framework.Shared.Support;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks.Dataflow;

namespace Jarvis.Framework.Kernel.Support
{
    public static class GuaranteedDeliveryBroadcastBlock
    {
        public static ActionBlock<T> Create<T>(
            IEnumerable<ITargetBlock<T>> targets,
            String commitPollingClientId,
            Int32 boundedCapacity)
        {
            var options = new ExecutionDataflowBlockOptions();
            if (boundedCapacity > 0)
            {
                options.BoundedCapacity = boundedCapacity;
            }
            var meter = Metric.Meter($"GuaranteedDeliveryBroadcastBlock-{commitPollingClientId}", Unit.Items, TimeUnit.Seconds);
            var actionBlock = new ActionBlock<T>(
                async item =>
                {
                    foreach (var target in targets)
                    {
                        Int32 errorCount = 0;
                        while (!(await target.SendAsync(item).ConfigureAwait(false)))
                        {
                            Thread.Sleep(1000); //give some time to free some resource.
                            if (errorCount > 2)
                            {
                                throw new JarvisFrameworkEngineException("GuaranteedDeliveryBroadcastBlock: Unable to send message to a target id " + commitPollingClientId);
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
