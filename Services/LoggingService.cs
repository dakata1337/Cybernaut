using Discord;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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
        private static BlockingCollection<string> sourceQueue = new BlockingCollection<string>();
        private static BlockingCollection<string> messageQueue = new BlockingCollection<string>();
        private static BlockingCollection<ConsoleColor> colorQueue = new BlockingCollection<ConsoleColor>();

        public static void Initialize()
        {
            //Cyrillic support
            Console.OutputEncoding = Encoding.Unicode;
            Thread thread = new Thread(() => 
            {
                while (true)
                {
                    string src = sourceQueue.Take();
                    string msg = messageQueue.Take();

                    Console.Write($"{DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss")} ");

                    Console.ForegroundColor = colorQueue.Take();
                    Console.Write($"[{src}] ");
                    Console.ResetColor();

                    Console.Write($"{msg}\n");
                }
            });
            thread.Start();
            Log("Log", "LoggingService initialized", ConsoleColor.Cyan);
        }

        //Regular Logger
        #region Regular Logger
        /// <summary>
        /// Log messages to the Console. Console colors are controlled by Severity level
        /// </summary>
        /// <param name="source">Log source</param>
        /// <param name="message">Log message</param>
        /// <param name="severity">Log severity</param>
        public static void Log(string source, string message, LogSeverity severity)
        {
            sourceQueue.Add(source);
            messageQueue.Add(message);
            colorQueue.Add(GetConsoleColor(severity));
        }

        /// <summary>
        /// Log messages to the Console. Console colors are controlled by the specified ConsoleColor (if not set it will default to Cyan)
        /// </summary>
        /// <param name="source">Log source</param>
        /// <param name="message">Log message</param>
        /// <param name="color">Log color</param>
        public static void Log(string source, string message, ConsoleColor? color = null)
        {
            sourceQueue.Add(source);
            messageQueue.Add(message);
            colorQueue.Add((ConsoleColor)(color == null ? ConsoleColor.Cyan : color));
        }
        #endregion


        //Victoria Logger
        #region Victoria
        public static async Task LogVictoriaAsync(string src, LogSeverity severity, string message, Exception exception = null)
        {
            //If the exception isn't null give it to the Exception Handler
            if (exception != null)
            {
                //Add to display exception & return
                Log(exception.Source, 
                    $"{exception.Message}\n" +
                    $"{exception.StackTrace}", severity);
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
            if (jObj["op"].ToObject<string>() == "stats")
            {
                src = "LavaLink";
                var memused = Math.Round(ConvertBytesToMegabytes((long)jObj["memory"]["used"]), 1);
                var cpuCores = jObj["cpu"]["cores"];
                var lavalinkLoad = Math.Round(jObj["cpu"]["lavalinkLoad"].ToObject<double>(), 2);
                var lavalinkUptime = jObj["uptime"];
                TimeSpan uptime = TimeSpan.FromMilliseconds((double)lavalinkUptime);
                builder.Append($"Memory: {memused}mb; CPU Load: {lavalinkLoad}%; Uptime: {Math.Round(uptime.TotalMinutes, 0)}:{uptime.ToString("ss")} minutes");
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
                if (jObj["type"].ToObject<string>() == "TrackEndEvent")
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
            Log(src, builder.ToString(), severity);
            #endif
        }

        static double ConvertBytesToMegabytes(long bytes)
        {
            return (bytes / 1024f) / 1024f;
        }
        #endregion

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
                    return ConsoleColor.Magenta;

                default:
                    return ConsoleColor.Cyan;
            }
        }
    }
}
