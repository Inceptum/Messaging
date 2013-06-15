using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Inceptum.Cqrs.Configuration
{
    public class CommandDispatcher
    {
        readonly Dictionary<Type, Action<object, IEventPublisher>> m_Handlers = new Dictionary<Type, Action<object, IEventPublisher>>();
        private readonly BoundedContext m_BoundedContext;

        public CommandDispatcher(BoundedContext boundedContext)
        {
            m_BoundedContext = boundedContext;
        }

        public void Wire(object o)
        {
            if (o == null) throw new ArgumentNullException("o");
            var handledTypes = o.GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                                .Where(m => m.Name == "Handle" && !m.IsGenericMethod && m.GetParameters().Length == 1)
                                .Select(m => m.GetParameters().First().ParameterType)
                                .Where(p => !p.IsInterface);

            foreach (var type in handledTypes)
            {
                registerHandler(type, o,false);
            }

            handledTypes = o.GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                            .Where(m => m.Name == "Handle" && !m.IsGenericMethod && m.GetParameters().Length == 2 && m.GetParameters()[1].ParameterType == typeof(IEventPublisher))
                            .Select(m => m.GetParameters().First().ParameterType)
                            .Where(p => !p.IsInterface);

            foreach (var type in handledTypes)
            {
                registerHandler(type, o,true);
            }

        }

        private void registerHandler(Type parameterType, object o, bool hasEventPublisherParam)
        {
            var command = Expression.Parameter(typeof(object), "command");
            var eventPublisher = Expression.Parameter(typeof(IEventPublisher), "eventPublisher");
            Expression[] parameters = hasEventPublisherParam
                                          ? new Expression[] { Expression.Convert(command, parameterType), eventPublisher }
                                          : new Expression[] { Expression.Convert(command, parameterType) };
            var call = Expression.Call(Expression.Constant(o), "Handle", null, parameters);
            var lambda = (Expression<Action<object, IEventPublisher>>)Expression.Lambda(call, command, eventPublisher);

 
            Action<object, IEventPublisher> handler;
            if (!m_Handlers.TryGetValue(parameterType, out handler))
            {
                m_Handlers.Add(parameterType, lambda.Compile());
                return;
            }
            throw new InvalidOperationException(string.Format(
                "Only one handler per command is allowed. Command {0} handler is already registered in bound context {1}. Can not register {2} as handler for it", parameterType, m_BoundedContext.Name, o));

        }

        public void Dispacth(object command)
        {
            Action<object, IEventPublisher> handler;

            if (!m_Handlers.TryGetValue(command.GetType(), out handler))
            {
                throw new InvalidOperationException(string.Format("Failed to handle command {0} in bound context {1}, no handler was registered for it", command, m_BoundedContext.Name));
            }
            handler(command,m_BoundedContext.EventsPublisher);
        }
    }
}