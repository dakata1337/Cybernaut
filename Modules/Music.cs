
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord_Bot.DataStrucs;
using Discord_Bot.Handlers;
using Interactivity;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Victoria;
using Victoria.Enums;
using Victoria.EventArgs;
using Victoria.Responses.Rest;

namespace Discord_Bot.Modules
{
    [Group("Music")]
    class Music
    {
        private readonly LavaNode _lavaNode;
        private InteractivityService _interactivityService;
        public Music(IServiceProvider serviceProvider)
        {
            _lavaNode = serviceProvider.GetRequiredService<LavaNode>();
            _interactivityService = serviceProvider.GetRequiredService<InteractivityService>();
        }

        #region JoinAsync
        /// <summary>
        /// Connects to voice channel
        /// </summary>
        /// <param name="context">Command context</param>
        /// <param name="channel">Channel to join (defaults to user channel if not specified)</param>
        /// <returns>Discord.Embed</returns>
        public async Task<Embed> JoinAsync(SocketCommandContext context, IVoiceChannel voiceChannel = null)
        {
            string embedTitle = "Music, Join";

            // If Bot is already connected
            if (_lavaNode.HasPlayer(context.Guild))
                return await EmbedHandler.CreateErrorEmbed(embedTitle, "I'm already connected to a voice channel!");

            // Get User VoiceState
            var userVState = context.User as IVoiceState;

            // If channel is not specified and user isn't in VC
            if (voiceChannel == null && userVState.VoiceChannel == null)
                return await EmbedHandler.CreateErrorEmbed(embedTitle, "You can't use this command because you aren't in a Voice Channel!");

            // Determine which channel to join
            IVoiceChannel channel = (voiceChannel == null ? userVState.VoiceChannel : voiceChannel);
            await _lavaNode.JoinAsync(channel, context.Channel as ITextChannel);

            // Return Successful Embed
            return await EmbedHandler.CreateBasicEmbed(embedTitle, $"Joined **{channel.Name}**.");
        }
        #endregion

        #region LeaveAsync
        /// <summary>
        /// Leaves voice channel
        /// </summary>
        /// <param name="context">Command context</param>
        /// <returns>Discord.Embed</returns>
        public async Task<Embed> LeaveAsync(SocketCommandContext context)
        {
            string embedTitle = "Music, Leave";

            var checkResult = MusicChecks(context, embedTitle).Result;
            if (checkResult != null)
                return checkResult;

            // Get Guild Player
            var player = _lavaNode.GetPlayer(context.Guild);

            // Stop Player if PlayerState.Playing
            if (player.PlayerState == PlayerState.Playing)
                await player.StopAsync();

            // Leave VC
            await _lavaNode.LeaveAsync(player.VoiceChannel);

            // Return Successful Embed
            return await EmbedHandler.CreateBasicEmbed(embedTitle, $"I'm sorry that I gave you up :'(.");
        }
        #endregion

        #region PlayAsync
        /// <summary>
        /// Plays specified song
        /// </summary>
        /// <param name="context">Command context</param>
        /// <param name="query">Song name/url</param>
        /// <returns>Discord.Embed</returns>
        public async Task<Embed> PlayAsync(SocketCommandContext context, string query, bool playNext = false)
        {
            string embedTitle = "Music, Play";

            var checkResult = MusicChecks(context, embedTitle).Result;
            if (checkResult != null)
                return checkResult;

            // Get Guild Player
            var player = _lavaNode.GetPlayer(context.Guild);

            // Get Guild Config
            GlobalData.GuildConfigs.TryGetValue(context.Guild.Id, out GuildConfig guildConfig);

            // Get Track
            var searchResult = SearchTrackAsync(context, query, guildConfig, embedTitle).Result;

            // Check if something went wrong
            if (searchResult.Embed != null)
                return searchResult.Embed;

            // Get Track
            LavaTrack track = searchResult.Track;

            // If playNext - add Track on top of Queue
            if (playNext)
                return await AddOnTopOfQueueAsync(context, track);

            // Play Track
            return await PlayTrackAsync(player, track, guildConfig, embedTitle);
        }
        #endregion

