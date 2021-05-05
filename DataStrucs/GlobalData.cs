using Newtonsoft.Json;
using Discord_Bot.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace Discord_Bot.DataStrucs
{
    public class GlobalData
    {
        private static string configName = "config.json";
        private static string configPath { get; set; } = $"{configName}";
        public static BotConfig Config { get; set; }
        public static Dictionary<ulong, GuildConfig> GuildConfigs = new Dictionary<ulong, GuildConfig>();

        public static void Initialize()
        {
            var json = string.Empty;

            if (!File.Exists(configPath))
            {
                json = JsonConvert.SerializeObject(GenerateNewConfig(), Formatting.Indented);
                File.WriteAllText(configPath, json, new UTF8Encoding(true));
                LoggingService.Log("Config", $"{configName} was created. Please modify the config to your liking.");
                Thread.Sleep(-1);
            }

            json = File.ReadAllText(configPath, new UTF8Encoding(true));
            Config = JsonConvert.DeserializeObject<BotConfig>(json);

            if(Config.token == "YourToken")
            {
                LoggingService.Log("Config", "You didn't configure the config!", ConsoleColor.Red);
                Thread.Sleep(-1);
            }
        }

        private static BotConfig GenerateNewConfig() => new BotConfig
        {
            token = "YourToken",
            gameStatus = "",
            defaultPrefix = "``",
            cacheUpdateTime = 2000,
            DB_Server = "",
            DB_Port = "",
            DB_User = "",
            DB_Password = "",
            DB_Database = ""
        };
    }
}
