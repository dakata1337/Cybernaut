using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Discord_Bot.Handlers
{
    public static class EmbedHandler
    {
        /// <summary>
        /// Creates Basic Discord Embed. 
        /// </summary>
        public static async Task<Embed> CreateBasicEmbed(string title, string description, string? imageUrl = null, Color? color = null, DateTime? timestamp = null)
        {
            // Here you can specifie your default embed color
            Color defaultColor = Color.DarkTeal;

            var embed = new EmbedBuilder()
            {
                Title = title,
                Description = description,
                Color = color == null ? defaultColor : (Color)color,
                Timestamp = timestamp == null ? DateTime.Now : timestamp,
                ImageUrl = imageUrl
            };
            return embed.Build();
        }

        /// <summary>
        /// Creates Error Discord Embed. 
        /// </summary>
        public static async Task<Embed> CreateErrorEmbed(string title, string description, string? imageUrl = null)
        {
            // Here you can specifie your Default Error Embed color
            Color defaultColor = Color.Red;

            var embed = new EmbedBuilder()
            {
                Title = title,
                Description = description,
                Color = defaultColor,
                Timestamp = DateTime.Now,
                ImageUrl = imageUrl
            };
            return embed.Build();
        }

        /// <summary>
        /// Creates Advanced Discord Embed. 
        /// </summary>
        public static async Task<Embed> CreateCustomEmbed(SocketGuild guild, string embedTitle, List<EmbedFieldBuilder> fields, Color color)
        {
            var embed = await Task.Run(() => new EmbedBuilder
            {
                Timestamp = DateTime.UtcNow,
                Title = embedTitle,
                Fields = fields,
                Color = color
            });
            return embed.Build();
        }

        /// <summary>
        /// Creates Advanced Discord Embed. 
        /// </summary>
        public static async Task<Embed> CreateCustomEmbed(SocketGuild guild, string embedTitle, List<EmbedFieldBuilder> fields, string footer, Color color)
        {
            var embed = await Task.Run(() => new EmbedBuilder
            {
                Timestamp = DateTime.UtcNow,
                Title = embedTitle,
                Fields = fields,
                Footer = new EmbedFooterBuilder { Text = $"{footer}", IconUrl = guild.CurrentUser.GetAvatarUrl() },
                Color = color,
            });
            return embed.Build();
        }
    }
}