        #region AddOnTopOfQueueAsync
        /// <summary>
        /// Adds song on top of the queue
        /// </summary>
        /// <param name="context">Command context</param>
        /// <param name="track">The track itself</param>
        /// <returns>Discord.Embed</returns>
        public async Task<Embed> AddOnTopOfQueueAsync(SocketCommandContext context, LavaTrack track)
        {
            string embedTitle = "Music, Play";

            // Get Player
            var player = _lavaNode.GetPlayer(context.Guild);

            // Get Guild Config
            GlobalData.GuildConfigs.TryGetValue(context.Guild.Id, out GuildConfig guildConfig);

            string status = guildConfig.IsLooping ? "looping" : "playing";

            // If Player is not playing AND queue is empty
            if (player.PlayerState != PlayerState.Playing && player.Queue.Count == 0)
            {
                await player.PlayAsync(track);
                return await EmbedHandler.CreateBasicEmbed(embedTitle, $"Now {status} [{track.Title}]({track.Url})");
            }

            // Save original queue
            List<LavaTrack> originalQueue = new List<LavaTrack>(player.Queue);

            // Clear queue
            player.Queue.Clear();

            // Add track to the top of queue
            player.Queue.Enqueue(track);

            // Add all tracks from the original queue
            for (int i = 0; i < originalQueue.Count; i++)
                player.Queue.Enqueue(originalQueue[i]);

            // Return Successful Embed
            return await EmbedHandler.CreateBasicEmbed("Music, Play", $"**[{track.Title}]({track.Url})** was added on the top of the queue.");
        }
        #endregion

        #region SkipTrackAsync
        /// <summary>
        /// Skip to the next track
        /// </summary>
        /// <param name="context">Command Context</param>
        /// <returns>Discord.Embed</returns>
        public async Task<Embed> SkipTrackAsync(SocketCommandContext context)
        {
            string embedTitle = "Music, Skip";

            var checkResult = MusicChecks(context, embedTitle).Result;
            if (checkResult != null)
                return checkResult;

            // Get Guild Player
            var player = _lavaNode.GetPlayer(context.Guild);

            // If Queue is empty - call StopAsync()
            if (player.Queue.Count == 0)
                return await StopAsync(context, embedTitle);

            // Save Current Track
            var currentTrack = player.Track;

            // Skip to next Track
            await player.SkipAsync();

            // Return Successful Embed
            return await EmbedHandler.CreateBasicEmbed(embedTitle,
                $"I have successfully skiped **[{currentTrack.Title}]({currentTrack.Url})**.\n" +
                $"Now playing: **[{player.Track.Title}]({player.Track.Url})**.");
        }
        #endregion

        #region StopAsync
        /// <summary>
        /// Stop all tracks and clear queue
        /// </summary>
        /// <param name="context">Command Context</param>
        /// <param name="embedTitle"></param>
        /// <returns>Discord.Embed</returns>
        public async Task<Embed> StopAsync(SocketCommandContext context, string embedTitle = "Music, Stop")
        {
            var checkResult = MusicChecks(context, embedTitle).Result;
            if (checkResult != null)
                return checkResult;

            // Get Guild Player
            var player = _lavaNode.GetPlayer(context.Guild);

            if (player.PlayerState != PlayerState.Playing)
                return await EmbedHandler.CreateErrorEmbed(embedTitle, "The bot is currently not playing music!");

            // Stop Player
            await player.StopAsync();

            // Return Successful Embed
            return await EmbedHandler.CreateBasicEmbed("Music, Stop", "I Have stopped playback & the playlist has been cleared.");
        }
        #endregion

        #region PauseAsync
        /// <summary>
        /// Pauses track playback
        /// </summary>
        /// <param name="context">Command Context</param>
        /// <returns>Discord.Embed</returns>
        public async Task<Embed> PauseAsync(SocketCommandContext context)
        {
            string embedTitle = "Music, Pause";

            var checkResult = MusicChecks(context, embedTitle).Result;
            if (checkResult != null)
                return checkResult;

            // Get Guild Player
            var player = _lavaNode.GetPlayer(context.Guild);

            // If Player State isn't Playing
            if (player.PlayerState != PlayerState.Playing)
                return await EmbedHandler.CreateErrorEmbed(embedTitle, "There is nothing to pause!");

            // Pause Track
            await player.PauseAsync();

            // Return Successful Embed
            return await EmbedHandler.CreateBasicEmbed("Music, Pause", $"Paused: **[{player.Track.Title}]({player.Track.Url})**.");
        }
        #endregion

        #region ResumeAsync
        /// <summary>
        /// Resumes track playback
        /// </summary>
        /// <param name="context">Command Context</param>
        /// <returns>Discord.Embed</returns>
        public async Task<Embed> ResumeAsync(SocketCommandContext context)
        {
            string embedTitle = "Music, Resume";

            var checkResult = MusicChecks(context, embedTitle).Result;
            if (checkResult != null)
                return checkResult;

            // Get Guild
            var player = _lavaNode.GetPlayer(context.Guild);

            // If Player State isn't Paused
            if (player.PlayerState != PlayerState.Paused)
                return await EmbedHandler.CreateErrorEmbed(embedTitle, "Player isn't paused!");

            // Resume Track
            await player.ResumeAsync();

            // Return Successful Embed
            return await EmbedHandler.CreateBasicEmbed("Music, Resume", $"Resumed: **[{player.Track.Title}]({player.Track.Url})**.");
        }
        #endregion

