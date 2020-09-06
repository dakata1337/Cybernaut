using Cybernaut.DataStructs;
using Discord;
using Discord.Commands;
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
        public static async Task<Embed> SendImage(string link, string title)
        {
            var embed = await Task.Run(() => new EmbedBuilder()
                .WithTitle(title)
                .WithImageUrl(link)
                .WithCurrentTimestamp().Build());
            return embed;
        }

        public static async Task<Embed> DisplayInfoAsync(SocketCommandContext context, Color color)
        {
            string configFile = $@"{GlobalData.Config.ConfigLocation}\{context.Guild.Id}.json";

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
            return embed.Build();
        }

        private static string GetPrefix(ICommandContext context, string configFile)
        {
            var json = string.Empty;

            json = File.ReadAllText(configFile);
            dynamic stuff = JsonConvert.DeserializeObject(json);
            return stuff.Prefix;
        }
    }
}
