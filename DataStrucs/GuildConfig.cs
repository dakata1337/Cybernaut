using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace Discord_Bot.DataStrucs
{
    public class GuildConfig
    {
        public ulong guildId { get; set; }
        public string prefix { get; set; }
        public List<ulong> whitelistedChannels { get; set; }
        public bool isLooping { get; set; }
        public int volume { get; set; }
        public JArray playlists { get; set; }
    }
}
