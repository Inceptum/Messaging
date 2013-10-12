using System;

namespace Inceptum.DataBus
{
    public interface IChannel<TData>
    {
        /// <summary>
        /// Handles feed selection with fluent API 
        /// </summary>
        /// <returns></returns>
        IFeed<TData> Feed();

        /// <summary>
        /// Handles feed selection with fluent API 
        /// </summary>
        /// <typeparam name="TContext">The type of the context.</typeparam>
        /// <param name="context">The context</param>
        /// <returns></returns>
        IFeed<TData> Feed<TContext>(TContext context);

        /// <summary>
        /// Handles feed selection with fluent API 
        /// </summary>
        /// <param name="c1">The context value 1.</param>
        /// <param name="c2">The context value 2.</param>
        /// <returns>the feed</returns>
        IFeed<TData> Feed<TContext1, TContext2>(TContext1 c1, TContext2 c2);

        /// <summary>
        /// Handles feed selection with fluent API 
        /// </summary>
        /// <param name="c1">The context value 1.</param>
        /// <param name="c2">The context value 2.</param>
        /// <param name="c3">The context value 3.</param>
        /// <returns>the feed</returns>
        IObservable<TData> Feed<TContext1, TContext2, TContext3>(TContext1 c1, TContext2 c2, TContext3 c3);

        /// <summary>
        /// Handles feed selection with fluent API 
        /// </summary>
        /// <param name="c1">The context value 1.</param>
        /// <param name="c2">The context value 2.</param>
        /// <param name="c3">The context value 3.</param>
        /// <param name="c4">The context value 4.</param>
        /// <returns>the feed</returns>
        IObservable<TData> Feed<TContext1, TContext2, TContext3, TContext4>(TContext1 c1, TContext2 c2, TContext3 c3, TContext4 c4);

        /// <summary>
        /// Handles feed selection with fluent API 
        /// </summary>
        /// <param name="c1">The context value 1.</param>
        /// <param name="c2">The context value 2.</param>
        /// <param name="c3">The context value 3.</param>
        /// <param name="c4">The context value 4.</param>
        /// <param name="c5">The context value 5.</param>
        /// <returns>the feed</returns>
        IObservable<TData> Feed<TContext1, TContext2, TContext3, TContext4, TContext5>(TContext1 c1, TContext2 c2, TContext3 c3, TContext4 c4, TContext5 c5);

        /// <summary>
        /// Handles feed selection with fluent API 
        /// </summary>
        /// <param name="c1">The context value 1.</param>
        /// <param name="c2">The context value 2.</param>
        /// <param name="c3">The context value 3.</param>
        /// <param name="c4">The context value 4.</param>
        /// <param name="c5">The context value 5.</param>
        /// <param name="c6">The context value 6.</param>
        /// <returns>the feed</returns>
        IObservable<TData> Feed<TContext1, TContext2, TContext3, TContext4, TContext5, TContext6>(TContext1 c1, TContext2 c2, TContext3 c3, TContext4 c4, TContext5 c5, TContext6 c6);

        /// <summary>
        /// Handles feed selection with fluent API 
        /// </summary>
        /// <param name="c1">The context value 1.</param>
        /// <param name="c2">The context value 2.</param>
        /// <param name="c3">The context value 3.</param>
        /// <param name="c4">The context value 4.</param>
        /// <param name="c5">The context value 5.</param>
        /// <param name="c6">The context value 6.</param>
        /// <param name="c7">The context value 7.</param>
        /// <returns>the feed</returns>
        IObservable<TData> Feed<TContext1, TContext2, TContext3, TContext4, TContext5, TContext6, TContext7>(TContext1 c1, TContext2 c2, TContext3 c3, TContext4 c4, TContext5 c5, TContext6 c6,
                                                                                                             TContext7 c7);
    }
}