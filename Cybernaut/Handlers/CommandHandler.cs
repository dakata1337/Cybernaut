using Cybernaut.DataStructs;
using Cybernaut.Services;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Cybernaut.Handlers
{
    class CommandHandler
    {
        private readonly DiscordSocketClient _client;
        private readonly CommandService _commands;
        private readonly IServiceProvider _services;

        public CommandHandler(IServiceProvider services)
        {
            _commands = services.GetRequiredService<CommandService>();
            _client = services.GetRequiredService<DiscordSocketClient>();
            _services = services;

            HookEvents();
        }

        public async Task InitializeAsync()
        {
            await _commands.AddModulesAsync(
                assembly: Assembly.GetEntryAssembly(),
                services: _services);
        }

        public void HookEvents()
        {
            _commands.CommandExecuted += CommandExecutedAsync;
            _commands.Log += LogAsync;
            _client.MessageReceived += HandleCommandAsync;
        }

        private Task HandleCommandAsync(SocketMessage socketMessage)
        {
            var argPos = 0;

            if (!(socketMessage is SocketUserMessage message) || message.Author.IsBot || message.Author.IsWebhook || message.Channel is IPrivateChannel)
                return Task.CompletedTask;

            var context = new SocketCommandContext(_client, socketMessage as SocketUserMessage);
            string configFile = $@"{GlobalData.Config.ConfigLocation}\{context.Guild.Id}.json";

            string serverPrefix = string.Empty;
            
            bool configExists = false;
            if (File.Exists(configFile))
            {
                configExists = true;
                serverPrefix = getPrefix(configFile);                                          //The config file exists so we use the prefix from there
                if (!message.HasStringPrefix(serverPrefix, ref argPos))
                    return Task.CompletedTask;
            }
            else
            {
                serverPrefix = GlobalData.Config.DefaultPrefix;                                         //The config file doesnt exist so we use the default
                if (!message.HasStringPrefix(serverPrefix, ref argPos))                                 //prefix
                    return Task.CompletedTask;

            }

            if (message.Content == serverPrefix)                                                        //checks if the message contains only the prefix
                return Task.CompletedTask;

            if (message.Content[serverPrefix.Length].ToString() == " " && message.Content.Length > 1)   //Checks if the char after the prefix is space 
                return Task.CompletedTask;                                                              //(used cuz ">" is used for formating in discord)


            ulong whitelistedChannel = new ulong();
            if (configExists)
            {
                dynamic jsonConfig = JsonConvert.DeserializeObject(File.ReadAllText(configFile));

                ulong[] channels = jsonConfig.whitelistedChannels.ToObject<ulong[]>();
                var whitelistedChannelCheck = from a in channels
                                              where a == context.Channel.Id
                                              select a;
                whitelistedChannel = whitelistedChannelCheck.FirstOrDefault();
            }

            
            if (whitelistedChannel != context.Channel.Id && configExists)
            {
                return Task.CompletedTask;
            }
            else
            {
                var result = _commands.ExecuteAsync(context, argPos, _services, MultiMatchHandling.Best);
                if (!result.Result.IsSuccess && !result.Result.ErrorReason.Contains("Could not find file")) //just.. don't
                    LoggingService.LogCriticalAsync("CommandError", $"{message.Author}: {message} => {result.Result.ErrorReason}  ({context.Guild.Id} | {context.Guild.Name})");

                return result;
            }
        }

        public async Task CommandExecutedAsync(Optional<CommandInfo> command, ICommandContext context, IResult result)
        {
            if (result.IsSuccess)
                return;

            if (!command.IsSpecified || !result.IsSuccess)
                await DisplayErrors(result, context);
                return;
        }

        private async Task DisplayErrors(IResult result, ICommandContext context)
        {
            string msg = string.Empty;
            string title = "Command Error";
            Color color = Color.Blue;
            switch (result.Error)
            {
                case CommandError.BadArgCount:
                    msg = "This command is not supposed to be used like this.";
                    break;
                case CommandError.UnmetPrecondition:
                    break;
                case CommandError.ObjectNotFound:
                    msg = "The specified object was not found.";
                    break;
                case CommandError.UnknownCommand:
                    msg = "Unknown command.";
                    break;
                default:
                    if (result.ErrorReason.Contains("Could not find file"))
                    {
                        msg = $"Please type `{GlobalData.Config.DefaultPrefix}prefix YourPrefixHere` to configure the bot."; color = Color.Orange; title = "Configuration needed!";
                    }
                    else
                    {
                        msg = "Internal Bot error.";
                        color = Color.Orange;
                    }
                    break;
            }

            if(msg != null)
            {
                await context.Channel.SendMessageAsync(embed: await EmbedHandler.CreateBasicEmbed(title, msg, color));
            }
        }

        private async Task<Task> LogAsync(LogMessage log)
        {
            switch (log.Severity)
            {
                case LogSeverity.Critical:
                    await LoggingService.LogCriticalAsync(log.Source, log.Message);
                    break;
                case LogSeverity.Info:
                    await LoggingService.LogInformationAsync(log.Source, log.Message);
                    break;
            }
            Thread.Sleep(5);
            return Task.CompletedTask;
        }

        private string getPrefix(string configFile)
        {
            dynamic stuff = JsonConvert.DeserializeObject(File.ReadAllText(configFile));
            return stuff.Prefix;
        }
    }
}
