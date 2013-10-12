using System;
using System.Reactive.Disposables;
using System.Threading;

namespace Db.Aces.Platform.Transport.Tibco
{
    public class SafeCompositeDisposable : IDisposable
    {
        private readonly CompositeDisposable _innerDisposable = new CompositeDisposable();
        private volatile bool _isDisposed;
        private volatile bool _isDisposing;

        public bool IsDisposed
        {
            get { return _isDisposed; }
        }

        public bool IsDisposing
        {
            get { return _isDisposing; }
        }

        public IDisposable Lock()
        {
            Monitor.Enter(_innerDisposable);
            return Disposable.Create(() => Monitor.Exit(_innerDisposable));
        }

        public void Dispose()
        {
            lock (_innerDisposable)
            {
                _isDisposing = true;
                _innerDisposable.Dispose();
                _isDisposed = true;
            }
        }

        public void Add(IDisposable disposable)
        {
            lock (_innerDisposable)
            {
                _innerDisposable.Add(disposable);
            }
        }
    }
}