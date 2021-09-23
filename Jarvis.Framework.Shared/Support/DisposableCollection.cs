using System;

namespace Jarvis.Framework.Shared.Support
{
    public class DisposableCollection : IDisposable
    {
        private readonly IDisposable[] _disposeList;

        public DisposableCollection(params IDisposable[] disposeList)
        {
            _disposeList = disposeList;
        }

        public void Dispose()
        {
            foreach (var disposableItem in _disposeList)
            {
                disposableItem.Dispose();
            }
        }
    }
}