        #region GetQueueAsync
        /// <summary>
        /// Display all songs in queue
        /// </summary>
        /// <param name="context">Command Context</param>
        /// <returns>Discord.Embed</returns>
        public async Task<Embed> GetQueueAsync(SocketCommandContext context, string embedTitle = "Music, Queue")
        {
            var checkResult = MusicChecks(context, embedTitle).Result;
            if (checkResult != null)
                return checkResult;

            // Get Guild
            var player = _lavaNode.GetPlayer(context.Guild);

            // If Player State isn't Playing
            if (player.PlayerState != PlayerState.Playing)
                return await EmbedHandler.CreateErrorEmbed(embedTitle, "Player doesn't seem to be playing anything right now!");

            // Get Guild Config
            GlobalData.GuildConfigs.TryGetValue(context.Guild.Id, out GuildConfig config);

            // Make String Builder
            StringBuilder builder = new StringBuilder();

            // How many ticks (Recommended: Don't go above 20 because it looks weird)
            // Example: ▬▬▬▬🔘▬▬▬▬▬▬▬▬▬▬▬▬▬▬▬
            int tickCount = 20;

            // Calculate how many seconds is 1 tick
            double tickDensity = player.Track.Position.TotalSeconds / (player.Track.Duration.TotalSeconds / tickCount);

            // Append Song title
            string status = (config.IsLooping ? "looping" : "playing");
            builder.Append($"**Now {status}: [{player.Track.Title}]({player.Track.Url})**\n");

            if (!player.Track.IsStream)
            {
                // Track Current position
                builder.Append($"{player.Track.Position.ToString(@"hh\:mm\:ss")} ");

                // Get Dot Location
                int dotLocation = (int)Math.Round(tickDensity, 0, MidpointRounding.AwayFromZero);

                for (int i = 0; i < tickCount; i++)
                {
                    if (i == dotLocation)
                        builder.Append("🔘");
                    else
                        builder.Append("▬");
                }

                // Track Duration
                builder.Append($" {player.Track.Duration}");
            }
            else
            {
                builder.Append("This is a stream!");
            }
            builder.Append(Environment.NewLine);


            var queue = player.Queue;
            // Queue is empty
            if (queue.Count == 0)
            {
                builder.Append("Nothing is queued");
            }
            //Queue isn't empty
            else
            {
                for (int trackNum = 0; trackNum < queue.Count; trackNum++)
                    builder.Append($"**{trackNum + 2}: [{queue.ElementAt(trackNum).Title}]({queue.ElementAt(trackNum).Url})**" + Environment.NewLine);
            }

            // Return Successful Embed
            return await EmbedHandler.CreateBasicEmbed(embedTitle, builder.ToString());
        }
        #endregion

        #region SetVolumeAsync
        /// <summary>
        /// Sets Bot volume
        /// </summary>
        /// <param name="context">Command context</param>
        /// <param name="volume">Music volume</param>
        /// <returns>Discord.Embed</returns>
        public async Task<Embed> SetVolumeAsync(SocketCommandContext context, int? volume = null)
        {
            string embedTitle = "Music, Volume";

            var checkResult = MusicChecks(context, embedTitle).Result;
            if (checkResult != null)
                return checkResult;

            // Get Guild Config
            GlobalData.GuildConfigs.TryGetValue(context.Guild.Id, out GuildConfig guildConfig);

            // Get Guild Player
            var player = _lavaNode.GetPlayer(context.Guild);

            // Show Current Volume
            if (volume == null)
                return await EmbedHandler.CreateBasicEmbed(embedTitle, $"Volume is set to {guildConfig.Volume}");

            // Volume must be in the specified range
            if (volume <= 0 || volume > 150)
                return await EmbedHandler.CreateBasicEmbed("Music, Volume", $"Volume must be between 1 and 150.");

            // Update Guild Config Volume
            GuildConfigHandler.UpdateGuildConfig(context.Guild, "volume", $"{volume}");

            // Set Player Volume
            await player.UpdateVolumeAsync((ushort)volume);

            // Return Successful Embed
            return await EmbedHandler.CreateBasicEmbed("Music, Volume", $"Volume has been set to {player.Volume}.");
        }
        #endregion

