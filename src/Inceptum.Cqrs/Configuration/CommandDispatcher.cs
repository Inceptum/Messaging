using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using Inceptum.Cqrs.Utils;
using Inceptum.Messaging.Contract;

namespace Inceptum.Cqrs.Configuration
{
    public class CommandHandlingResult
    {
        public long  RetryDelay { get; set; } 
        public bool  Retry { get; set; } 
    }


    internal class CommandDispatcher
    {
        readonly Dictionary<Type, Func<object, CommandHandlingResult>> m_Handlers = new Dictionary<Type, Func<object, CommandHandlingResult>>();
        private readonly string m_BoundedContext;
        private readonly QueuedTaskScheduler m_QueuedTaskScheduler;
        private readonly Dictionary<CommandPriority,TaskFactory> m_TaskFactories=new Dictionary<CommandPriority, TaskFactory>();

        public CommandDispatcher(string boundedContext, int threadCount=1)
        {
            m_QueuedTaskScheduler = new QueuedTaskScheduler(threadCount);
            foreach (var value in Enum.GetValues(typeof(CommandPriority)))
            {
                m_TaskFactories[(CommandPriority) value] = new TaskFactory(
                    ((CommandPriority) value) == CommandPriority.Normal
                        ? new CurrentThreadTaskScheduler()
                        : m_QueuedTaskScheduler.ActivateNewQueue((int) value));
            }
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
                    returnsResult=m.ReturnType==typeof(CommandHandlingResult),
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
                registerHandler(method.eventType, o, method.callParameters.ToDictionary(p => p.parameter, p => p.optionalParameter.Value),method.returnsResult);
            }

        }

        private void registerHandler(Type commandType, object o, Dictionary<ParameterInfo, object> optionalParameters, bool returnsResult)
        {
            var command = Expression.Parameter(typeof(object), "command");
            Expression[] parameters =
                new Expression[] { Expression.Convert(command, commandType) }.Concat(optionalParameters.Select(p => Expression.Constant(p.Value,p.Key.ParameterType))).ToArray();
            var call = Expression.Call(Expression.Constant(o), "Handle", null, parameters);

            Expression<Func<object, CommandHandlingResult>> lambda;
            if (returnsResult)
                lambda = (Expression<Func<object, CommandHandlingResult>>) Expression.Lambda(call, command);
            else
            {
                LabelTarget returnTarget = Expression.Label(typeof(CommandHandlingResult));
                var returnLabel = Expression.Label(returnTarget,Expression.Constant(new CommandHandlingResult { Retry = false, RetryDelay = 0 })); 
                var block = Expression.Block(
                    call,
                    returnLabel);
                lambda = (Expression<Func<object, CommandHandlingResult>>)Expression.Lambda(block, command);
            }


            Func<object, CommandHandlingResult> handler;
            if (!m_Handlers.TryGetValue(commandType, out handler))
            {
                m_Handlers.Add(commandType, lambda.Compile());
                return;
            }
            throw new InvalidOperationException(string.Format(
                "Only one handler per command is allowed. Command {0} handler is already registered in bound context {1}. Can not register {2} as handler for it", commandType, m_BoundedContext, o));

        }

        public void Dispacth(object command, CommandPriority priority, AcknowledgeDelegate acknowledge)
        {
            Func<object, CommandHandlingResult> handler;
            if (!m_Handlers.TryGetValue(command.GetType(), out handler))
            {
                throw new InvalidOperationException(string.Format("Failed to handle command {0} in bound context {1}, no handler was registered for it", command, m_BoundedContext));
            }

           m_TaskFactories[priority].StartNew(() => handle(command, acknowledge, handler));
        }

        private static void handle(object command, AcknowledgeDelegate acknowledge, Func<object, CommandHandlingResult> handler)
        {
            try
            {
                var result = handler(command);
                acknowledge(result.RetryDelay, !result.Retry);
            }
            catch (Exception e)
            {
                acknowledge(60000, false);
            }
        }
    }
}