using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reactive.Disposables;
using System.Reflection;
using Castle.Core;
using Castle.MicroKernel;
using Castle.MicroKernel.ComponentActivator;
using Castle.MicroKernel.Context;
using Inceptum.Messaging.Contract;

namespace Inceptum.Messaging.Castle
{
    public class MessageHandlerActivator : DefaultComponentActivator 
    {
        private const long FAILED_COMMAND_RETRY_DELAY = 60000;
        private IMessagingEngine m_MessagingEngine;
        private readonly string[] m_Endpoints;
        readonly Dictionary<Type, Func<object, CommandHandlingResult>> m_Handlers = new Dictionary<Type, Func<object, CommandHandlingResult>>();
        private CompositeDisposable m_Subscriptions;
        private object m_Lock=new object();

        public MessageHandlerActivator(ComponentModel model, IKernelInternal kernel, ComponentInstanceDelegate onCreation, ComponentInstanceDelegate onDestruction) : base(model, kernel, onCreation, onDestruction)
        {
            m_Endpoints=model.ExtendedProperties["MessageHandlerFor"] as string[];
         
        }

        private IMessagingEngine MessagingEngine
        {
            get
            {
                if (m_MessagingEngine == null)
                {
                    lock (m_Lock)
                    {
                        if (m_MessagingEngine == null)
                            m_MessagingEngine = Kernel.Resolve<IMessagingEngine>();

                    }
                }
                return m_MessagingEngine;
            }
        }

        public override object Create(CreationContext context, Burden burden)
        {
            var component = base.Create(context, burden);
            var types = wire(component).ToArray();
            m_Subscriptions = new CompositeDisposable(
                m_Endpoints.Select(endpoint =>
                    MessagingEngine.Subscribe(
                        (Endpoint) Kernel.Resolver.Resolve(context, context.Handler, Model, new DependencyModel(endpoint,typeof(Endpoint),false)) ,
                        dispatch,
                        (type, acknowledge) =>
                        {
                            throw new InvalidOperationException("Message of unknown received: " + type);
                        }, types))
                    .ToArray());
            return component;
        }

        public override void Destroy(object instance)
        {
            m_Subscriptions.Dispose();
            base.Destroy(instance);
        }
 

        private void dispatch(object message, AcknowledgeDelegate acknowledge)
        {
            Func<object, CommandHandlingResult> handler;
            if (!m_Handlers.TryGetValue(message.GetType(), out handler))
            {
                throw new InvalidOperationException(string.Format("Failed to handle message {0}, no handler was registered for it", message));
            }

            try
            {
                var result = handler(message);
                acknowledge(result.RetryDelay, !result.Retry);
            }
            catch (Exception e)
            {
                acknowledge(FAILED_COMMAND_RETRY_DELAY, false);
            }
        }

        private IEnumerable<Type> wire(object component)
        {
            if (component == null) throw new ArgumentNullException("o");

            var handleMethods = component.GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(m => m.Name == "Handle" &&
                            !m.IsGenericMethod &&
                            m.GetParameters().Length ==1 &&
                            !m.GetParameters().First().ParameterType.IsInterface)
                .Select(m => new
                {
                    method = m,
                    returnsResult = m.ReturnType == typeof(CommandHandlingResult),
                    messageType = m.GetParameters().First().ParameterType
                });

            foreach (var method in handleMethods)
            {
                registerHandler(method.messageType, component, method.returnsResult);
                yield return method.messageType;
            }

        }


        private void registerHandler(Type commandType, object o,  bool returnsResult)
        {
            var command = Expression.Parameter(typeof(object), "command");
            Expression[] parameters =
                new Expression[] { Expression.Convert(command, commandType) }.ToArray();
            var call = Expression.Call(Expression.Constant(o), "Handle", null, parameters);

            Expression<Func<object, CommandHandlingResult>> lambda;
            if (returnsResult)
                lambda = (Expression<Func<object, CommandHandlingResult>>)Expression.Lambda(call, command);
            else
            {
                LabelTarget returnTarget = Expression.Label(typeof(CommandHandlingResult));
                var returnLabel = Expression.Label(returnTarget, Expression.Constant(new CommandHandlingResult { Retry = false, RetryDelay = 0 }));
                var block = Expression.Block(
                    call,
                    returnLabel);
                lambda = (Expression<Func<object, CommandHandlingResult>>)Expression.Lambda(block, command);
            }

            m_Handlers.Add(commandType, lambda.Compile());
        }
    }
}