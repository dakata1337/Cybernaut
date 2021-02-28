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
using Victoria;
using Interactivity;
using Cybernaut.Modules;

namespace Discord_Bot
{
    public class DiscordService
    {
        private DiscordSocketClient _client;
        private ServiceProvider _services;
        private CommandHandler _commandHandler;
        private LavaNode _lavaNode;
        private GuildConfigHandler _guildConfigHandler;
        private Music _music;
        private Crypto _crypto;
        public DiscordService()
        {
            //Initialize Logger
            LoggingService.Initialize();

            //Initialize Config
            GlobalData.Initialize();

            //Initialize Services
            InitializeServices();

            //Subscribe Discord events
            SubscribeDiscordEvents();

            //Subscribe LavaLink events
            SubscribeLavaLinkEvents();
        }

        private void InitializeServices()
        {
            _services = ConfigureServices();
            _client = _services.GetRequiredService<DiscordSocketClient>();
            _commandHandler = _services.GetRequiredService<CommandHandler>();
            _lavaNode = _services.GetRequiredService<LavaNode>();
            _music = _services.GetRequiredService<Music>();
            _guildConfigHandler = _services.GetRequiredService<GuildConfigHandler>();
            _crypto = _services.GetRequiredService<Crypto>();
        }

        private void SubscribeDiscordEvents()
        {
            _client.Log += Client_Log;
            _client.Ready += OnClientReady;
            _client.JoinedGuild += _guildConfigHandler.JoinedGuild;
            _client.LeftGuild += _guildConfigHandler.LeftGuild;
        }

        private void SubscribeLavaLinkEvents()
        {
            _lavaNode.OnLog += Victoria_Log;
            _lavaNode.OnTrackEnded += _music.TrackEnded;
        }

        public async Task InitializeAsync()
        {
            //Initialize Command Handler
            await _commandHandler.InitializeAsync();

            //Initialize Crypto
            _crypto.Initialize();

            //Connect Discord Client
            await ClientConnect();

            await Task.Delay(-1);
        }

        private async Task ClientConnect()
        {
            await _client.LoginAsync(TokenType.Bot, GlobalData.Config.token);
            await _client.StartAsync();
        }

        private async Task OnClientReady()
        {
            await _client.SetGameAsync(GlobalData.Config.gameStatus);
            LoggingService.Log("Gateway", $"Logged in as: {_client.CurrentUser.Username}");
            await _lavaNode.ConnectAsync();

            await Task.CompletedTask;
        }

        private async Task Client_Log(LogMessage arg)
        {
            LoggingService.Log(arg.Source, $"{arg.Message}", arg.Severity);
            await Task.CompletedTask;
        }

        private async Task Victoria_Log(LogMessage logMessage)
        {
            await LoggingService.LogVictoriaAsync(logMessage.Source, logMessage.Severity, logMessage.Message, logMessage.Exception);
        }

        private ServiceProvider ConfigureServices()
        {
            return new ServiceCollection()
                .AddSingleton<DiscordSocketClient>()
                .AddSingleton<CommandService>()
                .AddSingleton<CommandHandler>()
                .AddSingleton<Commands>()
                .AddSingleton<LavaNode>()
                .AddSingleton(new LavaConfig())
                .AddSingleton<MySQL>()
                .AddSingleton<GuildConfigHandler>()
                .AddSingleton<Music>()
                .AddSingleton<HelpModule>()
                .AddSingleton<Crypto>()
                .AddSingleton<InteractivityService>()
                .BuildServiceProvider();
        }
    }
}
