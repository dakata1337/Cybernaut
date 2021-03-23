using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord_Bot.Handlers;
using Microsoft.Extensions.DependencyInjection;
using MySql.Data.MySqlClient;

namespace Discord_Bot.Modules
{
    public class Commands : ModuleBase<SocketCommandContext>
    {
        private CommandService _commandService;
        private MySqlConnection _connection;
        private MySQL _mySQL;
        public Commands(IServiceProvider serviceProvider)
        {
            _commandService = serviceProvider.GetRequiredService<CommandService>();
            _mySQL = serviceProvider.GetRequiredService<MySQL>();
            _connection = _mySQL.connection;
        }


        [Command("Help")]
        [Summary("Shows all commands.")]
        public async Task Help()
        {
            EmbedBuilder embedBuilder = new EmbedBuilder();
            foreach (CommandInfo command in _commandService.Commands.ToList())
            {
                if (command.Name.ToLower() == "help")
                    continue;

                var guildConfig = GuildConfigFunctions.GetGuildConfig(Context.Guild, _connection);
                StringBuilder embedFieldText = new StringBuilder();
                embedFieldText.Append(command.Summary ?? "No description available\n");
                embedFieldText.Append(" Usage: " + Environment.NewLine);

                var commandInfo = command.Parameters;
                embedFieldText.Append($"```{guildConfig.prefix}{command.Name.ToLower()} {(commandInfo.Count > 0 ? string.Join(' ', commandInfo) : string.Empty)}```");

                embedBuilder.AddField(command.Name, embedFieldText);
            }

            await ReplyAsync(
                message: "Here's a list of commands and their description: ", 
                embed: embedBuilder.Build()
            );
        }

        [Command("Spin")]
        [Summary("Replies with an image.")]
        public async Task Spin() =>
            await ReplyAsync(embed: new EmbedBuilder().WithImageUrl("https://media2.giphy.com/media/DrwExaEgwjDAMldPcU/giphy.gif").WithTitle("Spin!").WithCurrentTimestamp().Build());

        [Command("Prefix"), RequireUserPermission(GuildPermission.Administrator)]
        [Summary("Changes Guild prefix.")]
        public async Task Prefix(string prefix) =>
            await ReplyAsync(embed: await GuildConfigHandler.ChangePrefix(Context, prefix));

        [Command("Whitelist"), RequireUserPermission(GuildPermission.Administrator)]
        [Summary("Adds TextChannels to Whitelist.")]
        public async Task Whitelist(string argument, IChannel channel = null) =>
            await ReplyAsync(embed: await GuildConfigHandler.WhiteList(Context, argument, channel));
    }
}
