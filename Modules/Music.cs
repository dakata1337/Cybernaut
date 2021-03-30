
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord_Bot.DataStrucs;
using Discord_Bot.Handlers;
using Interactivity;
using Microsoft.Extensions.DependencyInjection;
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

            // Update Bot Volume
            await player.UpdateVolumeAsync((ushort)guildConfig.volume);


            var searchResponse = new SearchResponse();
            LavaTrack track;
            if (Uri.IsWellFormedUriString(query, UriKind.Absolute))
            {
                // Get Search results
                searchResponse = await _lavaNode.SearchAsync(query);
                track = searchResponse.Tracks.FirstOrDefault();
            }
            else
            {
                // Get Youtube Search results
                searchResponse = await _lavaNode.SearchYouTubeAsync(query);

                // If nothing was found on YT - Check SoundCloud
                if (searchResponse.LoadStatus == LoadStatus.NoMatches)
                    searchResponse = await _lavaNode.SearchSoundCloudAsync(query);

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
                    x => x.Author.Id == context.User.Id && !x.Content.StartsWith(guildConfig.prefix),
                    timeout: TimeSpan.FromMinutes(1), runOnGateway: false);

                if (!result.IsSuccess)
                    return await EmbedHandler.CreateErrorEmbed(embedTitle, $"**You ran out of time!**");

                // If not an Int OR Out of boundaries
                if (!int.TryParse(result.Value.Content, out int index) || index > maxTrackNum)
                    return await EmbedHandler.CreateErrorEmbed(embedTitle, "Not a valid option!");

                // Index 0 is used for cancelling
                if (index == 0)
                    return await EmbedHandler.CreateBasicEmbed(embedTitle, $"**{context.User.Mention} cancelled the selection.**");

                // Use selected Track
                track = searchResponse.Tracks[index - 1];
            }

            // If playNext - add Track on top of Queue
            if (playNext)
                return await AddOnTopOfQueueAsync(context, track);

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

        #region AddOnTopOfQueueAsync
        public async Task<Embed> AddOnTopOfQueueAsync(SocketCommandContext context, LavaTrack track)
        {
            string embedTitle = "Music, Play";

            // Get Player
            var player = _lavaNode.GetPlayer(context.Guild);

            // Get Guild Config
            GlobalData.GuildConfigs.TryGetValue(context.Guild.Id, out GuildConfig guildConfig);

            string status = guildConfig.isLooping ? "looping" : "playing";

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
        public async Task<Embed> GetQueueAsync(SocketCommandContext context)
        {
            string embedTitle = "Music, Queue";

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
            string status = (config.isLooping ? "looping" : "playing");
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
                return await EmbedHandler.CreateBasicEmbed(embedTitle, $"Volume is set to {guildConfig.volume}");

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
        public async Task<Embed> LoopAsync(SocketCommandContext context, string arg)
        {
            string embedTitle = "Music, Loop";

            // Get Guild Config
            GlobalData.GuildConfigs.TryGetValue(context.Guild.Id, out GuildConfig guildConfig);

            if (arg != null && arg.ToLower().Trim() == "status")
                return await EmbedHandler.CreateBasicEmbed(embedTitle, $"Looping is {(guildConfig.isLooping ? "enabled" : "disabled")}.");

            var checkResult = MusicChecks(context, embedTitle).Result;
            if (checkResult != null)
                return checkResult;

            // Update Looping
            GuildConfigHandler.UpdateGuildConfig(context.Guild, "isLooping", $"{(guildConfig.isLooping ? "0" : "1")}");

            // Return Successful Embed
            return await EmbedHandler.CreateBasicEmbed("Looping Status", $"Looping is now {(guildConfig.isLooping ? "disabled" : "enabled")}.");
        }
        #endregion

        #region SeekAsync
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

        #region TrackEndedAsync
        public async Task TrackEndedAsync(TrackEndedEventArgs args)
        {
            string embedTitle = "Music, Next Song";

            // ShouldPlayNext() returns true when TrackEndReason is FINISHED 
            if (!args.Reason.ShouldPlayNext())
                return;

            // Get Guild Config
            GlobalData.GuildConfigs.TryGetValue(args.Player.TextChannel.Guild.Id, out GuildConfig guildConfig);

            // If Guild Track looping is enabled - play same Track
            if (guildConfig.isLooping)
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
            if (guildConfig.volume != args.Player.Volume)
                await args.Player.UpdateVolumeAsync((ushort)guildConfig.volume);

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
