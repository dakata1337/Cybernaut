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

            //Latest.log location
            string latestLog = "logs/latest.log";

            #region Log Handler
            var loggingThread = new Thread(async () =>
            {
                //If directory "logs" doesn't exist create it
                if (!Directory.Exists(@"logs"))
                {
                    Directory.CreateDirectory(@"logs");
                }

                //Log to File Start time
                await LogToFile($"============={DateTime.UtcNow.ToString("dd/MM/yyyy HH:mm:ss")}=============\n", latestLog);

                //Create infinite loop
                while (true)
                {
                    //Take Source & Message
                    string src = sourceQueue.Take();
                    string msg = messageQueue.Take();

                    //If LogToFile is true log it
                    if (GlobalData.Config.logToFile)
                    {
                        await LogToFile($"[{src}] " + msg + Environment.NewLine, latestLog);
                    }

                    //Take Message Color
                    Console.ForegroundColor = colorQueue.Take();

                    //Print Source with selected color
                    Console.Write($"[{src}] ");

                    //Reset to default color
                    Console.ResetColor();

                    //Write the message
                    Console.Write(msg + Environment.NewLine);
                }

            });
            #endregion

            #region Excetion Handler Thread
            var exceptionThread = new Thread(async () =>
            {
                while (true)
                {
                    //Take exception from Queue
                    Exception exception = exceptionQueue.Take();

                    //Log it to File
                    await LogToFile(
                        exception.Message + Environment.NewLine +
                        exception.StackTrace + Environment.NewLine +
                        exception.InnerException + Environment.NewLine, latestLog);

                    //Display exception in Console
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
                            await AddToQueue(exception.Source, exception.ToString(), ConsoleColor.Red);
                            break;
                    }
                }
            });
            #endregion

            #region Start Threads
            loggingThread.IsBackground = true;
            loggingThread.Start();
            exceptionThread.IsBackground = true;
            exceptionThread.Start();
            #endregion

            return Task.CompletedTask;
        }


        //Discord Logger
        #region Discord
        public static async Task Log(string src, LogSeverity severity, string message, Exception exception = null)
        {
            //If there is an exception give it to the Exception Handler
            if (exception != null)
            {
                exceptionQueue.Add(exception);
                await Task.CompletedTask;
            }

            //If the message is null OR message lenght is 0 return
            if (message is null || message.Length == 0)
                await Task.CompletedTask;

            //Add to Logging Queue
            await AddToQueue(src, message, GetSeverityColor(severity));
        }
        #endregion

        //Victoria Logger
        #region Victoria
        public static async Task LogVictoriaAsync(string src, LogSeverity severity, string message, Exception exception = null)
        {
            //If the exception isn't null give it to the Exception Handler
            if (exception != null)
            {
                //Add to exception Queue & return
                exceptionQueue.Add(exception);
                await Task.CompletedTask;
            }

            #if DEBUG
            if (message is null || message.Length == 0)
                await Task.CompletedTask;

            //Deserialize Victoria message
            var jObj = (JObject)JsonConvert.DeserializeObject(message);

            //Create string builder
            StringBuilder builder = new StringBuilder();

            //If the message contains Lavalink Stats
            if(jObj["op"].ToObject<string>() == "stats")
            {
                src = "LavaLink";
                var memused = jObj["memory"]["used"];
                var cpuCores = jObj["cpu"]["cores"];
                var lavalinkLoad = Math.Round(jObj["cpu"]["lavalinkLoad"].ToObject<double>(), 2);
                var lavalinkUptime = jObj["uptime"];
                builder.Append($"Mem: {memused}; CPU Load: {lavalinkLoad}%; Uptime: {lavalinkUptime}");
            }

            //If the message contains Track Update
            else if (jObj["op"].ToObject<string>() == "playerUpdate")
            {
                src = "LavaLink";
                builder.Append($"State position: {jObj["state"]["position"].ToObject<int>() / 1000}sec in Guild: {jObj["guildId"]}");
            }

            //If the message contains Event Info
            else if (jObj["op"].ToObject<string>() == "event")
            {
                //If the Event Type is TrackEndEvent
                if(jObj["type"].ToObject<string>() == "TrackEndEvent")
                {
                    src = "TrackEndEvent";
                    builder.Append($"Track ended in Guild: {jObj["guildId"]} | Reason: {jObj["reason"]}");
                }

                //If the Event Type is TrackStartEvent
                else if (jObj["type"].ToObject<string>() == "TrackStartEvent")
                {
                    src = "TrackStartEvent";
                    builder.Append($"Track started: {jObj["track"]} in Guild: {jObj["guildId"]}");
                }

                else
                {
                    src = $"{jObj["type"]}";
                    builder.Append(message);
                }
            }

            else
            {
                builder.Append(message);
            }

            //Add to Logging Queue
            await AddToQueue(src, builder.ToString(), GetSeverityColor(severity));
            #endif
        }
        #endregion

        #region Archive logs
        private static void ArchiveOldLog()
        {
            //Get info about Latest.log
            FileInfo latest = new FileInfo(@"logs/latest.log");

            //If the file doesn't exist return
            if (!latest.Exists)
                return;

            //Create ZIP Name with the File Creation Time
            var zipName = $@"logs/{latest.CreationTime.ToString("dd_MM_yyyy-H_mm_ss")}.zip";

            //Add it to the ZIP file
            using (ZipArchive zip = ZipFile.Open(zipName, ZipArchiveMode.Create))
                zip.CreateEntryFromFile(@"logs/latest.log", "latest.log");

            //Delete Latest.log
            File.Delete(@"logs/latest.log");

            //Wait 0.1sec
            Thread.Sleep(100);

            //Create new Latest.log
            File.Create(@"logs/latest.log").Dispose();

            //Set Creation Time (in UTC)
            File.SetCreationTimeUtc(@"logs/latest.log", DateTime.UtcNow);
        }
        #endregion

        #region Add Log to Queue
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
                //Try log the message to the Log
                try
                {
                    //If the file doesnt exists create it
                    if (!File.Exists(logFileLocation))
                        await File.Create(logFileLocation).DisposeAsync();

                    //Write to the file
                    using (StreamWriter sw = File.AppendText(logFileLocation))
                    {
                        sw.Write(message);
                    }

                    //If succsessfull break out of the loop
                    break;
                }

                //If an error occurs try again
                catch { continue; }
            }
            await Task.CompletedTask;
        }
        #endregion

        #region Get Color from Log Severity
        private static ConsoleColor GetSeverityColor(LogSeverity severity = new LogSeverity())
        {
            //Get Color form LogSeverity
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
        #endregion
    }
}