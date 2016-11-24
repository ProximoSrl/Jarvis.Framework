using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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
            var actionBlock = new ActionBlock<T>(
                async item =>
                {
                    foreach (var target in targets)
                    {
                        Int32 errorCount = 0;
                        Boolean result;
                        while (result = await target.SendAsync(item) == false)
                        {
                            Thread.Sleep(1000); //give some time to free some resource.
                            if (errorCount > 2)
                            {
                                throw new Exception("GuaranteedDeliveryBroadcastBlock: Unable to send message to a target id " + commitPollingClientId);
                            }
                            errorCount++;
                        }
                    }
                }, options);

            actionBlock.Completion.ContinueWith(t =>
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
