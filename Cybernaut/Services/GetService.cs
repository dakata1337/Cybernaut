using Cybernaut.DataStructs;
using Discord;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Cybernaut.Services
{
    public class GetService
    {
        public string GetJSONAsync(IGuild guild)
        {
            #region Code
            string configFile = GetConfigLocation(guild);
            var json = File.ReadAllText(configFile);
            return json;
            #endregion
        }

        public string GetConfigLocation(IGuild guild)
        {
            return $@"{GlobalData.Config.ConfigLocation}\{guild.Id}.json";
        }
    }
}
