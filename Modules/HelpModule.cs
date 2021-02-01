using Discord;
using Discord.Commands;
using Discord_Bot.DataStrucs;
using Discord_Bot.Handlers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord.WebSocket;

namespace Discord_Bot.Modules
{
    public class HelpModule
    {
        private IServiceProvider _serviceProvider;
        public HelpModule(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public async Task<Embed> Help(SocketCommandContext context, CommandService commandService)
        {
            //Embed Builder
            EmbedBuilder embedBuilder = new EmbedBuilder().WithTitle("Here's a list of commands and their description: ").WithColor(Color.Green);

            //Get Guild Config
            GlobalData.GuildConfigs.TryGetValue(context.Guild.Id, out GuildConfig config);

            //Get Guild User
            var guildUser = context.Guild.GetUser(context.User.Id);

            //Loop thru all commands
            foreach (CommandInfo command in commandService.Commands.ToList())
            {
                //If command name is 'help' skip it
                if (command.Name.ToLower() == "help")
                    continue;

                var preconditionResult = await command.CheckPreconditionsAsync(context);

                //Check if the user can execute the command
                if (!preconditionResult.IsSuccess)
                    continue;

                var z = guildUser.GuildPermissions;

                //Add Field
                embedBuilder.AddField(command.Name, new StringBuilder()
                    .Append(command.Summary ?? "No description available\n")
                );
            }
            //Build Embed and return
            return embedBuilder.Build();
        }
        public async Task<Embed> About(SocketCommandContext context, CommandService commandService, string command)
        {
            bool found = false;
            //Get Guild Config
            GlobalData.GuildConfigs.TryGetValue(context.Guild.Id, out GuildConfig config);

            StringBuilder stringBuilder = new StringBuilder();
            foreach (CommandInfo _command in commandService.Commands.ToList())
            {
                //Command name check
                if (_command.Name.ToLower() != command.ToLower())
                    continue;

                //Specified command found
                found = true;

                //Description
                stringBuilder.Append($"{_command.Summary ?? "No description available"} ");

                //Usage
                stringBuilder.Append("Usage: " + Environment.NewLine);
                var commandInfo = _command.Parameters;
                stringBuilder.Append($"```\n{config.prefix}{_command.Name.ToLower()} {string.Join(' ', commandInfo)}\n```");

                //Arguments
                foreach (var argument in commandInfo)
                {
                    if (argument.Summary != null && argument.Summary.Length > 0)
                        stringBuilder.Append($"{argument} => {argument.Summary}");
                }
            }

            //Reply
            if (found)
                return await EmbedHandler.CreateBasicEmbed($"More info about {command}:", $"{stringBuilder}");
            else
                return await EmbedHandler.CreateErrorEmbed("Error", $"{command} isn't a valid command!");
        }
    }
}
