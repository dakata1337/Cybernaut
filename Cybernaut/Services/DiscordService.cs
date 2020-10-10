﻿using Cybernaut.DataStructs;
using Cybernaut.Handlers;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;
using System.IO;
using System.Threading;
using Victoria;
using Newtonsoft.Json;
using Cybernaut.Modules;

namespace Cybernaut.Services
{
    public class DiscordService
    {
        private readonly DiscordSocketClient _client;
        private readonly CommandHandler _commandHandler;
        private readonly ServiceProvider _services;
        private readonly GlobalData _globalData;
        private readonly GuildData _guildData;
        private readonly AutoMessagingService _autoMessagingService;
        private readonly LavaNode _lavaNode;
        private readonly LavaLinkService _audioService;
        GetService getService = new GetService();

        public DiscordService()
        {
            #region Services
            _services = ConfigureServices();
            _client = _services.GetRequiredService<DiscordSocketClient>();
            _commandHandler = _services.GetRequiredService<CommandHandler>();
            _globalData = _services.GetRequiredService<GlobalData>();
            _guildData = _services.GetRequiredService<GuildData>();
            _autoMessagingService = _services.GetRequiredService<AutoMessagingService>();
            _lavaNode = _services.GetRequiredService<LavaNode>();
            _audioService = _services.GetRequiredService<LavaLinkService>();
            #endregion

            SubscribeLavaLinkEvents();
            SubscribeDiscordEvents();
        }

        public async Task InitializeAsync()
        {
            await InitializeGlobalDataAsync();

            await ClientConnect();

            await _commandHandler.InitializeAsync();

            await Task.Delay(-1);
        }

        private async Task InitializeGlobalDataAsync()
        {
            await LoggingService.InitializeAsync(); //Initializes the logging thread
            await _globalData.InitializeAsync(); //Initializes GlobalData
            await _guildData.InitializeAsync(); //Initializes GuildData
        }

        private void SubscribeDiscordEvents()
        {
            _client.Ready += ReadyAsync;
            _client.Log += LogAsync;

            _client.JoinedGuild += _autoMessagingService.OnGuildJoin;
            _client.LeftGuild += DeleteConfig;
            _client.UserJoined += _autoMessagingService.OnUserJoin;
            _client.LatencyUpdated += LatencyUpdate;
            _client.GuildAvailable += GuildAvailable;
        }

        private void SubscribeLavaLinkEvents()
        {
            #if DEBUG
            _lavaNode.OnLog += LogAsync;
            #endif
            _lavaNode.OnTrackEnded += _audioService.TrackEnded;
        }

        private async Task<Task> ClientConnect()
        {
            await _client.LoginAsync(TokenType.Bot, GlobalData.Config.DiscordToken);
            await _client.StartAsync();

            #region Guild Update
            Thread guildsUpdate = new Thread(new ThreadStart(GuildsUpdate));
            guildsUpdate.Start();
            #endregion

            return Task.CompletedTask;
        }

        private async Task ReadyAsync()
        {
            try
            {
                await _lavaNode.ConnectAsync();
                await _client.SetGameAsync(GlobalData.Config.GameStatus);
            }
            catch (Exception ex)
            {
                await LoggingService.LogCriticalAsync(ex.Source, ex.Message, ex);
            }
        }

        private ServiceProvider ConfigureServices()
        {
            return new ServiceCollection()
                .AddSingleton<DiscordSocketClient>()
                .AddSingleton<CommandService>()
                .AddSingleton<CommandHandler>()
                .AddSingleton<GlobalData>()
                .AddSingleton<GuildData>()
                .AddSingleton<AutoMessagingService>()
                .AddSingleton<LavaNode>()
                .AddSingleton(new LavaConfig())
                .AddSingleton<LavaLinkService>()
                .BuildServiceProvider();
        }

        #region Custom whatever
        private async Task<Task> GuildAvailable(SocketGuild arg)
        {
            await LoggingService.LogInformationAsync("Guild", $"Connected to {arg.Name}");
            return Task.CompletedTask;
        }

        private void GuildsUpdate()
        {
            while (true)
            {
                GlobalData.JoinedGuilds = _client.Guilds.Count;
                Thread.Sleep(5000);
            }
        }

        private Task DeleteConfig(SocketGuild guild)
        {
            #region Code
            string configFile = getService.GetConfigLocation(guild);
            if (File.Exists(configFile))
            {
                File.Delete(configFile);
            }
            return Task.CompletedTask;
            #endregion
        }

        private Task LatencyUpdate(int arg1, int arg2) 
        { 
            LoggingService.LogTitle($"Current ping: {arg2}ms");
            return Task.CompletedTask;
        }
        #endregion

        #region Logging
        private async Task LogAsync(LogMessage logMessage)
        {
            await LoggingService.Log(logMessage.Source, logMessage.Severity, logMessage.Message, logMessage.Exception);
        }
        #endregion
    }
}