        #region LoopAsync
        /// <summary>
        /// Enables track looping
        /// </summary>
        /// <param name="context">Command Context</param>
        /// <param name="arg">Custom args</param>
        /// <returns>Discord.Embed</returns>
        public async Task<Embed> LoopAsync(SocketCommandContext context, string arg)
        {
            string embedTitle = "Music, Loop";

            // Get Guild Config
            GlobalData.GuildConfigs.TryGetValue(context.Guild.Id, out GuildConfig guildConfig);

            if (arg != null && arg.ToLower().Trim() == "status")
                return await EmbedHandler.CreateBasicEmbed(embedTitle, $"Looping is {(guildConfig.IsLooping ? "enabled" : "disabled")}.");

            var checkResult = MusicChecks(context, embedTitle).Result;
            if (checkResult != null)
                return checkResult;

            // Update Looping
            GuildConfigHandler.UpdateGuildConfig(context.Guild, "isLooping", $"{(guildConfig.IsLooping ? "0" : "1")}");

            // Return Successful Embed
            return await EmbedHandler.CreateBasicEmbed("Looping Status", $"Looping is now {(guildConfig.IsLooping ? "disabled" : "enabled")}.");
        }
        #endregion

        #region ShuffleAsync
        public async Task<Embed> ShuffleAsync(SocketCommandContext context)
        {
            string embedTitle = "Music, Shuffle";

            var checkResult = MusicChecks(context, embedTitle).Result;
            if (checkResult != null)
                return checkResult;

            // Get Guild Player
            var player = _lavaNode.GetPlayer(context.Guild);

            if (player.Queue.Count <= 1)
                return await EmbedHandler.CreateBasicEmbed(embedTitle, "Not enough songs in queue!");

            // Shuffle Playlist
            player.Queue.Shuffle();

            return await GetQueueAsync(context, embedTitle);
        }
        #endregion

        #region SeekAsync
        /// <summary>
        /// Seek track
        /// </summary>
        /// <param name="context">Command Context</param>
        /// <param name="seekTo"></param>
        /// <returns>Discord.Embed</returns>
        public async Task<Embed> SeekAsync(SocketCommandContext context, string seekTo)
        {
            string embedTitle = "Music, Seek";

            var checkResult = MusicChecks(context, embedTitle).Result;
            if (checkResult != null)
                return checkResult;

            // Get Guild Player
            var player = _lavaNode.GetPlayer(context.Guild);

            // If Track is Stream
            if (player.Track.IsStream)
                return await EmbedHandler.CreateErrorEmbed(embedTitle, $"You can't seek **[{player.Track.Title}]({player.Track.Url})** because it's a stream!");

            // Get ':' count
            var count = seekTo.Count(f => f == ':');

            TimeSpan timeSpan = new TimeSpan();
            switch (count)
            {
                case 1: timeSpan = DateTime.ParseExact(seekTo, "m\\:ss", CultureInfo.InvariantCulture).TimeOfDay;
                    break;
                case 2: timeSpan = DateTime.ParseExact(seekTo, "H\\:mm\\:ss", CultureInfo.InvariantCulture).TimeOfDay;
                    break;
                default: return await EmbedHandler.CreateErrorEmbed(embedTitle, $"**Unsupported time format**");
            }

            // Get Track Duration
            var duration = player.Track.Duration;

            if (timeSpan.TotalSeconds > duration.TotalSeconds)
                return await EmbedHandler.CreateErrorEmbed(embedTitle, $"**Value mustn't be bigger than {duration.ToString(@"hh\:mm\:ss")}!**");

            // Seek to specified TimeSpan
            await player.SeekAsync(timeSpan);

            // Return Successful Embed
            return await EmbedHandler.CreateBasicEmbed("Music, Seek", $"**Position changed to: {timeSpan}!**");
        }
        #endregion

        #region PlaylistAsync

