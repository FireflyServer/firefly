using System;
using System.Threading;

namespace Dragonfly.Utils
{
    public class Disposable : IDisposable
    {
        private Action _dispose;

        public Disposable(Action dispose)
        {
            _dispose = dispose;
        }

        public void Dispose()
        {
            Interlocked.Exchange(ref _dispose, ()=> { }).Invoke();
        }
    }
}
