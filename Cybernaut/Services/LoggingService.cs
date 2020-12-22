using Cybernaut.DataStructs;
using Cybernaut.Handlers;
using Discord;
using Discord.Net;
using Discord.WebSocket;
using Microsoft.VisualBasic.CompilerServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Victoria.Enums;

namespace Cybernaut.Services
{
    public class LoggingService
    {
        public static async Task LogInformationAsync(string source, string message)
           => await Log(source, LogSeverity.Info, message);

        public static async Task LogCriticalAsync(string source, string message, Exception exc = null)
            => await Log(source, LogSeverity.Critical, message, exc);

        private static BlockingCollection<string> sourceQueue = new BlockingCollection<string>();
        private static BlockingCollection<string> messageQueue = new BlockingCollection<string>();
        private static BlockingCollection<Exception> exceptionQueue = new BlockingCollection<Exception>();
        private static BlockingCollection<ConsoleColor> colorQueue = new BlockingCollection<ConsoleColor>();

        public static Task Initialize()
        {
            //Archive the old log
            ArchiveOldLog();

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

            var exceptionThread = new Thread(async () =>
            {
                #region Code
                while (true)
                {
                    Exception exception = exceptionQueue.Take();
                    await LogToFile(
                        exception.Message + Environment.NewLine +
                        exception.StackTrace + Environment.NewLine +
                        exception.InnerException + Environment.NewLine, latestLog);

                    switch (exception)
                    {
                        case GatewayReconnectException:
                            await AddToQueue("Gateway", exception.Message, ConsoleColor.Yellow);
                            break;
                        case WebSocketClosedException:
                            await AddToQueue("Gateway", exception.Message, ConsoleColor.Yellow);
                            break;
                        case WebSocketException:
                            await AddToQueue("Gateway", exception.Message, ConsoleColor.Yellow);
                            break;
                        default:
                            await AddToQueue(exception.Source, exception.ToString(), ConsoleColor.DarkYellow);
                            break;
                    }
                }
                #endregion
            });

            #region Start Threads
            loggingThread.IsBackground = true;
            loggingThread.Start();
            exceptionThread.IsBackground = true;
            exceptionThread.Start();
            #endregion

            return Task.CompletedTask;
        }

        #region Archive logs
        private static void ArchiveOldLog()
        {
            FileInfo latest = new FileInfo(@"logs/latest.log");
            if (!latest.Exists)
                return;

            var zipName = $@"logs/{latest.CreationTime.ToString("dd_MM_yyyy-H_mm_ss")}.zip";

            using (ZipArchive zip = ZipFile.Open(zipName, ZipArchiveMode.Create))
                zip.CreateEntryFromFile(@"logs/latest.log", "latest.log");

            File.Delete(@"logs/latest.log");
            Thread.Sleep(100);
            File.Create(@"logs/latest.log").Dispose();
            File.SetCreationTimeUtc(@"logs/latest.log", DateTime.UtcNow);
        }
        #endregion

        #region Log
        public static async Task Log(string src, LogSeverity severity, string message, Exception exception = null)
        {
            //If there is an exception give it to the exception handler
            if (exception != null)
            {
                exceptionQueue.Add(exception);
                await Task.CompletedTask;
            }

            if (message is null)
                await Task.CompletedTask;
            else if(message.Length != 0)
                await AddToQueue(src, message, GetSeverityColor(severity));
        }

        public static async Task LogVictoriaAsync(string src, LogSeverity severity, string message, Exception exception = null)
        {
            if (exception != null)
            {
                exceptionQueue.Add(exception);
                await Task.CompletedTask;
            }
            #if DEBUG
            if (message is null)
                await Task.CompletedTask;
            else if (message.Length == 0)
                await Task.CompletedTask;

            var jObj = (JObject)JsonConvert.DeserializeObject(message);
            StringBuilder builder = new StringBuilder();
            if(jObj["op"].ToObject<string>() == "stats")
            {
                src = "LavaLink";
                var memused = jObj["memory"]["used"];
                var cpuCores = jObj["cpu"]["cores"];
                var lavalinkLoad = Math.Round(jObj["cpu"]["lavalinkLoad"].ToObject<double>(), 2);
                var lavalinkUptime = jObj["uptime"];
                builder.Append($"Mem: {memused}; CPU Load: {lavalinkLoad}%; Uptime: {lavalinkUptime}");
            }
            else if (jObj["op"].ToObject<string>() == "playerUpdate")
            {
                src = "LavaLink";
                builder.Append($"State position: {jObj["state"]["position"].ToObject<int>() / 1000}sec in Guild: {jObj["guildId"]}");
            }
            else if (jObj["op"].ToObject<string>() == "event")
            {
                
                if(jObj["type"].ToObject<string>() == "TrackEndEvent")
                {
                    src = "TrackEndEvent";
                    builder.Append($"Track ended in Guild: {jObj["guildId"]} | Reason: {jObj["reason"]}");
                }
                else if (jObj["type"].ToObject<string>() == "TrackStartEvent")
                {
                    src = "TrackStartEvent";
                    builder.Append($"Track started: {jObj["track"]} in Guild: {jObj["guildId"]}");
                }
            }

            await AddToQueue(src, builder.ToString(), GetSeverityColor(severity));
            #endif
        }
        #endregion

        #region Queue
        private static async Task AddToQueue(string src, string msg, ConsoleColor color)
        {
            sourceQueue.Add(src); //Adds the source to the queue
            messageQueue.Add(msg); //Adds the message to the queue
            colorQueue.Add(color); //Adds the color to the queue
            await Task.CompletedTask;
        }
        #endregion

        #region Log to file
        private static async Task LogToFile(string message, string logFileLocation)
        {
            while (true)
            {
                try
                {
                    if (!File.Exists(logFileLocation))
                        await File.Create(logFileLocation).DisposeAsync();

                    using (StreamWriter sw = File.AppendText(logFileLocation))
                    {
                        sw.Write(message);
                    }
                    break;
                }
                catch { continue; }
            }
            await Task.CompletedTask;
        }
        #endregion

        private static ConsoleColor GetSeverityColor(LogSeverity severity = new LogSeverity())
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