        #region CommandSelection
        public async Task<Embed> PlaylistAsync(SocketCommandContext context, string command, string playlistName, string modifier, string query)
        {
            string embedTitle = "Music, Playlist";

            var checkResult = MusicChecks(context, embedTitle).Result;
            if (checkResult != null)
                return checkResult;

            if (!Regex.IsMatch(playlistName != null ? playlistName : "null", @"^[a-zA-Z0-9_]+$"))
                return await EmbedHandler.CreateErrorEmbed(embedTitle, "Playlist name contains illegal characters!");

            command = command.ToLower();

            if (command == "load")
            {
                return await LoadPlaylistAsync(context, playlistName, embedTitle);
            }
            else if (command == "show")
            {
                return await ShowPlaylistsAsync(context, playlistName, embedTitle);
            }
            else if (command == "modify")
            {
                if (modifier.ToLower() == "add")
                    return await AddPlaylistTrack(context, playlistName, query, embedTitle);
                else if (modifier.ToLower() == "remove")
                    return await RemovePlaylistTrack(context, playlistName, query, embedTitle);
                else
                    return await EmbedHandler.CreateErrorEmbed(embedTitle, $"{modifier} isn't a valid argument!");
            }
            else if (command == "create")
            {
                return await CreatePlaylistAsync(context, playlistName, embedTitle);
            }
            else if (command == "remove")
            {
                return await RemovePlaylistAsync(context, playlistName, embedTitle);
            }
            else
            {
                return await EmbedHandler.CreateErrorEmbed(embedTitle, $"{command} is not a valid argument!");
            }
        }
        #endregion

        #region CreatePlaylistAsync
        public async Task<Embed> CreatePlaylistAsync(SocketCommandContext context, string playlistName, string embedTitle)
        {
            // Get Guild Config
            GlobalData.GuildConfigs.TryGetValue(context.Guild.Id, out GuildConfig guildConfig);

            // Get Guild Playlists
            var guildPlaylists = guildConfig.Playlists != null && guildConfig.Playlists.Count > 0 ? 
                guildConfig.Playlists.ToObject<List<Playlist>>() : new List<Playlist>();
            
            // Check if playlist with the same name already exists
            foreach (var playlist in guildPlaylists)
                if (playlist.Name.ToLower() == playlistName.ToLower())
                    return await EmbedHandler.CreateErrorEmbed(embedTitle, "A playlist with this name already exists!");

            // Create Empty playlist
            guildPlaylists.Add(new Playlist()
            {
                Name = playlistName,
                Tracks = new List<Song>()
            });

            // Update Guild Config
            GuildConfigHandler.UpdateGuildConfig(context.Guild, "playlists", JsonConvert.SerializeObject(guildPlaylists, Formatting.None));

            // Return Successful Embed
            return await EmbedHandler.CreateBasicEmbed(embedTitle, $"\"**{playlistName}**\" was created successfully.");
        }
        #endregion

        #region RemovePlaylistAsync
        public async Task<Embed> RemovePlaylistAsync(SocketCommandContext context, string playlistName, string embedTitle)
        {
            // Get Guild Config
            GlobalData.GuildConfigs.TryGetValue(context.Guild.Id, out GuildConfig guildConfig);

            // Get Guild Playlists
            var guildPlaylists = guildConfig.Playlists != null && guildConfig.Playlists.Count > 0 ?
                guildConfig.Playlists.ToObject<List<Playlist>>() : new List<Playlist>();

            // Go through each playlist
            foreach (var playlist in guildPlaylists)
            {
                // If names match
                if (playlist.Name.ToLower() == playlistName.ToLower())
                {
                    // Delete playlist
                    guildPlaylists.Remove(playlist);

                    // Update Guild Config
                    GuildConfigHandler.UpdateGuildConfig(context.Guild, "playlists", JsonConvert.SerializeObject(guildPlaylists, Formatting.None));

                    // Return Successful Embed
                    return await EmbedHandler.CreateBasicEmbed(embedTitle, $"\"**{playlistName}**\" was removed.");
                }
            }
            return await EmbedHandler.CreateErrorEmbed(embedTitle, $"\"**{playlistName}**\" wasn't found!");
        }
        #endregion

        #region ShowPlaylistsAsync
        public async Task<Embed> ShowPlaylistsAsync(SocketCommandContext context, string playlistName, string embedTitle)
        {
            // Get Guild Config
            GlobalData.GuildConfigs.TryGetValue(context.Guild.Id, out GuildConfig guildConfig);

            // Get Guild Playlists
            var guildPlaylists = guildConfig.Playlists != null && guildConfig.Playlists.Count > 0 ?
                guildConfig.Playlists.ToObject<List<Playlist>>() : new List<Playlist>();

            // Check if Guild has playlists
            if (guildPlaylists.Count == 0)
                return await EmbedHandler.CreateErrorEmbed(embedTitle, $"No playlists were found!");

            // Create new StringBuilder
            StringBuilder trackBuilder = new StringBuilder();

            // Go through each playlist
            for (int i = 0; i < guildPlaylists.Count; i++)
            {
                // Get playlist at index
                var playlist = guildPlaylists[i];

                // Check if playlist is specified
                if (playlistName != null)
                {
                    // Continue to next playlist
                    if (playlist.Name.ToLower() != playlistName.ToLower())
                        continue;

                    // If playlist is empty
                    if (playlist.Tracks.Count == 0)
                    {
                        trackBuilder.Append($"No tracks found in \"**{playlist.Name}**\"");
                        // Break to send Embed
                        break;
                    }

                    // Change Embed title 
                    embedTitle += $" - {playlist.Name}";

                    int songIndex = 0;
                    // Add Tracks to StringBuilder
                    foreach (var track in playlist.Tracks)
                    {
                        trackBuilder.Append($"**{songIndex + 1}. [{track.Name}]({track.Url})**\n");
                        songIndex++;
                    }

                    // Break to send Embed
                    break;
                }

                // Add track to string
                trackBuilder.Append($"**{i + 1}. {playlist.Name} (Tracks: {playlist.Tracks.Count})**\n");
            }

            if (trackBuilder.Length == 0)
                return await EmbedHandler.CreateErrorEmbed(embedTitle, "Playlist wasn't found!");

            // Return Successful Embed
            return await EmbedHandler.CreateBasicEmbed(embedTitle, $"{trackBuilder}");
        }
        #endregion

