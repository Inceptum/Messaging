using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Inceptum.Cqrs.Configuration;

namespace Inceptum.Cqrs
{
    internal class OptionalParameter<T> : OptionalParameter
    {
        public OptionalParameter(string name,T value)
        {
            Name = name;
            Value = value;
            Type = typeof (T);
        }
        
        public OptionalParameter(T value)
        {
            Value = value;
            Type = typeof (T);
        }
    }

    abstract class OptionalParameter
    {
        public object Value { get; protected set; }
        public Type Type { get; protected set; }
        public string Name { get; protected set; }
    }
    internal class EventDispatcher
    {
        readonly Dictionary<Type, List<Action<object>>> m_Handlers = new Dictionary<Type, List<Action<object>>>();
        private readonly BoundedContext m_BoundedContext;

        public EventDispatcher(BoundedContext boundedContext)
        {
            m_BoundedContext = boundedContext;
        }
        public void Wire(object o, params OptionalParameter[] parameters)
        {
            parameters = parameters.Concat(new OptionalParameter[] {new OptionalParameter<string>("boundedContext", m_BoundedContext.Name)}).ToArray();

            var handleMethods = o.GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(m => m.Name == "Handle" && 
                    !m.IsGenericMethod && 
                    m.GetParameters().Length>0 && 
                    !m.GetParameters().First().ParameterType.IsInterface)
                .Select(m=>new
                    {
                        method=m,
                        eventType = m.GetParameters().First().ParameterType,
                        callParameters=m.GetParameters().Skip(1).Select(p=>new
                            {
                                parameter = p,
                                optionalParameter=parameters.FirstOrDefault(par=>par.Name==p.Name||par.Name==null && p.ParameterType==par.Type),
                            })
                    })
                .Where(m=>m.callParameters.All(p=>p.parameter!=null));


            foreach (var method in handleMethods)
            {
                registerHandler(method.eventType, o, method.callParameters.ToDictionary(p => p.parameter, p => p.optionalParameter.Value));
            }
        }

        private void registerHandler(Type parameterType, object o, Dictionary<ParameterInfo,object> optionalParameters)
        {
            var @event = Expression.Parameter(typeof(object), "event");
            Expression[] parameters =
                new Expression[] {Expression.Convert(@event, parameterType)}.Concat(optionalParameters.Select(p => Expression.Constant(p.Value))).ToArray();
            var call = Expression.Call(Expression.Constant(o), "Handle", null, parameters);


            var lambda = (Expression<Action<object>>)Expression.Lambda(call, @event);

            List<Action<object>> list;
            if (!m_Handlers.TryGetValue(parameterType, out list))
            {
                list = new List<Action<object>>();
                m_Handlers.Add(parameterType, list);
            }
            list.Add(lambda.Compile());

        }

/*
        public void Wire(object o)
        {
            if (o == null) throw new ArgumentNullException("o");
            var handledTypes = o.GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(m => m.Name == "Handle" && !m.IsGenericMethod && m.GetParameters().Length == 1)
                .Select(m => m.GetParameters().First().ParameterType)
                .Where(p=>!p.IsInterface);

            foreach (var type in handledTypes)
            {
                registerHandler(type,o,false);
            }        
            
            handledTypes = o.GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(m => m.Name == "Handle" && !m.IsGenericMethod && m.GetParameters().Length == 2 && m.GetParameters()[1].Name == "boundedContext" && m.GetParameters()[1].ParameterType==typeof(string))
                .Select(m => m.GetParameters().First().ParameterType)
                .Where(p=>!p.IsInterface);

            foreach (var type in handledTypes)
            {
                registerHandler(type,o,true);
            }
        }

        private void registerHandler(Type parameterType, object o,bool hasBoundedContextParam)
        {
            var @event = Expression.Parameter(typeof(object), "event");
            var boundedContext = Expression.Constant(m_BoundedContext.Name);// Parameter(typeof(string), "boundedContext");
            Expression[] parameters =hasBoundedContextParam
                ? new Expression[] { Expression.Convert(@event, parameterType) ,boundedContext}
                : new Expression[] { Expression.Convert(@event, parameterType) };
            var call = Expression.Call(Expression.Constant(o), "Handle", null, parameters);


            var lambda = (Expression<Action<object>>)Expression.Lambda(call, @event);

            List<Action<object>> list;
            if (!m_Handlers.TryGetValue(parameterType, out list))
            {
                list = new List<Action<object>>();
                m_Handlers.Add(parameterType,list);
            }
            list.Add(lambda.Compile());
            
        }*/
      
        public void Dispacth(object @event)
        {
            List<Action<object>> list;
            if (!m_Handlers.TryGetValue(@event.GetType(), out list))
                return;
            foreach (var handler in list)
            {
                handler(@event);
                //TODO: event handling
            }
        }
    }
}