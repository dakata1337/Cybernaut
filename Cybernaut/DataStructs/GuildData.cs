using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cybernaut.DataStructs
{
    class GuildData
    {
        public static string ConfigPath { get; set; }
        public static GuildConfig Config { get; set; }

        public Task InitializeAsync()
        {
            string targetDirectory = GlobalData.Config.ConfigLocation;

            string[] fileEntries = Directory.GetFiles(targetDirectory, "*.json").Select(Path.GetFileNameWithoutExtension).Select(p => p.Substring(0)).ToArray();

            foreach (string fileName in fileEntries)
            {
                var json = File.ReadAllText($@"{targetDirectory}\{fileName}.json", new UTF8Encoding(false));
                Config = JsonConvert.DeserializeObject<GuildConfig>(json); 
            }
            return Task.CompletedTask;
        }

        public static GuildConfig GenerateNewConfig(string prefix) => new GuildConfig
        {
            Prefix = prefix == null ? "!" : prefix,
            whitelistedChannels = new List<ulong>(),
            GiveRoleOnJoin = false,            
            RoleOnJoin = 0,
            RequireCAPTCHA = false,
            usersCAPTCHA = null,
            volume = 70,
            islooping = false,
            mutedUsers = null
        };
    }
}
