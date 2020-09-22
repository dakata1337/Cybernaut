using Cybernaut.DataStructs;
using Cybernaut.Handlers;
using Discord;
using Discord.Commands;
using Discord.Rest;
using Discord.WebSocket;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cybernaut.Services
{
    public class ConfigService
    {
        public List<ulong> whitelistedChannels { get; set; }

        public async Task<Embed> ChangePrefix(SocketCommandContext context, string prefix)
        {
            #region Checks

            #region Prefix Lenght Check
            if (prefix.Length > 15)
            {
                return await EmbedHandler.CreateBasicEmbed("Configuration Error.", $"The prefix is to long. Must be 15 characters or less.", Color.Orange);
            }
            #endregion

            #endregion

            #region Code
            var json = string.Empty;
            string configFile = $@"{GlobalData.Config.ConfigLocation}\{context.Guild.Id}.json";
            bool autoWhitelist = false;

            if (File.Exists(configFile))
            {
                json = File.ReadAllText(configFile);

                dynamic jsonObj = JsonConvert.DeserializeObject(json);

                if(jsonObj.Prefix == prefix)
                {
                    return await EmbedHandler.CreateBasicEmbed("Configuration Error.", '\u0022' + prefix + '\u0022' + " is already the prefix.", Color.Orange);
                }

                jsonObj["Prefix"] = prefix;

                string output = JsonConvert.SerializeObject(jsonObj, Formatting.Indented);
                File.WriteAllText(configFile, output);
            }
            else
            {
                json = JsonConvert.SerializeObject(GenerateNewConfig(prefix), Formatting.Indented);

                dynamic count = JsonConvert.DeserializeObject(json);

                var jObj = JsonConvert.DeserializeObject<JObject>(json);
                if (count["whitelistedChannels"].Count == 0)
                {
                    // or use `JObject.Parse`
                    ulong[] ts = new ulong[1];
                    ts[0] = context.Channel.Id;
                    jObj["whitelistedChannels"] = JToken.FromObject(ts);
                    autoWhitelist = true;
                }

                string output = JsonConvert.SerializeObject(jObj, Formatting.Indented);
                File.WriteAllText(configFile, output, new UTF8Encoding(false));
            }
            return await EmbedHandler.CreateBasicEmbed("Configuration Changed.",  autoWhitelist == true ? $"The prefix was successfully changed to \u0022{prefix}\u0022.\nAdded {context.Channel.Name} to the channel whitelist." : 
                $"The prefix was successfully changed to \u0022{prefix}\u0022.", Color.Blue);
            #endregion
        }

        public async Task<Embed> WhiteList(SocketCommandContext context, ulong channelID, string arg)
        {
            string configFile = GetConfigLocation(context);

            #region Checks

            #region Config Check
            if (!File.Exists(configFile))
                return await EmbedHandler.CreateBasicEmbed("Configuration Needed!", $"Please type `{GlobalData.Config.DefaultPrefix}prefix YourPrefixHere` to configure the bot.", Color.Orange);
            #endregion

            #region Text Channel Check
            if (context.Guild.GetChannel(channelID) != context.Guild.GetTextChannel(channelID)) //Checks if the selected channel is text channel
                return await EmbedHandler.CreateBasicEmbed("Configuration Error.", $"{context.Guild.GetChannel(channelID)} is not a text channel.", Color.Orange);
            #endregion

            #endregion

            #region Code
            var json = File.ReadAllText(configFile);
            dynamic jsonObj = JsonConvert.DeserializeObject(json);

            ulong[] ogArray = jsonObj.whitelistedChannels.ToObject<ulong[]>();
            var jObj = JsonConvert.DeserializeObject<JObject>(json);

            List<ulong> list = new List<ulong>(ogArray);
            string output = string.Empty;

            switch (arg)
            {
                case "add":
                    #region Checks

                    #region Channel Check
                    if (context.Guild.GetChannel(channelID) != context.Guild.GetTextChannel(channelID)) //Checks if the selected channel is text channel
                        return await EmbedHandler.CreateBasicEmbed("Configuration Error.", $"{context.Guild.GetChannel(channelID)} is not a text channel.", Color.Orange);
                    #endregion

                    #region Whitelist Limit Check
                    if (ogArray.Length > 100) //Limits the whitelisted channels to avoid massive file sizes
                    {
                        return await EmbedHandler.CreateBasicEmbed("Configuration Error.", $"You have reached the maximum of 100 whitelisted channels.", Color.Orange);
                    }
                    #endregion

                    #endregion
                    #region Add Code
                    foreach (ulong item in ogArray) //Checks if the channel is already whitelisted
                    {
                        if (item == channelID)
                        {
                            return await EmbedHandler.CreateBasicEmbed("Configuration Error.", $"{context.Guild.GetChannel(item)} is already whitelisted!", Color.Orange);
                        }
                    }

                    list.Add(channelID);
                    ogArray = list.ToArray();

                    // or use `JObject.Parse`
                    jObj["whitelistedChannels"] = JToken.FromObject(ogArray);

                    output = JsonConvert.SerializeObject(jObj, Formatting.Indented);
                    File.WriteAllText(configFile, output);
                    #endregion
                    return await EmbedHandler.CreateBasicEmbed("Configuration Changed.", $"{context.Guild.GetChannel(channelID)} was whitelisted.", Color.Blue);

                case "remove":
                    #region Checks

                    #region Channel Check
                    if (context.Guild.GetChannel(channelID) != context.Guild.GetTextChannel(channelID)) //Checks if the selected channel is text channel
                        return await EmbedHandler.CreateErrorEmbed("Configuration Error.", $"{context.Guild.GetChannel(channelID)} is not a text channel.");
                    #endregion

                    #region In List Check
                    bool found = false;
                    foreach (ulong item in ogArray)
                    {
                        if (item == channelID)
                        {
                            found = true;
                            break;
                        }
                    }

                    if(!found)
                        return await EmbedHandler.CreateErrorEmbed("Configuration Error.", $"{context.Guild.GetChannel(channelID)} is not whitelisted.");
                    #endregion

                    #region Minimum Check
                    if (list.Count == 1)
                    {
                        return await EmbedHandler.CreateErrorEmbed("Configuration Error.", $"You can't have less than 1 whitelisted channel.");
                    }
                    #endregion

                    #endregion
                    #region Remove Code
                    list.Remove(channelID);
                    ogArray = list.ToArray();

                    jObj["whitelistedChannels"] = JToken.FromObject(ogArray);

                    output = JsonConvert.SerializeObject(jObj, Formatting.Indented);
                    File.WriteAllText(configFile, output);
                    #endregion
                    return await EmbedHandler.CreateBasicEmbed("Configuration Changed.", $"{context.Guild.GetChannel(channelID)} was removed from the whitelist.", Color.Blue);

                default:
                    return await EmbedHandler.CreateErrorEmbed("Configuration Error.", $"{arg} is not a valid argument. Please type {jObj["Prefix"]}commands for help.");
            }
            #endregion
        }

        private static GuildConfig GenerateNewConfig(string prefix) => new GuildConfig
        {
            Prefix = prefix == null ? "!" : prefix,
            whitelistedChannels = new List<ulong>(),
            AuthRole = 0,
            AuthEnabled = false,
            volume = 70,
            islooping = false
        };

        private string GetConfigLocation(SocketCommandContext context)
        {
            return $@"{GlobalData.Config.ConfigLocation}\{context.Guild.Id}.json";
        }
    }
}
