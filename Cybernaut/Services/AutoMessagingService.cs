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
        GetService getService = new GetService();

        public Task OnUserJoin(SocketGuildUser user)
        {
            #region Code
            if (user.IsBot)
                return Task.CompletedTask;

            var t = new Thread(async () => await UserAuth(user));
            t.Start();

            return Task.CompletedTask;
            #endregion
        }

        public async Task<Task> UserAuth(SocketGuildUser user)
        {
            #region Checks
            if (user.IsBot)
                await Task.CompletedTask;
            #endregion

            #region Code
            #region Reading config
            string configFile = getService.GetConfigLocation(user.Guild);

            var json = File.ReadAllText(configFile);
            var jObj = JsonConvert.DeserializeObject<JObject>(json);
            #endregion

            //Get Auth Role
            IRole role = user.Guild.GetRole((ulong)jObj["AuthRole"]);

            //Checks if an auth role is set
            if (role is null)
                return Task.CompletedTask;

            #region AuthEnabled Check
            //If auth is disable give role straight away
            if (jObj["AuthEnabled"].ToObject<bool>() == false)
            {
                await user.AddRoleAsync(role);
                return Task.CompletedTask;
            }
            #endregion

            //Get DMs
            await user.GetOrCreateDMChannelAsync();

            //Reaction Message
            IMessage message = await user.SendMessageAsync(embed: await EmbedHandler.CreateBasicEmbed($"Are you a robot?", $"Please confirm that you are not a robot 🤖", Color.Blue));
            var checkEmoji = new Emoji("✅");
            await message.AddReactionAsync(checkEmoji);

            #region Reaction Check
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

                        //Welcome message
                        await user.SendMessageAsync(embed: await EmbedHandler.CreateBasicEmbed($"I'm glad you came!", $"Welcome to {user.Guild.Name}", Color.Blue));
                        break;
                    }
                }
            }
            #endregion

            #endregion

            return Task.CompletedTask;
        }

        public async Task OnGuildJoin(SocketGuild guild)
        {
            #region Code
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
                "If you want to whitelist another channel type `!whitelist add #your-channel` (Must be tagged).",
                IsInline = false
            }); 

            fields.Add(new EmbedFieldBuilder
            {
                Name = "Experiencing problems?",
                Value = $"If you experience any problems report them to **{GlobalData.Config.BotOwner}**.",
                IsInline = false
            });
            #endregion

            var channel = guild.DefaultChannel as SocketTextChannel;

            await channel.SendMessageAsync(embed: await EmbedHandler.CreateCustomEmbed(guild, Color.Blue, fields, "I have arrived!", true)); //Sends the Embed
            #endregion
        }
    }
}
