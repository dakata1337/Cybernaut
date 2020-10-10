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
    }
}
