using Discord;
using Discord.Commands;
using Cybernaut.DataStructs;
using Cybernaut.Handlers;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Discord.Rest;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Discord.WebSocket;
using System.Linq;

namespace Cybernaut.Services
{
    public class CommandsService
    {
        public async Task<Embed> GetInvite(SocketCommandContext context)
        {
            if (GlobalData.Config.BotInviteLink == null)
                return await EmbedHandler.CreateErrorEmbed("Invite Error!", $"{context.Message.Author.Mention} i'm sorry but this bot is ether private\nor there is no invite link provided by the Bot Owner.");
            return await EmbedHandler.CreateBasicEmbed("Invite Created.", $"{context.Message.Author.Mention} [here]({GlobalData.Config.BotInviteLink}) is the invite you asked for.", Color.Blue);
        }

        public async Task<Embed> Authentication(string arg, IRole role, SocketCommandContext context)
        {
            switch (arg)
            {
                case "enable":
                    return await EnableAuthentication(context);
                case "disable":
                    return await DisableAuthentication(context);
                case "role":
                    return await ChangeAuthenticationRole(role, context);
                default:
                    return await EmbedHandler.CreateErrorEmbed("Configuration Error!", $"{arg} is not a valid argument.");
            }
        }

        #region Auth Functions
        private async Task<Embed> EnableAuthentication(SocketCommandContext context)
        {
            string configFile = $@"{GlobalData.Config.ConfigLocation}\{context.Guild.Id}.json";

            #region Enable Authentication 
            var json = File.ReadAllText(configFile);
            var jObj = JsonConvert.DeserializeObject<JObject>(json);

            jObj["AuthEnabled"] = true;

            string output = JsonConvert.SerializeObject(jObj, Formatting.Indented);
            File.WriteAllText(configFile, output);
            #endregion 

            return await EmbedHandler.CreateBasicEmbed("Authentication Enabled.", $"Authentication is now enabled!", Color.Blue);
        }

        private async Task<Embed> DisableAuthentication(SocketCommandContext context)
        {
            string configFile = $@"{GlobalData.Config.ConfigLocation}\{context.Guild.Id}.json";

            #region Disable Authentication
            var json = File.ReadAllText(configFile);
            var jObj = JsonConvert.DeserializeObject<JObject>(json);

            jObj["AuthEnabled"] = false;

            string output = JsonConvert.SerializeObject(jObj, Formatting.Indented);
            File.WriteAllText(configFile, output);
            #endregion

            return await EmbedHandler.CreateBasicEmbed("Authentication Disabled.", $"Authentication is now disabled!", Color.Blue);
        }

        private async Task<Embed> ChangeAuthenticationRole(IRole role, SocketCommandContext context)
        {
            string configFile = $@"{GlobalData.Config.ConfigLocation}\{context.Guild.Id}.json";

            #region Changes the auth role
            var json = File.ReadAllText(configFile);
            var jObj = JsonConvert.DeserializeObject<JObject>(json);

            jObj["AuthRole"] = role.Id; 

            string output = JsonConvert.SerializeObject(jObj, Formatting.Indented);
            File.WriteAllText(configFile, output);
            #endregion

            return await EmbedHandler.CreateBasicEmbed("Authentication Configuration.", $"{role.Name} is now the authentication role.", Color.Blue);
        }
        #endregion
    }
}
