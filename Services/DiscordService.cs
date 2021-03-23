using System;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Discord_Bot.Modules;
using Discord_Bot.DataStrucs;
using Discord_Bot.Services;
using Discord_Bot.Handlers;
using Discord.Commands;

namespace Discord_Bot
{
    public class DiscordService
    {
        private DiscordSocketClient _client;
        private ServiceProvider _services;
        private CommandHandler _commandHandler;
        private MySQL _mySQL;
        private GuildConfigHandler _guildConfigHandler;
        public DiscordService()
        {
            //Initialize Logger
            LoggingService.Initialize();

            //Initialize Config
            GlobalData.Initialize();

            //Initialize Services
            InitializeServices();

            //Subscribe to Discord Events
            SubscribeDiscordEvents();
        }

        private void InitializeServices()
        {
            _services = ConfigureServices();
            _client = _services.GetRequiredService<DiscordSocketClient>();
            _commandHandler = _services.GetRequiredService<CommandHandler>();
            _mySQL = _services.GetRequiredService<MySQL>();
            _guildConfigHandler = _services.GetRequiredService<GuildConfigHandler>();
        }

        private void SubscribeDiscordEvents()
        {
            _client.Log += Client_Log;
            _client.Ready += OnClientReady;
            _client.JoinedGuild += _guildConfigHandler.JoinedGuild;
            _client.LeftGuild += _guildConfigHandler.LeftGuild;
        }

        public async Task InitializeAsync()
        {
            //Initialize Command Handler
            await _commandHandler.InitializeAsync();

            //Connect Discord Client
            await ClientConnect();

            //Halt Startup Thread
            await Task.Delay(-1);
        }

        private async Task ClientConnect()
        {
            //Login Bot with Token
            await _client.LoginAsync(TokenType.Bot, GlobalData.Config.token);

            //Start connection between Discord and Bot
            await _client.StartAsync();
        }

        //Change Currently Playing Game 
        private async Task OnClientReady()
            => await _client.SetGameAsync(GlobalData.Config.gameStatus);

        //Log Discord.Net messages
        private async Task Client_Log(LogMessage arg)
            => await LoggingService.Log(arg.Source, $"{arg.Message}", arg.Severity);
        

        private ServiceProvider ConfigureServices()
        {
            return new ServiceCollection()
                .AddSingleton<DiscordSocketClient>()
                .AddSingleton<CommandService>()
                .AddSingleton<CommandHandler>()
                .AddSingleton<Commands>()
                .AddSingleton<MySQL>()
                .AddSingleton<GuildConfigHandler>()
                .BuildServiceProvider();
        }
    }
}
