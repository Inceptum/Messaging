using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Inceptum.Cqrs
{
    public class CommandDispatcher
    {
        readonly Dictionary<Type, Action<object>> m_Handlers = new Dictionary<Type, Action<object>>();


        public void Wire(object o)
        {
            if (o == null) throw new ArgumentNullException("o");
            var handledTypes = o.GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(m => m.Name == "Handle" && !m.IsGenericMethod && m.GetParameters().Length == 1)
                .Select(m => m.GetParameters().First().ParameterType)
                .Where(p => !p.IsInterface);

            foreach (var type in handledTypes)
            {
                registerHandler(type, o);
            }
        }

        private void registerHandler(Type parameterType, object o)
        {
            var @event = Expression.Parameter(typeof(object), "command");
            var call = Expression.Call(Expression.Constant(o), "Handle", null, Expression.Convert(@event, parameterType));
            var lambda = (Expression<Action<object>>)Expression.Lambda(call, @event);

            Action<object> handler;
            if (!m_Handlers.TryGetValue(parameterType, out handler))
            {
                m_Handlers.Add(parameterType, lambda.Compile());
                return;
            }
            throw new InvalidOperationException(string.Format(
                "Only one handler per command is allowed. Command {0} handler is already registered. Can not register {1} as handler for it",parameterType,o));

        }

        public void Dispacth(object command, string boundContext)
        {
            Action<object> handler;
            if (!m_Handlers.TryGetValue(command.GetType(), out handler))
            {
                throw new InvalidOperationException(string.Format("Failed to handle command {0}, no handler was registered for it",command));
            }
            handler(command);
        } 
    }
}