using Cybernaut.DataStructs;
using Cybernaut.Services;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Cybernaut.Handlers
{
    public static class EmbedHandler
    {
        static GetService getService = new GetService();

        public static async Task<Embed> CreateBasicEmbed(string title, string description, Color color)
        {
            var embed = await Task.Run(() => (new EmbedBuilder()
                .WithTitle(title)
                .WithDescription(description)
                .WithColor(color)
                .WithCurrentTimestamp().Build()));
            return embed;
        }

        public static async Task<Embed> CreateErrorEmbed(string title, string description)
        {
            var embed = await Task.Run(() => (new EmbedBuilder()
                .WithTitle(title)
                .WithDescription(description)
                .WithColor(Color.Orange)
                .WithCurrentTimestamp().Build()));
            return embed;
        }
        public static async Task<Embed> CreateImageEmbed(string link, string title)
        {
            var embed = await Task.Run(() => new EmbedBuilder()
                .WithTitle(title)
                .WithImageUrl(link)
                .WithCurrentTimestamp().Build());
            return embed;
        }

        public static async Task<Embed> DisplayInfoAsync(SocketCommandContext context, Color color)
        {
            #region Code
            string configFile = getService.GetConfigLocation(context.Guild);

            #region Custom Fields

            var fields = new List<EmbedFieldBuilder>();
            fields.Add(new EmbedFieldBuilder
            {
                Name = "Bot Info",
                Value = $"I am currently in {GlobalData.JoinedGuilds} Discord servers.",
                IsInline = false
            });
            fields.Add(new EmbedFieldBuilder
            {
                Name = "Server Info",
                Value = $"Overall Users: {context.Guild.Users.Count}\nCurrent People: {context.Guild.Users.Count(x => !x.IsBot)} | Current Bots: {context.Guild.Users.Count(x => x.IsBot)}\n" +
                $"Text Channels: {context.Guild.TextChannels.Count} | Voice Channels: {context.Guild.VoiceChannels.Count}",
                IsInline = false
            });
            fields.Add(new EmbedFieldBuilder
            {
                Name = "Experiencing problems?",
                Value = $"If you experience any problems report them to **{GlobalData.Config.BotOwner}**.",
                IsInline = false
            });

            var embed = await Task.Run(() => new EmbedBuilder
            {
                Title = $"Info",
                ThumbnailUrl = context.Guild.IconUrl,
                Timestamp = DateTime.UtcNow,
                Color = Color.DarkOrange,
                Footer = new EmbedFooterBuilder { Text = $"Powered By {context.Client.CurrentUser.Username}", IconUrl = context.Client.CurrentUser.GetAvatarUrl() },
                Fields = fields
            });
            embed.Color = color;

            #endregion

            return embed.Build();
            #endregion 
        }

        public static async Task<Embed> CreateCustomEmbed(SocketGuild guild, Color color, List<EmbedFieldBuilder> fields, string embedTitle, bool showAppreciation)
        {
            #region Code
            var embed = await Task.Run(() => new EmbedBuilder
            {
                Title = embedTitle,
                Timestamp = DateTime.UtcNow,
                Color = Color.DarkOrange,
                Fields = fields
            });

            if (showAppreciation)
            {
                embed.ThumbnailUrl = guild.IconUrl;
                embed.Footer = new EmbedFooterBuilder { Text = $"Thank you for choosing {guild.CurrentUser.Username}", IconUrl = guild.CurrentUser.GetAvatarUrl() };
            }

            embed.Color = color;
            return embed.Build();
            #endregion
        }

        private static string GetPrefix(ICommandContext context, string configFile)
        {
            #region Code
            var json = File.ReadAllText(configFile);
            dynamic stuff = JsonConvert.DeserializeObject(json);
            return stuff.Prefix;
            #endregion
        }
    }
}
