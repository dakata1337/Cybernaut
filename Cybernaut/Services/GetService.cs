using Cybernaut.DataStructs;
using Discord;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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
        public static JObject GetJObject(IGuild guild)
        {
            #region Code
            //Get Config Location
            string configFile = GetConfigLocation(guild).ToString();

            //Config string
            var json = File.ReadAllText(configFile);

            //Deserialize Config string
            return (JObject)JsonConvert.DeserializeObject(json);
            #endregion
        }

        public static string GetConfigLocation(IGuild guild)
        {
            //Returns config location
            return $@"{GlobalData.Config.ConfigLocation}\{guild.Id}.json";
        }

        public static string GetPrefix(IGuild guild)
        {
            #region Code
            var jObj = GetService.GetJObject(guild);
            return jObj["Prefix"].ToString();
            #endregion
        }
    }
}
