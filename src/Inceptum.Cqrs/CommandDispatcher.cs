using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Inceptum.Cqrs
{
    public class CommandDispatcher
    {
        readonly Dictionary<Tuple<Type, string>, Action<object>> m_Handlers = new Dictionary<Tuple<Type, string>, Action<object>>();
        public string[] KnownBoundContexts { get { return m_Handlers.Keys.Select(k => k.Item2).Distinct().ToArray(); } }
        public void Wire(object o,string boundContext)
        {
            if (o == null) throw new ArgumentNullException("o");
            var handledTypes = o.GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(m => m.Name == "Handle" && !m.IsGenericMethod && m.GetParameters().Length == 1)
                .Select(m => m.GetParameters().First().ParameterType)
                .Where(p => !p.IsInterface);

            foreach (var type in handledTypes)
            {
                registerHandler(boundContext,type, o);
            }
        }

        private void registerHandler(string boundContext,Type parameterType, object o)
        {
            var @event = Expression.Parameter(typeof(object), "command");
            var call = Expression.Call(Expression.Constant(o), "Handle", null, Expression.Convert(@event, parameterType));
            var lambda = (Expression<Action<object>>)Expression.Lambda(call, @event);

            Action<object> handler;
            var key = Tuple.Create(parameterType, boundContext);
            if (!m_Handlers.TryGetValue(key, out handler))
            {
                m_Handlers.Add(key, lambda.Compile());
                return;
            }
            throw new InvalidOperationException(string.Format(
                "Only one handler per command is allowed. Command {0} handler is already registered in bound context {1}. Can not register {2} as handler for it", parameterType,boundContext, o));

        }

        public void Dispacth(object command, string boundContext)
        {
            Action<object> handler;
            var key = Tuple.Create(command.GetType(), boundContext);
            if (!m_Handlers.TryGetValue(key, out handler))
            {
                throw new InvalidOperationException(string.Format("Failed to handle command {0} in bound context {1}, no handler was registered for it",command, boundContext));
            }
            handler(command);
        } 
    }
}