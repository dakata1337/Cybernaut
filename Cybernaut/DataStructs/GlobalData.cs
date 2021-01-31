using Cybernaut.Services;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Cybernaut.DataStructs
{
    public class GlobalData
    {
        private static string ConfigPath { get; set; } = "config.json";
        public static BotConfig Config { get; set; }
        public static int JoinedGuilds { get; set; }

        public async Task InitializeAsync()
        {
            var json = string.Empty;

            if (!File.Exists(ConfigPath))
            {
                json = JsonConvert.SerializeObject(GenerateNewConfig(), Formatting.Indented);
                File.WriteAllText(ConfigPath, json, new UTF8Encoding(false));
                await LoggingService.LogCriticalAsync("Config", "config.json was created. Please modify the config to your liking.");
                await Task.Delay(-1);
            }

            json = File.ReadAllText(ConfigPath, new UTF8Encoding(false));
            Config = JsonConvert.DeserializeObject<BotConfig>(json);

            if (!Directory.Exists(Config.ConfigLocation))
                Directory.CreateDirectory(Config.ConfigLocation);
        }

        private static BotConfig GenerateNewConfig() => new BotConfig
        {
            DiscordToken = "Put your bot token here",
            DefaultPrefix = "!",
            GameStatus = "Game name here",
            ConfigLocation = "configs",
            logToFile = false,
            BotInviteLink = null,
            BotOwner = "YourName#0001"
        };
    }
}
