using Cybernaut.DataStructs;
using Discord;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Cybernaut.Services
{
    public class GetService
    {
        public static string GetJSONAsync(IGuild guild)
        {
            #region Code
            string configFile = GetConfigLocation(guild).ToString();
            var json = File.ReadAllText(configFile);
            return json;
            #endregion
        }

        public static string GetConfigLocation(IGuild guild)
        {
            return $@"{GlobalData.Config.ConfigLocation}\{guild.Id}.json";
        }

        public static string GetPrefix(string configFile)
        {
            #region Code
            dynamic stuff = JsonConvert.DeserializeObject(File.ReadAllText(configFile));
            return stuff.Prefix;
            #endregion
        }

        public static Emoji[] GetNumbersEmojisAndCancel()
        {
            #region Code
            Emoji[] emotes = new Emoji[6];
            emotes[0] = new Emoji("1️⃣");
            emotes[1] = new Emoji("2️⃣");
            emotes[2] = new Emoji("3️⃣");
            emotes[3] = new Emoji("4️⃣");
            emotes[4] = new Emoji("5️⃣");
            emotes[5] = new Emoji("🚫");
            return emotes;
            #endregion
        }
    }
}
