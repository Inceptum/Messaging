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
        private readonly Dictionary<Type, Func<object, CommandHandlingResult>> m_Handlers = new Dictionary<Type, Func<object, CommandHandlingResult>>();
        private Action<string> m_HandlerDefault;
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
            wireDefault(component);
            var types = wire(component).ToArray();

            m_Subscriptions = new CompositeDisposable(
                m_Endpoints.Select(endpoint =>
                    MessagingEngine.Subscribe(
                        (Endpoint) Kernel.Resolver.Resolve(context, context.Handler, Model, new DependencyModel(endpoint,typeof(Endpoint),false)) ,
                        dispatch,
                        dispatchUnknown,
                        types))
                    .ToArray());
            return component;
        }

        public override void Destroy(object instance)
        {
            m_Subscriptions.Dispose();
            base.Destroy(instance);
        }

        private void dispatchUnknown(string type, AcknowledgeDelegate acknowledge)
        {
            if (m_HandlerDefault == null)
            {
                throw new InvalidOperationException(String.Format("Failed to handle unknown message: {0}, no default handler registered", type));
            }

            try
            {
                m_HandlerDefault(type);
            }
            catch (Exception e)
            {
            }
            finally
            {
                acknowledge(0, true);
            }

        }

        private void dispatch(object message, AcknowledgeDelegate acknowledge, Dictionary<string, string> headers)
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


        private void wireDefault(object component)
        {
            if (component == null) throw new ArgumentNullException("o");

            var handleMethod = component.GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .FirstOrDefault(m => m.Name == "HandleUnknown" &&
                                     !m.IsGenericMethod &&
                                     m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof (string));

            if (handleMethod != null)
            {
                registerDefaultHandler(component);
            }
        }

        private void registerDefaultHandler(object o)
        {
            var typeParameter = Expression.Parameter(typeof(string), "type");
            Expression[] parameters = new Expression[] { typeParameter }.ToArray();
            var call = Expression.Call(Expression.Constant(o), "HandleUnknown", null, parameters);

            Expression<Action<string>> lambda = (Expression<Action<string>>)Expression.Lambda(call, typeParameter);
            m_HandlerDefault = lambda.Compile();
        }
    }
}