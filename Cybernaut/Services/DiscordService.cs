using Cybernaut.DataStructs;
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
using System.Reflection;
using Victoria.EventArgs;
using System.IO.Compression;

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
        private readonly UserManagementService _userManagementService;

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
            _userManagementService = _services.GetRequiredService<UserManagementService>();
            #endregion

            SubscribeDiscordEvents();
            SubscribeLavaLinkEvents();
        }

        private void SubscribeDiscordEvents()
        {
            _client.Ready += ReadyAsync;
            _client.Log += LogAsync;

            _client.JoinedGuild += _autoMessagingService.OnGuildJoin;
            _client.LeftGuild += DeleteConfig;
            _client.UserJoined += _autoMessagingService.OnUserJoin;
            _client.UserLeft += _autoMessagingService.OnUserLeft;
            _client.GuildAvailable += GuildAvailable;
        }

        private void SubscribeLavaLinkEvents()
        {
            _lavaNode.OnLog += LogVictoriaAsync;
            _lavaNode.OnTrackEnded += _audioService.TrackEnded;
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
            await LoggingService.Initialize(); //Initializes the logging thread
            await _globalData.InitializeAsync(); //Initializes GlobalData
            await _guildData.InitializeAsync(); //Initializes GuildData
        }

        private async Task<Task> ClientConnect()
        {
            await _client.LoginAsync(TokenType.Bot, GlobalData.Config.DiscordToken);
            await _client.StartAsync();

            #region GuildCount Update
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
                await _userManagementService.InitializeAsync(); //Initilizing UMS
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
                .AddSingleton<UserManagementService>()
                .BuildServiceProvider();
        }

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
            string configFile = GetService.GetConfigLocation(guild).ToString();
            if (File.Exists(configFile))
            {
                File.Delete(configFile);
            }
            return Task.CompletedTask;
            #endregion
        }

        #region Logging
        private async Task LogAsync(LogMessage logMessage)
        {
            await LoggingService.Log(logMessage.Source, logMessage.Severity, logMessage.Message, logMessage.Exception);
        }
        private async Task LogVictoriaAsync(LogMessage logMessage)
        {
            await LoggingService.LogVictoriaAsync(logMessage.Source, logMessage.Severity, logMessage.Message, logMessage.Exception);
        }
        #endregion
    }
}
