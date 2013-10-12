using System;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
 

namespace Inceptum.DataBus
{
    public static class ObservableExtenisons
    {
        public static IObservable<T> ObserveOn<T>(this IObservable<T> source, IScheduler scheduler)
        {
            Func<IObserver<T>, IDisposable> subscribe = o =>
                                 {
                                     var queue = new Queue<Notification<T>>();
                                     Action deque = () =>
                                                        {
                                                            lock (queue)
                                                            {
                                                                var n = queue.Dequeue();
                                                                n.Accept(o);
                                                            }
                                                        };
                                     IDisposable disposable = source.Materialize().Subscribe(
                                         n =>
                                             {
                                                 queue.Enqueue(n);
                                                 scheduler.Schedule(deque);
                                             }
                                         );
                                     return disposable;
                                 };
            return Observable.Create(subscribe);
        }

/*
        public static IObservable<T> ObserveOnDispatcher<T>(this IObservable<T> observable, DispatcherPriority priority)
        {
            if (observable == null)
                throw new NullReferenceException();

            return observable.ObserveOn(Dispatcher.CurrentDispatcher, priority);
        }

        public static IObservable<T> ObserveOn<T>(this IObservable<T> observable, Dispatcher dispatcher, DispatcherPriority priority)
        {
            if (observable == null)
                throw new NullReferenceException();

            if (dispatcher == null)
                throw new ArgumentNullException("dispatcher");

            return Observable.CreateWithDisposable<T>(
                o => observable.Subscribe(
                    obj => dispatcher.Invoke((Action) (() => o.OnNext(obj)), priority),
                    ex => dispatcher.Invoke((Action) (() => o.OnError(ex)), priority),
                    () => dispatcher.Invoke((Action) (o.OnCompleted), priority)
                         )
                );
        }
*/
    }
}
