using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Inceptum.Cqrs.Configuration
{
    internal class CommandDispatcher
    {
        readonly Dictionary<Type, Action<object>> m_Handlers = new Dictionary<Type, Action<object>>();
        private readonly string m_BoundedContext;

        public CommandDispatcher(string boundedContext)
        {
            m_BoundedContext = boundedContext;
        }

        public void Wire(object o, params OptionalParameter[] parameters)
        {
            if (o == null) throw new ArgumentNullException("o");

            var handleMethods = o.GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(m => m.Name == "Handle" &&
                    !m.IsGenericMethod &&
                    m.GetParameters().Length > 0 &&
                    !m.GetParameters().First().ParameterType.IsInterface)
                .Select(m => new
                {
                    method = m,
                    eventType = m.GetParameters().First().ParameterType,
                    callParameters = m.GetParameters().Skip(1).Select(p => new
                    {
                        parameter = p,
                        optionalParameter = parameters.FirstOrDefault(par => par.Name == p.Name || par.Name == null && p.ParameterType == par.Type),
                    })
                })
                .Where(m => m.callParameters.All(p => p.parameter != null));


            foreach (var method in handleMethods)
            {
                registerHandler(method.eventType, o, method.callParameters.ToDictionary(p => p.parameter, p => p.optionalParameter.Value));
            }

        }

        private void registerHandler(Type commandType, object o, Dictionary<ParameterInfo, object> optionalParameters)
        {
            var command = Expression.Parameter(typeof(object), "command");

            Expression[] parameters =
                new Expression[] { Expression.Convert(command, commandType) }.Concat(optionalParameters.Select(p => Expression.Constant(p.Value,p.Key.ParameterType))).ToArray();
            var call = Expression.Call(Expression.Constant(o), "Handle", null, parameters);
            var lambda = (Expression<Action<object>>)Expression.Lambda(call, command);

 
            Action<object> handler;
            if (!m_Handlers.TryGetValue(commandType, out handler))
            {
                m_Handlers.Add(commandType, lambda.Compile());
                return;
            }
            throw new InvalidOperationException(string.Format(
                "Only one handler per command is allowed. Command {0} handler is already registered in bound context {1}. Can not register {2} as handler for it", commandType, m_BoundedContext, o));

        }

        public void Dispacth(object command)
        {
            Action<object> handler;

            if (!m_Handlers.TryGetValue(command.GetType(), out handler))
            {
                throw new InvalidOperationException(string.Format("Failed to handle command {0} in bound context {1}, no handler was registered for it", command, m_BoundedContext));
            }
            handler(command);
        }
    }
}