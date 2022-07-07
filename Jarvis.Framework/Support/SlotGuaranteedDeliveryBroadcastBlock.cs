using Jarvis.Framework.Kernel.ProjectionEngine.Rebuild;
using Jarvis.Framework.Shared.Events;
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
        /// <summary>
        /// This is the number of milliseconds we sleep if we cannot enqueue item in 
        /// the tpl chain due to backpressure.
        /// </summary>
        private const int _queueInTplWaitTime = 5000;

        /// <summary>
        /// If backpressure does not allow you to queue an item in TPL we wait for a _queueInTplWaitTime
        /// and 5 total cycle.
        /// </summary>
        private const int _queueSleepMaxIterationCount = 5;

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
                                Thread.Sleep(_queueInTplWaitTime); //give some time to free some resource.
                                if (errorCount > _queueSleepMaxIterationCount)
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

        /// <summary>
        /// This create an ActionBlock capable to dispatch a standard events to a slot. All the
        /// target of the event (list of projection) is contained as Tasrget in <paramref name="slotInfoList"/>
        /// parameter.
        /// </summary>
        /// <param name="slotInfoList">The list of all slots that we want to dispatch with this
        /// specific broadcaster.</param>
        /// <param name="commitPollingClientId"></param>
        /// <param name="boundedCapacity"></param>
        /// <returns></returns>
        /// <exception cref="JarvisFrameworkEngineException"></exception>
        public static ActionBlock<DomainEvent> CreateForStandardEvents(
            IEnumerable<SlotInfo<DomainEvent>> slotInfoList,
            String commitPollingClientId,
            Int32 boundedCapacity)
        {
            var options = new ExecutionDataflowBlockOptions();
            if (boundedCapacity > 0)
            {
                options.BoundedCapacity = boundedCapacity;
                //This is the default, but it is important to be sure that we have no parallelism here, each executor has a series of projection
                //that should be dispatched one by one .
                options.MaxDegreeOfParallelism = 1;
            }
            var actionBlock = new ActionBlock<DomainEvent>(
                async item =>
                {
                    if (item != null)
                    {
                        var evtType = item?.GetType();

                        if (LastEventMarker.Instance == item)
                        {
                            //ok we reached the end, we need to dispatch this special event. 
                            //TODO: simplify this logic
                            foreach (var slotInfo in slotInfoList)
                            {
                                await slotInfo.Target.SendAsync(item).ConfigureAwait(false);
                            }
                        }
                        else
                        {
                            //Cycle for each slot, each slot can have its own pace, for each slot we simply queue the
                            //event in the target property of the slotinfo.
                            foreach (var slotInfo in slotInfoList.Where(_ => _.HandledDomainEvents.Contains(evtType)))
                            {
                                Int32 errorCount = 0;
                                while (!(await slotInfo.Target.SendAsync(item).ConfigureAwait(false)))
                                {
                                    Thread.Sleep(_queueInTplWaitTime); //give some time to free some resource.
                                    if (errorCount > _queueSleepMaxIterationCount)
                                    {
                                        throw new JarvisFrameworkEngineException("GuaranteedDeliveryBroadcastBlock: Unable to send message to a target id " + commitPollingClientId);
                                    }
                                    errorCount++;
                                }
                            }
                        }
                    }
                }, options);

            actionBlock.Completion.ContinueWith(_ =>
            {
                foreach (var slotInfo in slotInfoList)
                {
                    slotInfo.Target.Complete();
                }
            });
            return actionBlock;
        }

        /// <summary>
        /// This class abstract a series of information about a slot, with also the 
        /// target block that physically dispatch an events to all projections of the slot.
        /// Contained in <see cref="Target"/> property.
        /// </summary>
        /// <typeparam name="T"></typeparam>
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
