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
using System.Net;

namespace Cybernaut.Services
{
    public static class CommandsService
    {
        public static async Task<Embed> GetInvite(SocketCommandContext context)
        {
            #region Code
            if (GlobalData.Config.BotInviteLink == null)
                return await EmbedHandler.CreateErrorEmbed("Invite Error!", $"{context.Message.Author.Mention} i'm sorry but this bot is ether private\n" +
                    $"or there is no invite link provided by the Bot Owner.");
            return await EmbedHandler.CreateBasicEmbed("Invite Created.", $"{context.Message.Author.Mention} " +
                $"[here]({GlobalData.Config.BotInviteLink}) is the invite you asked for.", Color.Blue);
            #endregion
        }

        public static async Task<Embed> Mute(IUser user, string time, SocketCommandContext context)
        {
            #region Code
            string configFile = GetService.GetConfigLocation(context.Guild);
            var jObj = GetService.GetJObject(context.Guild);

            JObject[] ogArray = jObj["mutedUsers"].ToObject<JObject[]>();
            List<JObject> newList = new List<JObject>();

            //Checks if the user is muted already
            if (!(ogArray is null))
            {
                newList = new List<JObject>(ogArray);
                foreach (JObject item in ogArray)
                {
                    if (!(item is null))
                    {
                        var userInfo = item.ToObject<UserInfo>();
                        if (userInfo.Id == user.Id)
                        {
                            StringBuilder builder = new StringBuilder();
                            builder.Append(userInfo.ExpiresOn.Hour + "h ");
                            builder.Append(userInfo.ExpiresOn.Minute + "m ");
                            builder.Append(userInfo.ExpiresOn.Second + "s ");
                            builder.Append("on " + userInfo.ExpiresOn.ToString("dd.MM.yyyy"));
                            return await EmbedHandler.CreateErrorEmbed("Admin, Mute", $"{context.Guild.GetUser(user.Id).Username} is already muted until {builder}!");
                        }
                    }
                }
            }

            //Parses mute time 
            DateTime expiresOn = DateTime.Now;
            int latterChar = 0;
            for (int i = 0; i < time.Length; i++)
            {
                if (Char.IsLetter(time[i]))
                {
                    latterChar = i;
                    break;
                }
            }

            //Adds the mute time to DateTime.Now
            double toAdd = double.Parse(time.Remove(latterChar, time.Length - latterChar));
            switch (time[latterChar].ToString().ToLower())
            {
                case "m":
                    expiresOn = expiresOn.AddMinutes(toAdd);
                    break;
                case "h":
                    expiresOn = expiresOn.AddHours(toAdd);
                    break;
                case "d":
                    expiresOn = expiresOn.AddDays(toAdd);
                    break;
                default:
                    return await EmbedHandler.CreateErrorEmbed("Admin, Mute", $"{time[latterChar]} is not a proper time format.");
            }

            //Makes user info
            UserInfo newMutedUser = new UserInfo { Id = user.Id, GuildId = context.Guild.Id, ExpiresOn = expiresOn };

            //Adds user info
            newList.Add(JObject.FromObject(newMutedUser));


            //Saves it to the config
            jObj["mutedUsers"] = JToken.FromObject(newList);
            File.WriteAllText(configFile,
                       JsonConvert.SerializeObject(jObj, Formatting.Indented));
            return await EmbedHandler.CreateBasicEmbed("Admin, Mute", $"{context.Guild.GetUser(newMutedUser.Id)} was muted for {time}", Color.Blue);
            #endregion
        }

        public static async Task<Embed> Unmute(IUser user, SocketCommandContext context)
        {
            #region Code
            string configFile = GetService.GetConfigLocation(context.Guild);
            var jObj = GetService.GetJObject(context.Guild);

            JObject[] ogArray = jObj["mutedUsers"].ToObject<JObject[]>();
            List<JObject> newList = new List<JObject>();

            //Checks if the user is muted 
            if (!(ogArray is null))
            {
                newList = new List<JObject>(ogArray);
                foreach (JObject item in ogArray)
                {
                    if (!(item is null))
                    {
                        var userInfo = item.ToObject<UserInfo>();
                        newList.Remove(item);
                        jObj["mutedUsers"] = JToken.FromObject(newList.ToArray());

                        File.WriteAllText(configFile,
                            JsonConvert.SerializeObject(jObj, Formatting.Indented));
                        return await EmbedHandler.CreateBasicEmbed("Admin, Unmute", $"{user.Username} was unmuted.", Color.Blue);
                    }
                }
            }
            return await EmbedHandler.CreateBasicEmbed("Admin, Unmute", $"{user.Username} wasn't unmuted.", Color.Blue);
            #endregion
        }

