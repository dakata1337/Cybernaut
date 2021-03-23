using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace Discord_Bot.DataStrucs
{
    public class GuildConfig
    {
        public string prefix { get; set; }
        public List<ulong> whitelistedChannels { get; set; }
        public bool isLooping { get; set; }
        public int volume { get; set; }
        public JArray playlists { get; set; }
    }

    public class BotConfig
    {
        public string token { get; set; }
        public string gameStatus { get; set; }
        public string defaultPrefix { get; set; }
        public int cacheUpdateTime { get; set; }
        public string DB_Server { get; set; }
        public string DB_Port { get; set; }
        public string DB_User { get; set; }
        public string DB_Password { get; set; }
        public string DB_Database { get; set; }
    }

    public class Log
    {
        public string Message { get; set; }
        public string Source { get; set; }
        public ConsoleColor ConsoleColor { get; set; }
    }
}
