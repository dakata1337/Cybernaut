using Cybernaut.DataStructs;
using Discord;
using Microsoft.VisualBasic.CompilerServices;
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

        public static void LogTitle(string message)
            => Console.Title = message;

        private static BlockingCollection<string> sourceQueue = new BlockingCollection<string>();
        private static BlockingCollection<string> messageQueue = new BlockingCollection<string>();
        private static BlockingCollection<ConsoleColor> colorQueue = new BlockingCollection<ConsoleColor>();

        public static Task InitializeAsync()
        {
            string latestLog = "logs/latest.log";
            var loggingThread = new Thread(async () =>
            {
                #region Checks

                #region Directory Check
                if (!Directory.Exists(@"logs"))
                {
                    Directory.CreateDirectory(@"logs");
                }
                #endregion

                #endregion

                #region Code

                await LogToFile($"============={DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss")}=============\n", latestLog);

                #region Loop
                while (true)
                {
                    string src = sourceQueue.Take();
                    string msg = messageQueue.Take();
                    
                    if (GlobalData.Config.logToFile)
                    {
                        await LogToFile($"[{src}] " + msg + Environment.NewLine, latestLog);
                    }

                    Console.ForegroundColor = colorQueue.Take();
                    Console.Write($"[{src}] ");
                    Console.ResetColor();
                    Console.Write(msg + Environment.NewLine);
                }
                #endregion

                #endregion
            });

            loggingThread.IsBackground = true;
            loggingThread.Start();
            return Task.CompletedTask;
        }

        public static async Task Log(string src, LogSeverity severity, string message, Exception exception = null)
        {
            if (exception != null)
            {
                await AddToQueue(exception.Source,
                    $"========({DateTime.UtcNow})========\n" +
                    $"Exception: {exception}\n " +
                    $" Message: {exception.Message}\n " +
                    $" StackTrace: {exception.StackTrace}\n " +
                    $" InnerEXception: {exception.InnerException}\n" +
                    $"=====", 
                    getSeverityColor(severity));
                await Task.CompletedTask;
            }
            await AddToQueue(src, message, getSeverityColor(severity));
        }

        private static async Task AddToQueue(string src, string msg, ConsoleColor color)
        {
            sourceQueue.Add(src); //Adds the source to the queue
            messageQueue.Add(msg); //Adds the message to the queue
            colorQueue.Add(color); //Adds the color to the queue
            await Task.CompletedTask;
        }

        private static async Task LogToFile(string message, string logFileLocation)
        {
            using (StreamWriter writer = File.AppendText(logFileLocation))
                writer.Write(message);

            await Task.CompletedTask;
        }

        private static ConsoleColor getSeverityColor(LogSeverity severity = new LogSeverity())
        {
            #region Code
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
            #endregion
        }
    }
}