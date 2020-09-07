using Cybernaut.DataStructs;
using Cybernaut.Handlers;
using Discord;
using Discord.WebSocket;
using System.Threading.Tasks;
using System.Collections.Generic;
using Discord.Commands;
using System;
using Discord.Rest;
using System.Linq;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Threading;

namespace Cybernaut.Services
{
    public class AutoMessagingService
    {
        public async Task<Task> OnUserJoin(SocketGuildUser user)
        {
            var t = new Thread(() => UserAuth(user));
            t.Start();

            return Task.CompletedTask;
        }

        public async Task<Task> UserAuth(SocketGuildUser user)
        {
            #region Reading config
            string configFile = $@"{GlobalData.Config.ConfigLocation}\{user.Guild.Id}.json";

            var json = File.ReadAllText(configFile);
            var jObj = JsonConvert.DeserializeObject<JObject>(json);
            #endregion

            if (jObj["AuthEnabled"].ToObject<bool>() == false) //Checks if auth is enabled
                return Task.CompletedTask;

            await user.GetOrCreateDMChannelAsync();

            await user.SendMessageAsync(embed: await EmbedHandler.CreateBasicEmbed($"I'm glad you came!", $"Welcome to {user.Guild.Name}", Color.Blue));

            IMessage message = await user.SendMessageAsync(embed: await EmbedHandler.CreateBasicEmbed($"Are you a robot?", $"Please confirm that you are not a robot 🤖", Color.Blue));

            var checkEmoji = new Emoji("✅");
            await message.AddReactionAsync(checkEmoji);

            IRole role = user.Guild.GetRole((ulong)jObj["AuthRole"]);
            bool loop = true;
            while (loop)
            {
                var reactedUsers = await message.GetReactionUsersAsync(checkEmoji, 1).FlattenAsync();
                foreach (var item in reactedUsers)
                {
                    if (!item.IsBot && item.Id == user.Id)
                    {
                        await user.AddRoleAsync(role);
                        await message.DeleteAsync();
                        loop = false;
                        break;
                    }
                }
            }
            return Task.CompletedTask;
        }

        public async Task OnGuildJoin(SocketGuild guild)
        {
            #region Custom Embed 
            var fields = new List<EmbedFieldBuilder>();
            fields.Add(new EmbedFieldBuilder
            {
                Name = $"Setup time!",
                Value = $"We have some setup to do! " +
                $"Type `{GlobalData.Config.DefaultPrefix}prefix YourPrefixHere`.",
                IsInline = false
            });
            fields.Add(new EmbedFieldBuilder
            {
                Name = "**NOTE**",
                Value = "By default, the channel in which you will type the prefix command, will be whitelisted. " +
                "If you want to whitelist another channel type `!whitelistadd #your-channel` (Must be tagged).",
                IsInline = false
            }); 

            fields.Add(new EmbedFieldBuilder
            {
                Name = "Experiencing problems?",
                Value = $"If you experience any problems report them to **{GlobalData.Config.BotOwner}**.",
                IsInline = false
            });
            #endregion

            var channel = guild.SystemChannel as SocketTextChannel; //Gets the channel to send the message in
            await channel.SendMessageAsync(embed: await CreateCustomEmbed(guild, Color.Blue, fields, "I have arrived!")); //Sends the Embed
        }

        public async Task UserBanned(SocketUser user, SocketGuild guild)
        {
            var channel = guild.SystemChannel as SocketTextChannel; //Gets the channel to send the message in
            await channel.SendMessageAsync(embed: await EmbedHandler.CreateBasicEmbed("User Banned.", $"{user.Mention} was banned from the server!", Color.Red)); //Sends the Embed
        }


        //Custom embeds
        public async Task<Embed> CreateCustomEmbed(SocketGuild guild, Color color, List<EmbedFieldBuilder> fields, string embedTitle)
        {
            var embed = await Task.Run(() => new EmbedBuilder
            {
                Title = embedTitle,
                ThumbnailUrl = guild.IconUrl,
                Timestamp = DateTime.UtcNow,
                Color = Color.DarkOrange,
                Footer = new EmbedFooterBuilder { Text = $"Thank you for choosing {guild.CurrentUser.Username}", IconUrl = guild.CurrentUser.GetAvatarUrl() },
                Fields = fields
            });
            embed.Color = color;
            return embed.Build();
        }

    }
}