        public static async Task<Embed> Kick(IUser user, string reason, SocketCommandContext context)
        {
            #region Code
            SocketGuildUser guildUser = (SocketGuildUser)user;
            try
            {
                await guildUser.KickAsync(reason is null ?
                    $"Not set" : $"{reason}");
            }
            catch (Exception e)
            {
                return await EmbedHandler.CreateErrorEmbed("Admin, Kick", $"{e.Message}");
            }

            try
            {
                await user.GetOrCreateDMChannelAsync();
                #region Custom Embed 
                var fields = new List<EmbedFieldBuilder>();
                fields.Add(new EmbedFieldBuilder
                {
                    Name = $"I'm sorry to inform you but...",
                    Value = $"You were kicked from **{context.Guild.Name}**.",
                    IsInline = false
                });

                if (!(reason is null))
                {
                    fields.Add(new EmbedFieldBuilder
                    {
                        Name = "**Reason for the kick:**",
                        Value = $"{reason}",
                        IsInline = false
                    });
                }

                fields.Add(new EmbedFieldBuilder
                {
                    Name = "Still having question?",
                    Value = $"You can contact **{context.Message.Author}** for more info.",
                    IsInline = false
                });
                #endregion

                await user.SendMessageAsync(embed:
                    await EmbedHandler.CreateCustomEmbed(context.Guild, Color.Red, fields, "Ouch... that hurts"));
            }
            catch
            {
                return await EmbedHandler.CreateBasicEmbed("Admin, Kick", $"**{user.Mention} has his/hers direct messages from server members disabled!**\n" +
                    $"{user.Username} was kicked from the guild.", Color.Blue);
            }

            return await EmbedHandler.CreateBasicEmbed("Admin, Kick", $"{user.Username} was kicked from the guild.", Color.Blue);
            #endregion
        }

        public static async Task<Embed> Ban(IUser user, string reason, SocketCommandContext context)
        {
            #region Code
            SocketGuildUser guildUser = (SocketGuildUser)user;

            StringBuilder guildMsg = new StringBuilder();

            try
            {
                await user.GetOrCreateDMChannelAsync();
                #region Custom Embed 
                var fields = new List<EmbedFieldBuilder>();
                fields.Add(new EmbedFieldBuilder
                {
                    Name = $"I'm sorry to inform you but...",
                    Value = $"You were banned from **{context.Guild.Name}**.",
                    IsInline = false
                });

                if (!(reason is null))
                {
                    fields.Add(new EmbedFieldBuilder
                    {
                        Name = "**Reason for the ban:**",
                        Value = $"{reason}",
                        IsInline = false
                    });
                }

                fields.Add(new EmbedFieldBuilder
                {
                    Name = "Still having question?",
                    Value = $"You can contact **{context.Message.Author}** for more info.",
                    IsInline = false
                });
                #endregion

                await user.SendMessageAsync(embed:
                    await EmbedHandler.CreateCustomEmbed(context.Guild, Color.Red, fields, "Ouch... that hurts"));
            }
            catch
            {
                guildMsg.Append($"**{user.Mention} has his/hers direct messages from server members disabled!**\n");
            }

            try
            {
                await guildUser.BanAsync(0, reason is null ?
                    $"Not set" : $"{reason}");
                guildMsg.Append($"{user.Username} was banned from the guild.");
            }
            catch (Exception e)
            {
                return await EmbedHandler.CreateErrorEmbed("Admin, Kick", $"{e.Message}");
            }

            return await EmbedHandler.CreateBasicEmbed("Admin, Kick", $"{guildMsg}", Color.Blue);
            #endregion
        }
    }
}