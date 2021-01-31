using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Discord_Bot.DataStrucs;
using Discord_Bot.Modules;
using Discord_Bot.Services;
using Microsoft.Extensions.DependencyInjection;
using MySql.Data.MySqlClient;

namespace Discord_Bot.Handlers
{
    public class GuildConfigHandler
    {
        private MySQL _mySQL;
        private MySqlConnection _connection;
        public GuildConfigHandler(IServiceProvider serviceProvider)
        {
            _mySQL = serviceProvider.GetRequiredService<MySQL>();
            _connection = _mySQL.connection;
        }

        //On Guild Join Create Config
        public async Task JoinedGuild(SocketGuild guild)
        {
            //If the guild has config, delete it
            if (_mySQL.GuildHasConfig(guild))
            {
                _mySQL.RemoveGuildConfig(guild);
            }

            //Guild Defualt Channel
            var defaultChannel = guild.DefaultChannel as SocketTextChannel;

            List<ulong> whitelistedChannels = new List<ulong>();
            if (whitelistedChannels.Count == 0)
            {
                //Add Default Channel ID
                whitelistedChannels.Add(defaultChannel.Id);

                //Create Guild Config
                _mySQL.CreateNewGuildConfig(guild);

                //Custom Embed
                var fields = new List<EmbedFieldBuilder>();
                fields.Add(new EmbedFieldBuilder
                {
                    Name = "**Please Note**",
                    Value = $"By default, {defaultChannel.Mention} is the default bot channel.\n" +
                    $"If you want to change it, type {GlobalData.Config.defaultPrefix}whitelist add #YourTextChannel",
                    IsInline = false
                });

                //Send Embed
                await defaultChannel.SendMessageAsync(embed:
                    await EmbedHandler.CreateCustomEmbed(
                        guild: guild,
                        color: Color.DarkTeal,
                        fields: fields,
                        embedTitle: "I have arrived!",
                        footer: $"Thank you for choosing {guild.CurrentUser.Username}"
                ));
            }
            await Task.CompletedTask;
        }

        //On Left Guild Delete Config
        public Task LeftGuild(SocketGuild guild)
        {
            if (_mySQL.GuildHasConfig(guild))
            {
                _mySQL.RemoveGuildConfig(guild);
            }
            return Task.CompletedTask;
        }

        public async Task<Embed> ChangePrefix(SocketCommandContext context, string newPrefix)
        {
            //If prefix lenght is more than 5 chars long
            if (newPrefix.Length > 5)
            {
                return await EmbedHandler.CreateErrorEmbed("Configuration Error.", $"The prefix is to long. Must be 5 characters or less.");
            }

            //Get Guild Config
            GlobalData.GuildConfigs.TryGetValue(context.Guild.Id, out GuildConfig config);

            //Get Guild Prefix from Config
            string oldPrefix = config.prefix;

            //If the Selected Prefix is already the Prefix
            if (oldPrefix == newPrefix)
                return await EmbedHandler.CreateErrorEmbed("Configuration Error.", $"\"{newPrefix}\" is already the prefix.");

            //Update Config
            _mySQL.UpdateGuildConfig(context.Guild, "prefix", newPrefix);

            return await EmbedHandler.CreateBasicEmbed("Configuration Changed.", $"The prefix was successfully changed to \"{newPrefix}\".");
        }

        public async Task<Embed> WhiteList(SocketCommandContext context, string arg, IChannel channel)
        {
            //Check if channel is specified ONLY when arg isnt list
            if (arg != "list")
            {
                //If no channel is specified
                if (channel is null)
                    return await EmbedHandler.CreateErrorEmbed("Configuration Error.", $"No channel specified.");

                //Checks if the selected channel is text channel
                if (context.Guild.GetChannel(channel.Id) != context.Guild.GetTextChannel(channel.Id))
                    return await EmbedHandler.CreateErrorEmbed("Configuration Error.", $"{context.Guild.GetChannel(channel.Id)} is not a text channel.");
            }

            //Get Guild Config
            GlobalData.GuildConfigs.TryGetValue(context.Guild.Id, out GuildConfig config);

            //Get Whitelisted Channel
            List<ulong> whitelistedChannels = config.whitelistedChannels;

            switch (arg)
            {
                #region Add Channel to Whitelist
                case "add":
                    //Limits the whitelisted channels to 5
                    int limit = 5;
                    if (whitelistedChannels.Count > limit) 
                        return await EmbedHandler.CreateErrorEmbed("Configuration Error.", $"You have reached the maximum of {limit} whitelisted channels.");

                    //Check If the channel is already whitelisted
                    foreach (ulong item in whitelistedChannels)
                        if (item == channel.Id)
                            return await EmbedHandler.CreateErrorEmbed("Configuration Error.", $"{context.Guild.GetChannel(item)} is already whitelisted!");

                    //Add Channel to Whitelist
                    whitelistedChannels.Add(channel.Id);

                    //Update Config
                    _mySQL.UpdateGuildConfig(context.Guild, "whitelistedChannels", $"{string.Join(';', whitelistedChannels)}");

                    return await EmbedHandler.CreateBasicEmbed("Configuration Changed.", $"{context.Guild.GetChannel(channel.Id)} was whitelisted.");
                #endregion

                #region Remove Channel from Whitelist
                case "remove":
                    if (whitelistedChannels.Count == 1)
                        return await EmbedHandler.CreateErrorEmbed("Configuration Error.", $"You can't have less than 1 whitelisted channel.");

                    bool notFound = true;
                    foreach (ulong item in whitelistedChannels)
                    {
                        if (item == channel.Id)
                        {
                            notFound = false;
                            break;
                        }
                    }

                    //If the Channel is not Whitelisted
                    if (notFound)
                        return await EmbedHandler.CreateErrorEmbed("Configuration Error.", $"{context.Guild.GetChannel(channel.Id)} is not whitelisted.");

                    //Remove Channel from Whitelist
                    whitelistedChannels.Remove(channel.Id);

                    //Update Config
                    _mySQL.UpdateGuildConfig(context.Guild, "whitelistedChannels", $"{string.Join(';', whitelistedChannels)}");

                    return await EmbedHandler.CreateBasicEmbed("Configuration Changed.", $"{context.Guild.GetChannel(channel.Id)} was removed from the whitelist.");
                #endregion

                #region List Whitelisted Channels
                case "list":
                    StringBuilder builder = new StringBuilder();
                    builder.Append("**Whitelisted channels:**\n");
                    for (int i = 0; i < whitelistedChannels.Count; i++)
                    {
                        var whitelistedChannel = context.Guild.GetChannel(whitelistedChannels[i]);
                        builder.Append($"{i + 1}. {whitelistedChannel.Name} (ID: {whitelistedChannel.Id})\n");
                    }
                    return await EmbedHandler.CreateBasicEmbed("Whitelist, List", $"{builder}");
                #endregion

                default:
                    return await EmbedHandler.CreateErrorEmbed("Configuration Error.", $"{arg} is not a valid argument.");
            }
        }
    }
}
