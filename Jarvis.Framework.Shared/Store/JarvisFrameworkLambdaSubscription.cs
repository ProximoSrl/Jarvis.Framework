using NStore.Core.Persistence;
using System;
using System.Threading.Tasks;

namespace Jarvis.Framework.Shared.Store
{
    public class JarvisFrameworkLambdaSubscription : ISubscription
    {
        private readonly ChunkProcessor _fn;
        private long _failedPosition;

        public bool ReadCompleted { get; private set; }
        public bool Failed => LastException != null;

        /// <summary>
        /// Last failed position, if the caller set zero to this value, this will be the last
        /// dispatched chunk position
        /// </summary>
        public long FailedPosition => _failedPosition == 0 && Failed ? LastReceivedPosition : _failedPosition;

        /// <summary>
        /// Ok, we have two distinct way of failing, caller always calls <see cref="OnErrorAsync"/>
        /// but we want to distinguish if the error is happening in <see cref="_fn"/> chunk processor
        /// or if it is a general error (maybe db is down);
        /// </summary>
        public Boolean DispatchingFailed { get; private set; }

        public Exception LastException { get; private set; }

        /// <summary>
        /// This is the last <see cref="IChunk.Position"/> of good dispatched
        /// chunk.
        /// </summary>
        public Int64 LastReceivedPosition => LastDispatchedChunk?.Position ?? 0L;

        public IChunk LastDispatchedChunk { get; set; }

        public JarvisFrameworkLambdaSubscription(ChunkProcessor fn)
        {
            _fn = fn;
            _failedPosition = 0;
            LastException = null;
        }

        public Task<bool> OnNextAsync(IChunk chunk)
        {
            _failedPosition = 0;
            //Reset the last exception, this will be reset again if we generate other errors.
            LastException = null;

            LastDispatchedChunk = chunk;
            try
            {
                var retValue = _fn(chunk);
                DispatchingFailed = false;
                return retValue;
            }
            catch (Exception)
            {
                //Signal dispatching failed, then simply retrhow the exception.
                DispatchingFailed = true;
                throw;
            }
        }

        public Task OnStartAsync(long indexOrPosition)
        {
            return Task.CompletedTask;
        }

#pragma warning disable S4144 // Methods should not have identical implementations
        public Task CompletedAsync(long indexOrPosition)
        {
            this.ReadCompleted = true;
            return Task.CompletedTask;
        }

        public Task StoppedAsync(long indexOrPosition)

        {
            this.ReadCompleted = true;
            return Task.CompletedTask;
        }
#pragma warning restore S4144 // Methods should not have identical implementations

        public Task OnErrorAsync(long indexOrPosition, Exception ex)
        {
            LastException = ex;
            _failedPosition = indexOrPosition;

            return Task.CompletedTask;
        }
    }
}
