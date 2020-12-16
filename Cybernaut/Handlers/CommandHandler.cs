using Cybernaut.DataStructs;
using Cybernaut.Services;
using Discord;
using Discord.Commands;
using Discord.Net;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
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

        private async Task<Task> HandleCommandAsync(SocketMessage socketMessage)
        {
            if (socketMessage.Content.Length == 0)
                return Task.CompletedTask;

            var argPos = 0;
            var context = new SocketCommandContext(_client, socketMessage as SocketUserMessage);

            string configFile = GetService.GetConfigLocation(context.Guild);
            var jObj = GetService.GetJObject(context.Guild);


            #region Private Messages & CAPTCHA
            if (socketMessage.Channel is IDMChannel dMChannel)
            {
                if (socketMessage.Author.IsBot)
                    return Task.CompletedTask;

                try
                { 
                    string configLocation = GetService.GetConfigLocation(context.Guild);
                    var user = await dMChannel.GetUserAsync(socketMessage.Author.Id);

                    JObject[] ogArray = jObj["usersCAPTCHA"].ToObject<JObject[]>();
                    List<JObject> newList = new List<JObject>();

                    #region CPATCHA Checks
                    //Checks if the captcha is for the right user & if the captcha is correct
                    if (!(ogArray is null))
                    {
                        newList = new List<JObject>(ogArray);
                        foreach (JObject item in ogArray)
                        {
                            if (!(item is null))
                            {
                                var userCAPTCHA = item.ToObject<CAPTCHAs>();
                                if (userCAPTCHA.userID != user.Id)
                                    continue;

                                #region CPATCHA Answer Check
                                if (userCAPTCHA.captchaAnswer == socketMessage.Content)
                                {
                                    IRole role = context.Guild.GetRole((ulong)jObj["RoleOnJoin"]);

                                    if (role is null)
                                    {
                                        break;
                                    }

                                    newList.Remove(item);
                                    jObj["usersCAPTCHA"] = JToken.FromObject(newList.ToArray());

                                    var guildUser = context.Guild.GetUser(user.Id);
                                    await guildUser.AddRoleAsync(role);

                                    File.WriteAllText(configLocation,
                                        JsonConvert.SerializeObject(jObj, Formatting.Indented));
                                    await user.SendMessageAsync(embed:
                                        await EmbedHandler.CreateBasicEmbed("I am glad you came!",
                                        $"You entered the correct CAPTCHA.\n" +
                                        $"**Welcome to {context.Guild.Name}.**",
                                        Color.Blue));
                                    File.Delete(@$"captchas/{context.Guild.Id}-{userCAPTCHA.userID}.png");
                                    break;
                                }
                                else
                                {
                                    await user.SendMessageAsync(embed:
                                        await EmbedHandler.CreateBasicEmbed("Oh.. sorry.", "You didn't solve the captcha correctly.\nI hope I see you again.", Discord.Color.Red));
                                    var guildUser = context.Guild.GetUser(user.Id);
                                    await guildUser.KickAsync();
                                }
                                #endregion
                            }
                        }
                    }
                    #endregion

                    return Task.CompletedTask;
                }
                catch
                {
                    return Task.CompletedTask;
                }
            }
            #endregion

            #region Checks message origins
            if (!(socketMessage is SocketUserMessage message) || message.Author.IsBot || message.Author.IsWebhook || message.Channel is IPrivateChannel)
                return Task.CompletedTask;
            #endregion

            #region Is user muted
            bool willRun = true;
            string json = string.Empty;

            try
            {
                json = File.ReadAllText(configFile);
            }
            catch (FileNotFoundException)
            {
                willRun = false;
            }

            if (willRun)
            {
                JObject[] ogArray = jObj["mutedUsers"].ToObject<JObject[]>();

                //Checks if the user is muted
                if (!(ogArray is null))
                {
                    List<JObject> newList = new List<JObject>(ogArray);
                    foreach (JObject item in ogArray)
                    {
                        if (!(item is null))
                        {
                            var userInfo = item.ToObject<UserInfo>();
                            if (userInfo.Id == socketMessage.Author.Id)
                            {
                                var user = socketMessage.Author;
                                await user.GetOrCreateDMChannelAsync();

                                StringBuilder builder = new StringBuilder();
                                var x = userInfo.ExpiresOn.Subtract(DateTime.Now);
                                builder.Append($"{x.Days}d ");
                                builder.Append($"{x.Hours}h ");
                                if (x.Minutes <= 1)
                                    builder.Append($"{x.Minutes}m {x.Seconds}s");
                                else
                                    builder.Append($"{x.Minutes}m ");

                                try
                                {
                                    await user.SendMessageAsync($"You are muted from {context.Guild.Name}. Your mute will end in {builder}");
                                }
                                catch (HttpException) { }

                                await socketMessage.DeleteAsync();
                                return Task.CompletedTask;
                            }
                        }
                    }
                }
            }
            #endregion

            #region Prefix Checks
            string serverPrefix = GetService.GetPrefix(context.Guild);                                  //If the config file exists we use the prefix from there

            if (!message.HasStringPrefix(serverPrefix, ref argPos))
                return Task.CompletedTask;

            if (message.Content == serverPrefix)                                                        //Checks if the message contains only the prefix
                return Task.CompletedTask;

            if (message.Content[serverPrefix.Length].ToString() == " " && message.Content.Length > 1)   //Checks if the char after the prefix is space 
                return Task.CompletedTask;                                                              //(used cuz ">" is used for formating in discord)
            #endregion

            #region Whitelisted Channel Check
            ulong whitelistedChannel = new ulong();

            ulong[] channels = jObj["whitelistedChannels"].ToObject<ulong[]>();
            var whitelistedChannelCheck = from a in channels
                                          where a == context.Channel.Id
                                          select a;
            whitelistedChannel = whitelistedChannelCheck.FirstOrDefault();
            #endregion

            #region Last Whitelist & Config Check
            if (whitelistedChannel == context.Channel.Id)
                return _commands.ExecuteAsync(context, argPos, _services, MultiMatchHandling.Best);
            #endregion

            return Task.CompletedTask;
        }

        public async Task CommandExecutedAsync(Optional<CommandInfo> command, ICommandContext context, IResult result)
        {
            if (result.IsSuccess)
                return;

            if (!command.IsSpecified || !result.IsSuccess)
                await ErrorHandler.ExecutionErrorHandler(result, context);
                return;
        }

        private async Task<Task> LogAsync(LogMessage log)
        {
            #region Code
            switch (log.Severity)
            {
                case LogSeverity.Critical:
                    await LoggingService.LogCriticalAsync(log.Source, log.Message);
                    break;
                case LogSeverity.Info:
                    await LoggingService.LogInformationAsync(log.Source, log.Message);
                    break;
            }
            return Task.CompletedTask;
            #endregion
        }
    }
}
