using System;
using System.Threading;

namespace Inceptum.Utils
{
    /// <summary>
    /// Disposable wrapper
    /// </summary>
    public static class Disposable
    {
        /// <summary>
        /// Creates new AnonymousDisposable instance 
        /// </summary>
        /// <param name="dispose">Delegate which will be called on calling of Dispose</param>
        /// <returns>Instance of IDisposable, which will call supplied Action when Dispose method is called</returns>
        public static IDisposable Create(Action dispose)
        {
            if (dispose == null)
            {
                throw new ArgumentNullException("dispose");
            }
            return new AnonymousDisposable(dispose);
        }

        /// <summary>
        /// Empty disposable instance
        /// </summary>
        public static IDisposable Empty
        {
            get { return DefaultDisposable.Instance; }
        }


        /// <summary>
        /// Calls supplied Action on Dispose
        /// </summary>
        internal sealed class AnonymousDisposable : IDisposable
        {
            private readonly Action dispose;
            private int isDisposed;

            /// <summary>
            /// Creates new instance of AnonymousDisposable
            /// </summary>
            /// <param name="dispose">Action to call when Dispose is called</param>
            public AnonymousDisposable(Action dispose)
            {
                this.dispose = dispose;
            }

            public void Dispose()
            {
                if (Interlocked.Exchange(ref isDisposed, 1) == 0)
                {
                    dispose();
                }
            }
        }

        /// <summary>
        /// Default disposable, does nothing on dispose
        /// </summary>
        internal sealed class DefaultDisposable : IDisposable
        {
            /// <summary>
            /// Single instance of DefaultDisposable
            /// </summary>
            public static readonly DefaultDisposable Instance = new DefaultDisposable();

            // Methods
            private DefaultDisposable()
            {
            }

            /// <summary>
            /// Dispose method does nothing
            /// </summary>
            public void Dispose()
            {
            }
        }
    }
}