using Cybernaut.DataStructs;
using Cybernaut.Handlers;
using Discord;
using Discord.Commands;
using Discord.Rest;
using Discord.WebSocket;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Victoria;

namespace Cybernaut.Services
{
    public static class ConfigService
    {

        public static async Task<Embed> ChangePrefix(SocketCommandContext context, string prefix)
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
            string configFile = GetService.GetConfigLocation(context.Guild);
            bool autoWhitelist = false;

            var jObj = GetService.GetJObject(context.Guild);

            if ((string)jObj["Prefix"] == prefix)
                return await EmbedHandler.CreateBasicEmbed("Configuration Error.", '\u0022' + prefix + '\u0022' + " is already the prefix.", Color.Orange);

            jObj["Prefix"] = prefix;

            File.WriteAllText(configFile, JsonConvert.SerializeObject(jObj, Formatting.Indented));

            return await EmbedHandler.CreateBasicEmbed("Configuration Changed.",  autoWhitelist == true ? 
                $"The prefix was successfully changed to \"{prefix}\".\nAdded **{context.Channel.Name}** to the channel whitelist." : 
                $"The prefix was successfully changed to \"{prefix}\".", Color.Blue);
            #endregion
        }

        public static async Task<Embed> WhiteList(SocketCommandContext context, IChannel channel, string arg)
        {
            string configFile = GetService.GetConfigLocation(context.Guild);

            #region Checks

            #region Text Channel Check
            if (channel != null)
            {
                if (context.Guild.GetChannel(channel.Id) != context.Guild.GetTextChannel(channel.Id)) //Checks if the selected channel is text channel
                    return await EmbedHandler.CreateBasicEmbed("Configuration Error.", $"{context.Guild.GetChannel(channel.Id)} is not a text channel.", Color.Orange);
            }
            #endregion

            #endregion

            #region Code
            var jObj = GetService.GetJObject(context.Guild);

            ulong[] whitelistedChannels = jObj["whitelistedChannels"].ToObject<ulong[]>();
            List<ulong> newList = new List<ulong>(whitelistedChannels);

            switch (arg)
            {
                case "add":
                    #region Checks

                    #region Is Channel Mentioned
                    if(channel is null)
                        return await EmbedHandler.CreateErrorEmbed("Configuration Error.", $"No channel specified.");
                    #endregion

                    #region Channel Check
                    if (context.Guild.GetChannel(channel.Id) != context.Guild.GetTextChannel(channel.Id)) //Checks if the selected channel is text channel
                        return await EmbedHandler.CreateErrorEmbed("Configuration Error.", $"{context.Guild.GetChannel(channel.Id)} is not a text channel.");
                    #endregion

                    #region Whitelist Limit Check
                    if (whitelistedChannels.Length > 100) //Limits the whitelisted channels to avoid massive file sizes
                    {
                        return await EmbedHandler.CreateBasicEmbed("Configuration Error.", $"You have reached the maximum of 100 whitelisted channels.", Color.Orange);
                    }
                    #endregion

                    #region Check if whitelisted
                    foreach (ulong item in whitelistedChannels) 
                    {
                        if (item == channel.Id)
                            return await EmbedHandler.CreateBasicEmbed("Configuration Error.", $"{context.Guild.GetChannel(item)} is already whitelisted!", Color.Orange);
                    }
                    #endregion

                    #endregion
                    #region Add Code

                    //Add the channel id to a List
                    newList.Add(channel.Id);

                    //Overwrite the jsonObj file with the updated array
                    jObj["whitelistedChannels"] = JToken.FromObject(newList.ToArray());

                    //Saving to file
                    File.WriteAllText(configFile,
                        JsonConvert.SerializeObject(jObj, Formatting.Indented));
                    #endregion
                    return await EmbedHandler.CreateBasicEmbed("Configuration Changed.", $"{context.Guild.GetChannel(channel.Id)} was whitelisted.", Color.Blue);

                case "remove":
                    #region Checks

                    #region Minimum Check
                    if (newList.Count == 1)
                        return await EmbedHandler.CreateErrorEmbed("Configuration Error.", $"You can't have less than 1 whitelisted channel.");
                    #endregion

                    #region Is Channel Mentioned
                    if (channel is null)
                        return await EmbedHandler.CreateErrorEmbed("Configuration Error.", $"No channel specified.");
                    #endregion

                    #region Channel Check
                    if (context.Guild.GetChannel(channel.Id) != context.Guild.GetTextChannel(channel.Id)) //Checks if the selected channel is text channel
                        return await EmbedHandler.CreateErrorEmbed("Configuration Error.", $"{context.Guild.GetChannel(channel.Id)} is not a text channel.");
                    #endregion

                    #region In List Check
                    bool found = false;
                    foreach (ulong item in whitelistedChannels)
                    {
                        if (item == channel.Id)
                        {
                            found = true;
                            break;
                        }
                    }

                    if(!found)
                        return await EmbedHandler.CreateErrorEmbed("Configuration Error.", $"{context.Guild.GetChannel(channel.Id)} is not whitelisted.");
                    #endregion

                    #endregion
                    #region Remove Code
                    newList.Remove(channel.Id);

                    jObj["whitelistedChannels"] = JToken.FromObject(newList.ToArray());

                    File.WriteAllText(configFile,
                        JsonConvert.SerializeObject(jObj, Formatting.Indented));
                    #endregion
                    return await EmbedHandler.CreateBasicEmbed("Configuration Changed.", $"{context.Guild.GetChannel(channel.Id)} was removed from the whitelist.", Color.Blue);
                case "list":
                    #region Read Whitelisted Channels
                    StringBuilder builder = new StringBuilder();
                    builder.Append("Whitelisted channels:\n");
                    for (int i = 0; i < whitelistedChannels.Length; i++)
                    {
                        var whitelistedChannel = context.Guild.GetChannel(whitelistedChannels[i]);
                        builder.Append($"{i + 1}. {whitelistedChannel.Name} (ID: {whitelistedChannel.Id})\n");
                    }
                    #endregion
                    return await EmbedHandler.CreateBasicEmbed("Whitelist, List", $"{builder}", Color.Blue);
                default:
                    return await EmbedHandler.CreateErrorEmbed("Configuration Error.", $"{arg} is not a valid argument. Please type {jObj["Prefix"]}commands for help.");
            }
            #endregion
        }

