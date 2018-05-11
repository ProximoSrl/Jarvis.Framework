using Jarvis.Framework.Kernel.ProjectionEngine.Rebuild;
using Jarvis.Framework.Shared.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks.Dataflow;


namespace Jarvis.Framework.Kernel.Support
{
    public static class SlotGuaranteedDeliveryBroadcastBlock
    {
        public static ActionBlock<UnwindedDomainEvent> Create(
            IEnumerable<SlotInfo<UnwindedDomainEvent>> slotInfoList,
            String commitPollingClientId,
            Int32 boundedCapacity)
        {
            var options = new ExecutionDataflowBlockOptions();
            if (boundedCapacity > 0)
            {
                options.BoundedCapacity = boundedCapacity;
            }
            var actionBlock = new ActionBlock<UnwindedDomainEvent>(
                async item =>
                {
                    var evtType = item.Event?.GetType();
                    if (evtType != null)
                    {
                        foreach (var slotInfo in slotInfoList.Where(_ => _.HandledDomainEvents.Contains(evtType)))
                        {
                            Int32 errorCount = 0;
                            while (!(await slotInfo.Target.SendAsync(item).ConfigureAwait(false)))
                            {
                                Thread.Sleep(1000); //give some time to free some resource.
                                if (errorCount > 2)
                                {
                                    throw new JarvisFrameworkEngineException("GuaranteedDeliveryBroadcastBlock: Unable to send message to a target id " + commitPollingClientId);
                                }
                                errorCount++;
                            }
                        }
                    }
                    else if (UnwindedDomainEvent.LastEvent == item)
                    {
                        //ok we reached the end, we need to dispatch this special event. 
                        //TODO: simplify this logic
                        foreach (var slotInfo in slotInfoList)
                        {
                            await slotInfo.Target.SendAsync(item).ConfigureAwait(false);
                        }
                    }
                }, options);

            actionBlock.Completion.ContinueWith(t =>
            {
                foreach (var slotInfo in slotInfoList)
                {
                    slotInfo.Target.Complete();
                }
            });
            return actionBlock;
        }

        public class SlotInfo<T>
        {
            public SlotInfo(ITargetBlock<T> target, string slotName, HashSet<Type> handledDomainEvents)
            {
                Target = target;
                SlotName = slotName;
                HandledDomainEvents = handledDomainEvents;
            }

            public ITargetBlock<T> Target { get; private set; }

            public String SlotName { get; private set; }

            public HashSet<Type> HandledDomainEvents { get; private set; }
        }
    }
}
