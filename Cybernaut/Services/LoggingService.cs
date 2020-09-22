using Cybernaut.DataStructs;
using Discord;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;

namespace Cybernaut.Services
{
    public class LoggingService
    {
        public static async Task LogInformationAsync(string source, string message)
           => await Log(source, LogSeverity.Info, message);

        public static async Task LogCriticalAsync(string source, string message, Exception exc = null)
            => await Log(source, LogSeverity.Critical, message, exc);

        #pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        public static async Task LogTitleAsync(string message)
        #pragma warning restore CS1998
            => Console.Title = message;

        private static BlockingCollection<string> logQueue = new BlockingCollection<string>();
        private static BlockingCollection<ConsoleColor> logColor = new BlockingCollection<ConsoleColor>();

        public static Task InitializeAsync()
        {

            var thread = new Thread(() =>
            {
                if (!Directory.Exists(@"logs"))
                {
                    Directory.CreateDirectory(@"logs");
                }

                string logFileLocation = @"logs/latest.log";
                using (StreamWriter writer = File.AppendText(logFileLocation))
                {
                    writer.Write($"\n============={DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss")}=============\n");
                }

                while (true)
                {
                    string log = logQueue.Take();
                    
                    if (GlobalData.Config.logToFile)
                    {
                        using (StreamWriter writer = File.AppendText(logFileLocation))
                        {
                            writer.Write(log);
                        }
                    }

                    Console.ForegroundColor = logColor.Take();
                    Console.Write(log);
                    Console.ResetColor();
                }
            });
            thread.IsBackground = true;
            thread.Start();
            return Task.CompletedTask;
        }

        public static async Task Log(string src, LogSeverity severity, string message, Exception exception = null)
        {
            if (exception != null)
            {
                await Append($"=====\n({DateTime.UtcNow}) [{exception.Source}] NullException:\n Message: {exception.Message}\n StackTrace: {exception.StackTrace}\n InnerEXception: {exception.InnerException}\n=====\n", getSeverityColor(severity));
                await Task.CompletedTask;
            }
            await Append($"{GetSeverityString(severity)} ", getSeverityColor(severity));
            await Append($"[{src}] {message}\n", ConsoleColor.Gray);
        }

        private static async Task Append(string message, ConsoleColor color)
        {
            logColor.Add(color); //Adds the color to the queue
            logQueue.Add(message); //Adds the message to the queue
        }

        private static ConsoleColor getSeverityColor(LogSeverity severity = new LogSeverity())
        {
            switch (severity)
            {
                case LogSeverity.Critical:
                    return ConsoleColor.Red;
                case LogSeverity.Error:
                    return ConsoleColor.DarkRed;
                case LogSeverity.Warning:
                    return ConsoleColor.Yellow;
                case LogSeverity.Info:
                    return ConsoleColor.Green;
                case LogSeverity.Debug:
                    return ConsoleColor.Magenta;
                default:
                    return ConsoleColor.White;
            }
        }

        private static string GetSeverityString(LogSeverity severity)
        {
            switch (severity)
            {
                case LogSeverity.Critical:
                    return "CRIT";
                case LogSeverity.Debug:
                    return "DBUG";
                case LogSeverity.Error:
                    return "EROR";
                case LogSeverity.Info:
                    return "INFO";
                case LogSeverity.Verbose:
                    return "VERB";
                case LogSeverity.Warning:
                    return "WARN";
                default: 
                    return "UNKN";
            }
        }
    }
}