        #region LoadPlaylistAsync
        public async Task<Embed> LoadPlaylistAsync(SocketCommandContext context, string playlistName, string embedTitle)
        {
            // Get Guild Config
            GlobalData.GuildConfigs.TryGetValue(context.Guild.Id, out GuildConfig guildConfig);

            // Get Guild Playlists
            var guildPlaylists = guildConfig.Playlists != null && guildConfig.Playlists.Count > 0 ?
                guildConfig.Playlists.ToObject<List<Playlist>>() : new List<Playlist>();

            // Go through each playlist
            foreach (var playlist in guildPlaylists)
            {
                // Continue to next playlist
                if (playlist.Name.ToLower() != playlistName.ToLower())
                    continue;

                // If playlist is larger than 5 Tracks send embed
                // to notify user that playlist is loading
                if (playlist.Tracks.Count > 5)
                    await context.Channel.SendMessageAsync(embed: await EmbedHandler.CreateBasicEmbed(embedTitle, $"Loading \"**{playlist.Name}**\"."));

                // Go through each Track
                foreach (var track in playlist.Tracks)
                {
                    // Get Track
                    var searchResult = SearchTrackAsync(context, track.Url, guildConfig, embedTitle).Result;

                    // Check for errors
                    if (searchResult.Embed != null)
                    {
                        await context.Channel.SendMessageAsync(embed: searchResult.Embed);
                        continue;
                    }

                    // Play Track
                    await PlayTrackAsync(_lavaNode.GetPlayer(context.Guild), searchResult.Track, guildConfig, embedTitle);
                }
                // Return Successful Embed
                return await EmbedHandler.CreateBasicEmbed(embedTitle, $"Playlist \"**{playlist.Name}**\" was loaded.");
            }
            return await EmbedHandler.CreateErrorEmbed(embedTitle, $"Playlist \"**{playlistName}**\" was not found.");
        }
        #endregion

        #region AddPlaylistTrack
        public async Task<Embed> AddPlaylistTrack(SocketCommandContext context, string playlistName, string query, string embedTitle)
        {
            // Get Guild Config
            GlobalData.GuildConfigs.TryGetValue(context.Guild.Id, out GuildConfig guildConfig);

            // Get Guild Playlists
            var guildPlaylists = guildConfig.Playlists != null && guildConfig.Playlists.Count > 0 ?
                guildConfig.Playlists.ToObject<List<Playlist>>() : new List<Playlist>();

            // Go through each playlist
            foreach (var playlist in guildPlaylists)
            {
                if (playlist.Name.ToLower() != playlistName.ToLower())
                    continue;

                // Go through each Track
                foreach (var track in playlist.Tracks)
                    if (track.Name.ToLower() == query.ToLower())
                        return await EmbedHandler.CreateErrorEmbed(embedTitle, $"{query} is already in the playlist!");

                // Get Track
                var searchResult = SearchTrackAsync(context, query, guildConfig, embedTitle).Result;

                // Add Track to playlist
                playlist.Tracks.Add(new Song() { Name = searchResult.Track.Title, Url = searchResult.Track.Url });

                // Update Guild Config
                GuildConfigHandler.UpdateGuildConfig(context.Guild, "playlists", JsonConvert.SerializeObject(guildPlaylists, Formatting.None));

                // Return Successful Embed
                return await EmbedHandler.CreateBasicEmbed(embedTitle, $"**[{searchResult.Track.Title}]({searchResult.Track.Url}) was added to the playlist.**");
            }
            return await EmbedHandler.CreateErrorEmbed(embedTitle, $"**\"{playlistName}\" wasn't found.**");
        }
        #endregion