        public static async Task<Embed> Authentication(string arg, IRole role, SocketCommandContext context)
        {
            #region Cases
            switch (arg)
            {
                case "enable":
                    return await EnableAuthentication(context);
                case "disable":
                    return await DisableAuthentication(context);
                case "role":
                    return await ChangeAuthenticationRole(role, context);
                case "require":
                    return await RequireCAPTCHA(context);
                case null:
                    return await AuthenticationStatus(context);
                default:
                    return await EmbedHandler.CreateErrorEmbed("Configuration Error!", $"{arg} is not a valid argument.");
            }
            #endregion
        }

        #region Auth Functions
        private static async Task<Embed> EnableAuthentication(SocketCommandContext context)
        {
            string configFile = GetService.GetConfigLocation(context.Guild);

            #region Enable Authentication 
            var jObj = GetService.GetJObject(context.Guild);

            if ((bool)jObj["GiveRoleOnJoin"] == true)
                return await EmbedHandler.CreateErrorEmbed("Configuration Error!", "Authentication is already enabled.");

            jObj["GiveRoleOnJoin"] = true;

            string output = JsonConvert.SerializeObject(jObj, Formatting.Indented);
            File.WriteAllText(configFile, output);
            #endregion 

            return await EmbedHandler.CreateBasicEmbed("Authentication Enabled.", $"Authentication is now enabled!", Color.Blue);
        }


        private static async Task<Embed> DisableAuthentication(SocketCommandContext context)
        {
            string configFile = GetService.GetConfigLocation(context.Guild);

            #region Disable Authentication
            var jObj = GetService.GetJObject(context.Guild);

            if ((bool)jObj["GiveRoleOnJoin"] == false)
                return await EmbedHandler.CreateErrorEmbed("Configuration Error!", "Authentication is already disabled.");

            jObj["GiveRoleOnJoin"] = false;

            string output = JsonConvert.SerializeObject(jObj, Formatting.Indented);
            File.WriteAllText(configFile, output);
            #endregion

            return await EmbedHandler.CreateBasicEmbed("Authentication Disabled.", $"Authentication is now disabled!", Color.Blue);
        }

        private static async Task<Embed> ChangeAuthenticationRole(IRole role, SocketCommandContext context)
        {
            string configFile = GetService.GetConfigLocation(context.Guild);

            #region Changes the auth role
            var jObj = GetService.GetJObject(context.Guild);

            if ((ulong)jObj["RoleOnJoin"] == role.Id)
                return await EmbedHandler.CreateErrorEmbed("Configuration Error!", $"\"{role.Name}\" is already the authentication role.");

            jObj["RoleOnJoin"] = role.Id;

            string output = JsonConvert.SerializeObject(jObj, Formatting.Indented);
            File.WriteAllText(configFile, output);
            #endregion

            return await EmbedHandler.CreateBasicEmbed("Authentication Configuration.", $"\"{role.Name}\" is now the authentication role.", Color.Blue);
        }

        private static async Task<Embed> AuthenticationStatus(SocketCommandContext context)
        {
            #region JSON
            var jObj = GetService.GetJObject(context.Guild);

            bool isEnabled = (bool)jObj["GiveRoleOnJoin"];
            ulong roleID = (ulong)jObj["RoleOnJoin"];
            string role = roleID == 0 ? "not set" : $"{context.Guild.GetRole(roleID).Name} (ID: {roleID})";
            bool RequireCAPTCHA = (bool)jObj["RequireCAPTCHA"];
            #endregion

            #region Custom Embed
            var fields = new List<EmbedFieldBuilder>();
            fields.Add(new EmbedFieldBuilder
            {
                Name = "**Settings**",
                Value = $"Enabled: {isEnabled}\n" +
                $"Role: {role}\n" +
                $"Require CAPTCHA: {RequireCAPTCHA}",
                IsInline = false
            });
            #endregion

            return await EmbedHandler.CreateCustomEmbed(context.Guild, Color.Blue, fields, "Authentication Status", false);
        }

        private static async Task<Embed> RequireCAPTCHA(SocketCommandContext context)
        {
            string configFile = GetService.GetConfigLocation(context.Guild);

            #region Changes RequireCAPTCHA
            var jObj = GetService.GetJObject(context.Guild);

            if ((bool)jObj["RequireCAPTCHA"] == false)
            {
                jObj["RequireCAPTCHA"] = true;
            }
            else
            {
                jObj["RequireCAPTCHA"] = false;
            }


            string output = JsonConvert.SerializeObject(jObj, Formatting.Indented);
            File.WriteAllText(configFile, output);
            #endregion

            string status = ((bool)jObj["RequireCAPTCHA"] == true ? "enabled" : "disabled");
            return await EmbedHandler.CreateBasicEmbed("Authentication Configuration.", $"RequireCAPTCHA is now {status}.", Color.Blue);
        }
        #endregion
    }
}