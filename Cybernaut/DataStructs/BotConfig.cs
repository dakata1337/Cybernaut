using System.Collections.Generic;
using System.IO;

namespace Cybernaut.DataStructs
{
    public class BotConfig
    {
        public string DiscordToken { get; set; }
        public string DefaultPrefix { get; set; }
        public string GameStatus { get; set; }
        public string ConfigLocation { get; set; }
        public bool logToFile { get; set; }
        public string BotInviteLink { get; set; }
        public string BotOwner { get; set; }
    }
}