        #region RemovePlaylistTrack
        public async Task<Embed> RemovePlaylistTrack(SocketCommandContext context, string playlistName, string query, string embedTitle)
        {
            // Get Guild Config
            GlobalData.GuildConfigs.TryGetValue(context.Guild.Id, out GuildConfig guildConfig);

            // Get Guild Playlists
            var guildPlaylists = guildConfig.Playlists != null && guildConfig.Playlists.Count > 0 ?
                guildConfig.Playlists.ToObject<List<Playlist>>() : new List<Playlist>();

            // Go through each playlist
            foreach (var playlist in guildPlaylists)
            {
                if (playlist.Name.ToLower() != playlistName.ToLower())
                    continue;

                // Go through each Track
                foreach (var track in playlist.Tracks)
                {
                    if (track.Name.ToLower() != query.ToLower())
                        continue;

                    // Remove Track
                    playlist.Tracks.Remove(track);

                    // Update Guild Config
                    GuildConfigHandler.UpdateGuildConfig(context.Guild, "playlists", JsonConvert.SerializeObject(guildPlaylists, Formatting.None));

                    // Return Successful Embed
                    return await EmbedHandler.CreateBasicEmbed(embedTitle, $"**{track.Name} was removed from the playlist.**");
                }
                return await EmbedHandler.CreateErrorEmbed(embedTitle, $"**\"{query}\" wasn't found in \"{playlist.Name}\"**");
            }
            return await EmbedHandler.CreateErrorEmbed(embedTitle, $"**\"{playlistName}\" wasn't found.**");
        }

        #endregion

        #endregion

        #region SearchTrackAsync
        public async Task<TrackRequest> SearchTrackAsync(SocketCommandContext context, string query, GuildConfig guildConfig, string embedTitle)
        {
            // Replace < >
            query = query.Replace("<", "").Replace(">", "");

            await _lavaNode.SearchAsync(query);
            // If query starts with path - load from directory
            if (query.StartsWith("path:"))
            {
                var searchResponse = await _lavaNode.SearchAsync($@"{query.Replace("path:", "")}");
                return new TrackRequest() { Track = searchResponse.Tracks.FirstOrDefault() };
            }
            // If query is URL
            else if (Uri.IsWellFormedUriString(query, UriKind.Absolute))
            {
                // Get Search results
                var searchResponse = await _lavaNode.SearchAsync(query);
                return new TrackRequest() { Track = searchResponse.Tracks.FirstOrDefault() };
            }
            else
            {
                // Get Youtube Search results
                var searchResponse = await _lavaNode.SearchYouTubeAsync(query);

                // If nothing was found on YT - Check SoundCloud
                if (searchResponse.LoadStatus == LoadStatus.NoMatches)
                    searchResponse = await _lavaNode.SearchSoundCloudAsync(query);

                if (searchResponse.Exception.Message != null)
                    return new TrackRequest() { Embed = await EmbedHandler.CreateErrorEmbed(embedTitle, searchResponse.Exception.Message) };

                if (searchResponse.Tracks.Count == 0)
                    return new TrackRequest() { Embed = await EmbedHandler.CreateErrorEmbed(embedTitle, $"Nothing found for \"**{query}**\"") };

                StringBuilder tracks = new StringBuilder();
                tracks.Append("**You have 1m to select a song by typing the song index.\nOr you can type 0 to cancel the selection.**\n");

                //Maximum Tracks to select from
                int maxTrackNum = 5;

                // If Found Tracks are less than maxTrackNum - Change maxTrackNum
                // to Tracks count
                if (searchResponse.Tracks.Count < maxTrackNum)
                    maxTrackNum = searchResponse.Tracks.Count;

                // Append all Tracks
                for (int i = 0; i < maxTrackNum; i++)
                    tracks.Append($"{i + 1}. [{searchResponse.Tracks[i].Title}]({searchResponse.Tracks[i].Url})\n");

                // Send all found Tracks
                await context.Channel.SendMessageAsync(embed:
                    await EmbedHandler.CreateBasicEmbed(embedTitle, tracks.ToString()));

                // Wait for user reply
                var result = await _interactivityService.NextMessageAsync(
                    x => x.Author.Id == context.User.Id && !x.Content.StartsWith(guildConfig.Prefix),
                    timeout: TimeSpan.FromMinutes(1), runOnGateway: false);

                if (!result.IsSuccess)
                    return new TrackRequest() { Embed = await EmbedHandler.CreateErrorEmbed(embedTitle, $"**You ran out of time!**") };

                // If not an Int OR Out of boundaries
                if (!int.TryParse(result.Value.Content, out int index) || index > maxTrackNum)
                    return new TrackRequest() { Embed = await EmbedHandler.CreateErrorEmbed(embedTitle, "Not a valid option!") };

                // Index 0 is used for cancelling
                if (index == 0)
                    return new TrackRequest() { Embed = await EmbedHandler.CreateBasicEmbed(embedTitle, $"**{context.User.Mention} cancelled the selection.**") };

                // Use selected Track
                return new TrackRequest() { Track = searchResponse.Tracks[index - 1] };
            }
        }
        #endregion

