using Discord;
using Discord_Bot.DataStrucs;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Discord_Bot.Services
{
    class LoggingService
    {
        private static BlockingCollection<Log> logQueue = new BlockingCollection<Log>();
        public static void Initialize()
        {
            Thread thread = new Thread(() =>
            {
                while (true)
                {
                    var log = logQueue.Take();
                   
                    Console.Write($"{DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss")} ");

                    Console.ForegroundColor = log.ConsoleColor;
                    Console.Write($"[{log.Source}] ");
                    Console.ResetColor();

                    Console.Write($"{log.Message}\n");
                }
            }) { Name = "Logger", Priority = ThreadPriority.Lowest };
            thread.Start();
            Log("Log", "LoggingService initialized", ConsoleColor.Cyan).ConfigureAwait(false);
        }

        /// <summary>
        /// Log messages to the Console. Console colors are controlled by Severity level
        /// </summary>
        /// <param name="source">Log source</param>
        /// <param name="message">Log message</param>
        /// <param name="severity">Log severity</param>
        public static async Task Log(string source, string message, LogSeverity severity)
        {
            logQueue.Add(new Log()
            {
                Source = source,
                Message = message,
                ConsoleColor = GetConsoleColor(severity)
            });
            await Task.CompletedTask;
        }

        /// <summary>
        /// Log messages to the Console. Console colors are controlled by the specified ConsoleColor (if not set it will default to Cyan)
        /// </summary>
        /// <param name="source">Log source</param>
        /// <param name="message">Log message</param>
        /// <param name="color">Log color</param>
        public static async Task Log(string source, string message, ConsoleColor color = ConsoleColor.Cyan)
        {
            logQueue.Add(new Log()
            {
                Source = source,
                Message = message,
                ConsoleColor = color
            });
            await Task.CompletedTask;
        }

        /// <summary>
        /// Get ConsoleColor from Discord.LogSeverity
        /// </summary>
        /// <returns>System.ConsoleColor</returns>
        private static ConsoleColor GetConsoleColor(LogSeverity severity)
        {
            switch (severity)
            {
                case LogSeverity.Critical:
                    return ConsoleColor.Red;

                case LogSeverity.Error:
                    return ConsoleColor.Red;

                case LogSeverity.Warning:
                    return ConsoleColor.Yellow;

                case LogSeverity.Verbose:
                    return ConsoleColor.DarkYellow;

                case LogSeverity.Debug:
                    return ConsoleColor.Yellow;

                default:
                    return ConsoleColor.Cyan;
            }
        }
    }
}
