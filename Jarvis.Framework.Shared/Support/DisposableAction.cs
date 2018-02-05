using System;
using System.Threading;

namespace Jarvis.Framework.Shared.Support
{
    namespace Jarvis.Common.Shared.Utils
    {
        internal sealed class DisposableAction : IDisposable
        {
            public static readonly DisposableAction Empty = new DisposableAction(null);

            private Action _disposeAction;
            private Boolean _disposed;

            public DisposableAction(Action disposeAction)
            {
                _disposeAction = disposeAction;
            }

            public void Dispose()
            {
                // Interlocked allows the continuation to be executed only once
                Action dispose = Interlocked.Exchange(ref _disposeAction, null);
                if (dispose != null && !_disposed)
                {
                    _disposed = true;
                    dispose();
                }
            }
        }
    }
}
