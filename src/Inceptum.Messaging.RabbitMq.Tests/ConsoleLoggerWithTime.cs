using System;
using System.Globalization;
using Castle.Core.Logging;

namespace Inceptum.Messaging.RabbitMq.Tests
{
    /// <summary>
    ///	The Logger sending everything to the standard output streams.
    /// This is mainly for the cases when you have a utility that
    /// does not have a logger to supply.
    /// </summary>
    [Serializable]
    public class ConsoleLoggerWithTime : LevelFilteredLogger
    {
        /// <summary>
        ///   Creates a new ConsoleLogger with the <c>Level</c>
        ///   set to <c>LoggerLevel.Debug</c> and the <c>Name</c>
        ///   set to <c>String.Empty</c>.
        /// </summary>
        public ConsoleLoggerWithTime()
            : this(String.Empty, LoggerLevel.Debug)
        {
        }

        /// <summary>
        ///   Creates a new ConsoleLogger with the <c>Name</c>
        ///   set to <c>String.Empty</c>.
        /// </summary>
        /// <param name = "logLevel">The logs Level.</param>
        public ConsoleLoggerWithTime(LoggerLevel logLevel)
            : this(String.Empty, logLevel)
        {
        }

        /// <summary>
        ///   Creates a new ConsoleLogger with the <c>Level</c>
        ///   set to <c>LoggerLevel.Debug</c>.
        /// </summary>
        /// <param name = "name">The logs Name.</param>
        public ConsoleLoggerWithTime(String name)
            : this(name, LoggerLevel.Debug)
        {
        }

        /// <summary>
        ///   Creates a new ConsoleLogger.
        /// </summary>
        /// <param name = "name">The logs Name.</param>
        /// <param name = "logLevel">The logs Level.</param>
        public ConsoleLoggerWithTime(String name, LoggerLevel logLevel)
            : base(name, logLevel)
        {
        }

        /// <summary>
        ///   A Common method to log.
        /// </summary>
        /// <param name = "loggerLevel">The level of logging</param>
        /// <param name = "loggerName">The name of the logger</param>
        /// <param name = "message">The Message</param>
        /// <param name = "exception">The Exception</param>
        protected override void Log(LoggerLevel loggerLevel, String loggerName, String message, Exception exception)
        {
            Console.Out.WriteLine("{3:H:mm:ss.fff} [{0}] '{1}' {2}", loggerLevel, loggerName, message, DateTime.Now);

            if (exception != null)
            {
                Console.Out.WriteLine("{5:H:mm:ss.fff} [{0}] '{1}' {2}: {3} {4}", loggerLevel, loggerName, exception.GetType().FullName,
                                      exception.Message,/* exception.StackTrace*/"",DateTime.Now);
            }
        }

        ///<summary>
        ///  Returns a new <c>ConsoleLogger</c> with the name
        ///  added after this loggers name, with a dot in between.
        ///</summary>
        ///<param name = "loggerName">The added hierarchical name.</param>
        ///<returns>A new <c>ConsoleLogger</c>.</returns>
        public override ILogger CreateChildLogger(string loggerName)
        {
            if (loggerName == null)
            {
                throw new ArgumentNullException("loggerName", "To create a child logger you must supply a non null name");
            }

            return new ConsoleLoggerWithTime(String.Format(CultureInfo.CurrentCulture, "{0}.{1}", Name, loggerName), Level);
        }
    }
}