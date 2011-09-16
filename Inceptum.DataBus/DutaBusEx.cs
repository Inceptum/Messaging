using System;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
 

namespace Inceptum.DataBus
{
    //TODO: rename to ObservableExtenisons
    public static class DutaBusExtensions
    {
        public static IObservable<T> ObserveOn<T>(this IObservable<T> source, IScheduler scheduler)
        {
            return Observable.Create<T>(o =>
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
                                                              return source.Materialize().Subscribe(
                                                                  n =>
                                                                      {
                                                                          queue.Enqueue(n);
                                                                          scheduler.Schedule(deque);
                                                                      }
                                                                  );
                                                          }
                );
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