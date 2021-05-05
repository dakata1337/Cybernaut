using Discord;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;
using Victoria;

namespace Discord_Bot.DataStrucs
{
    public class GuildConfig
    {
        public string Prefix { get; set; }
        public List<ulong> WhitelistedChannels { get; set; }
        public bool IsLooping { get; set; }
        public int Volume { get; set; }
        public JArray Playlists { get; set; }
    }

    public class Playlist
    {
        public string Name;
        public List<Song> Tracks;
    }

    public class Song
    {
        public string Name;
        public string Url;
    }

    public class TrackRequest
    {
        public Embed Embed;
        public LavaTrack Track;
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

    public class Crypto
    {
        public string cryptoName;
        public double price;
    }
}