        #region PlayTrackAsync
        public async Task<Embed> PlayTrackAsync(LavaPlayer player, LavaTrack track, GuildConfig guildConfig, string embedTitle)
        {
            // Update Bot Volume
            await player.UpdateVolumeAsync((ushort)guildConfig.Volume);

            // If Bot is playing track OR PlayerState is Paused - add song to queue
            if (player.Track != null && player.PlayerState is PlayerState.Playing || player.PlayerState is PlayerState.Paused)
            {
                // Add Track to queue
                player.Queue.Enqueue(track);

                // Return Successful Embed
                return await EmbedHandler.CreateBasicEmbed(embedTitle, $"**[{track.Title}]({track.Url})** has been added to queue.");
            }

            // Play selected Track
            await player.PlayAsync(track);

            // Return Successful Embed
            return await EmbedHandler.CreateBasicEmbed(embedTitle, $"Now playing **[{track.Title}]({track.Url})**");
        }
        #endregion

        #region TrackEndedAsync
        /// <summary>
        /// Play next song on TrackEnded
        /// </summary>
        /// <param name="args">TrackEndedEventArgs</param>
        /// <returns>Discord.Embed</returns>
        public async Task TrackEndedAsync(TrackEndedEventArgs args)
        {
            string embedTitle = "Music, Next Song";

            // ShouldPlayNext() returns true when TrackEndReason is FINISHED 
            if (!args.Reason.ShouldPlayNext())
                return;

            // Get Guild Config
            GlobalData.GuildConfigs.TryGetValue(args.Player.TextChannel.Guild.Id, out GuildConfig guildConfig);

            // If Guild Track looping is enabled - play same Track
            if (guildConfig.IsLooping)
            {
                await args.Player.PlayAsync(args.Track);
                return;
            }

            // If Queue is empty - return
            if (!args.Player.Queue.TryDequeue(out LavaTrack track))
                return;

            // Save Track that ended
            LavaTrack endedTrack = args.Track;

            // If Guild volume has changed - Update Bot volume
            if (guildConfig.Volume != args.Player.Volume)
                await args.Player.UpdateVolumeAsync((ushort)guildConfig.Volume);

            // Play next track
            await args.Player.PlayAsync(track);

            // Return Successful Embed
            await args.Player.TextChannel.SendMessageAsync(
                embed: await EmbedHandler.CreateBasicEmbed(embedTitle, $"**[{endedTrack.Title}]({endedTrack.Url})** finished playing.\n" +
                $"Now playing: **[{track.Title}]({track.Url})**"));
        }
        #endregion

        #region MusicChecks
        /// <summary>
        /// Essential checks for all music commands
        /// </summary>
        /// <param name="context">Command context</param>
        /// <param name="embedTitle">Embed title</param>
        /// <returns>Discord.Embed</returns>
        private async Task<Embed> MusicChecks(SocketCommandContext context, string embedTitle)
        {
            // If a connection to Lavalink was made
            if (!_lavaNode.IsConnected)
                return await EmbedHandler.CreateErrorEmbed(embedTitle, "We are experiencing problems with the Music Library!\nPlease report this to the Developers.");

            // Check if Guild Has Player
            if (!_lavaNode.HasPlayer(context.Guild))
                return await EmbedHandler.CreateErrorEmbed(embedTitle, "I'm not connected to a voice channel!");

            // Check if User is in a VC
            if ((context.User as IVoiceState).VoiceChannel == null)
                return await EmbedHandler.CreateErrorEmbed(embedTitle, "You can't use this command because you aren't in a Voice Channel!");

            // Check if User is in same VC as Bot
            if (_lavaNode.GetPlayer(context.Guild).VoiceChannel.Id != (context.User as IVoiceState).VoiceChannel.Id)
                return await EmbedHandler.CreateErrorEmbed(embedTitle, "You can't use this command because you aren't in the same channel as the bot!");

            return null;
        }
        #endregion
    }
}
