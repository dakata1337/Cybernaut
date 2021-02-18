using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Discord_Bot.Handlers;
using Discord_Bot.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Victoria;
using Victoria.Enums;
using Victoria.EventArgs;
using Victoria.Responses.Rest;
using Microsoft.Extensions.DependencyInjection;
using Discord_Bot.DataStrucs;
using System.Globalization;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using Interactivity;

namespace Discord_Bot.Modules
{
    class Music
    {
        private readonly LavaNode _lavaNode;
        private readonly MySQL _mySQL;
        private readonly InteractivityService _interactivityService;
        public Music(IServiceProvider serviceProvider)
        { 
            _lavaNode = serviceProvider.GetRequiredService<LavaNode>();
            _mySQL = serviceProvider.GetRequiredService<MySQL>();
            _interactivityService = serviceProvider.GetRequiredService<InteractivityService>();
        }

        #region Join
        public async Task<Embed> JoinAsync(SocketCommandContext context)
        {
            //Checks if the bot is already connected
            if (_lavaNode.HasPlayer(context.Guild))
                return await EmbedHandler.CreateErrorEmbed("Music, Join", "I'm already connected to a voice channel!");

            var voiceState = context.User as IVoiceState;
            if (voiceState.VoiceChannel is null)
                return await EmbedHandler.CreateErrorEmbed("Music, Join", "You can't use this command because you aren't in a Voice Channel!");

            try
            {
                //Change isLooping to false;
                _mySQL.UpdateGuildConfig(context.Guild, "isLooping", $"0");

                var textChannel = context.Channel;
                //The Bot joins the channel
                await _lavaNode.JoinAsync(voiceState.VoiceChannel, textChannel as ITextChannel);

                GlobalData.GuildConfigs.TryGetValue(context.Guild.Id, out GuildConfig config);

                //Log information to Console & Discord
                LoggingService.Log("JoinAsync", $"Bot joined \"{voiceState.VoiceChannel.Name}\" ({context.Guild.Id})");
                return await EmbedHandler.CreateBasicEmbed("Music, Join", $"Joined {voiceState.VoiceChannel.Name}.\n" +
                    $"**WARNING!** - to avoid earrape lower the volume ({config.prefix}volume).\n Current volume is {config.volume}.");
            }
            catch (Exception ex)
            {
                //Log information to Discord
                return await EmbedHandler.CreateErrorEmbed("Music, Join", ex.Message);
            }
        }
        #endregion

        #region Leave
        public async Task<Embed> LeaveAsync(SocketCommandContext context)
        {
            var user = context.User as SocketGuildUser;
            Embed sameChannel = await SameChannelAsBot(context.Guild, user, "LeaveAsync");
            if (sameChannel != null) //Checks If User is in the same Voice Channel as the bot.
            {
                return sameChannel;
            }

            try
            {
                //Get the Guild Player
                var player = _lavaNode.GetPlayer(context.Guild);

                //If The Player is playing, Stop it.
                if (player.PlayerState is PlayerState.Playing)
                {
                    await player.StopAsync();
                }

                //Voice Channel
                var vc = player.VoiceChannel;

                //Leave the voice channel.
                await _lavaNode.LeaveAsync(vc);

                //Log information to Console & Discord
                LoggingService.Log("LeaveAsync", $"Bot has left \"{vc}\". ({context.Guild.Id})");
                return await EmbedHandler.CreateBasicEmbed("LeaveAsync", $"I'm sorry that I gave you up :'(.");
            }

            //Throws the error in discord
            catch (InvalidOperationException ex)
            {
                return await EmbedHandler.CreateErrorEmbed("LeaveAsync", ex.Message);
            }
        }
        #endregion

        #region Play
        public async Task<Embed> PlayAsync(SocketCommandContext context, string query, bool playNext = false)
        {
            GlobalData.GuildConfigs.TryGetValue(context.Guild.Id, out GuildConfig guildConfig);

            var user = context.User as SocketGuildUser;

            Embed sameChannel = await SameChannelAsBot(context.Guild, user, "PlayAsync");
            //Checks If User is in the same Voice Channel as the bot.
            if (sameChannel != null)
            {
                return sameChannel;
            }

            try
            {
                //Get the player for that guild.
                var player = _lavaNode.GetPlayer(context.Guild);

                LavaTrack track = null;
                var search = new SearchResponse();


                //If query starts with "path:" try load track form location 
                if (query.StartsWith("path:"))
                {
                    query = query.Remove(0, 5);
                    search = await _lavaNode.SearchAsync(@$"{query}");

                    track = search.Tracks.FirstOrDefault();
                }

                //Search for the query
                else if (Uri.IsWellFormedUriString(query, UriKind.Absolute))
                {
                    search = await _lavaNode.SearchAsync(query);

                    #region LoadFailed/NoMatches Check
                    //If load failed, tell the user
                    if (search.LoadStatus == LoadStatus.LoadFailed)
                        return await EmbedHandler.CreateErrorEmbed("Music, Play", $"Failed to load {query}.\n{search.Exception.Message}");

                    //If we couldn't find anything, tell the user.
                    if (search.LoadStatus == LoadStatus.NoMatches)
                        return await EmbedHandler.CreateErrorEmbed("Music, Play", $"I wasn't able to find anything for {query}.");
                    #endregion

                    track = search.Tracks.FirstOrDefault();
                }
                else
                {
                    #region If the Query isn't a link
                    search = await _lavaNode.SearchYouTubeAsync(query);

                    #region LoadFailed/NoMatches Check
                    //If load failed, tell the user
                    if (search.LoadStatus == LoadStatus.LoadFailed)
                        return await EmbedHandler.CreateErrorEmbed("Music, Play", $"Failed to load {query}.\n{search.Exception.Message}");

                    //If we couldn't find anything, tell the user.
                    if (search.LoadStatus == LoadStatus.NoMatches)
                        return await EmbedHandler.CreateErrorEmbed("Music, Play", $"I wasn't able to find anything for {query}.");
                    #endregion

                    #region Send available songs list
                    //Show the first 5 songs from the search
                    StringBuilder builder = new StringBuilder();
                    builder.Append("**You have 1 minute to select a song by typing the number of the song.**\n");
                    for (int i = 0; i < 5; i++)
                    {
                        builder.Append($"{i + 1}. {search.Tracks[i].Title}\n");
                    }

                    //Send the message to the channel
                    var select = await SendMessageAsync(await EmbedHandler.CreateBasicEmbed("Music, Select", builder.ToString()), context);
                    #endregion

                    #region Reply Check

                    while (true)
                    {
                        //Wait for users reply
                        var result = await _interactivityService.NextMessageAsync(x => x.Author == context.User);
                        //Get reply
                        string reply = result.Value.Content;

                        //If the user types another command wait for another reponse
                        if(reply.StartsWith(guildConfig.prefix))
                            continue;

                        //If the reponse isn't an intiger return
                        if (!int.TryParse(reply, out int index))
                            return await EmbedHandler.CreateErrorEmbed("Music, Select", "The reply isn't an intiger!");

                        track = search.Tracks[index - 1];
                        break;
                    }
                    #endregion

                    #endregion
                }


                #region Update Volume
                //Get Guild Config
                GlobalData.GuildConfigs.TryGetValue(context.Guild.Id, out GuildConfig config);

                //Get islooping Bool
                string status = config.isLooping == true ? "looping" : "playing";

                //Update Player Volume
                await player.UpdateVolumeAsync((ushort)config.volume);
                #endregion

                #region Plays / Adds song to queue
                if (playNext)
                {
                    return await AddOnTopOfQueueAsync(context, track);
                }
                else
                {
                    //If the Bot is already playing music, or if it is paused but still has music in the playlist, Add the requested track to the queue.
                    if (player.Track != null && player.PlayerState is PlayerState.Playing || player.PlayerState is PlayerState.Paused)
                    {
                        player.Queue.Enqueue(track);
                        LoggingService.Log("PlayAsync", $"\"{track.Title}\" has been added to the music queue. ({context.Guild.Id})");
                        return await EmbedHandler.CreateBasicEmbed("Music, Play", $"**[{track.Title}]({track.Url})** has been added to queue.");
                    }

                    //Play the track
                    await player.PlayAsync(track);
                }

                //Log information to Console & Discord
                LoggingService.Log("PlayAsync", $"Bot now {status}: {track.Title} ({context.Guild.Id})");
                if (Uri.IsWellFormedUriString(track.Url, UriKind.Absolute))
                    return await EmbedHandler.CreateBasicEmbed("Music, Play", $"Now {status}: [{track.Title}]({track.Url})");
                else
                    return await EmbedHandler.CreateBasicEmbed("Music, Play", $"Now {status}: {track.Url}");
                #endregion
            }
            catch (Exception ex) //Throws the error in discord
            {
                return await EmbedHandler.CreateErrorEmbed("Music, Play", ex.Message);
            }
        }
        #endregion

        #region Add on Top of Queue
        public async Task<Embed> AddOnTopOfQueueAsync(SocketCommandContext context, LavaTrack track)
        {
            var player = _lavaNode.GetPlayer(context.Guild);

            //Get Guild Config
            GlobalData.GuildConfigs.TryGetValue(context.Guild.Id, out GuildConfig config);

            //Get islooping Bool
            string status = config.isLooping == true ? "looping" : "playing";


            //If the Player is not playing & Queue is empty play the song
            if (player.PlayerState != PlayerState.Playing && player.Queue.Count == 0)
            {
                await player.PlayAsync(track);
                return await EmbedHandler.CreateBasicEmbed("Music, Play", $"Now {status}: [{track.Title}]({track.Url})");
            }

            //Save the Queue
            List<LavaTrack> orignalQueue = new List<LavaTrack>(player.Queue);

            //Clear the Queue
            player.Queue.Clear();

            //Add the song first
            player.Queue.Enqueue(track);

            //Then add the old songs from the old Queue
            for (int i = 0; i < orignalQueue.Count; i++)
                player.Queue.Enqueue(orignalQueue[i]);

            return await EmbedHandler.CreateBasicEmbed("Music, Play", $"**[{track.Title}]({track.Url})** was added on the top of the queue.");
        }
        #endregion

        #region Skip
        public async Task<Embed> SkipTrackAsync(SocketCommandContext context)
        {
            //Checks If User is in the same Voice Channel as the bot.
            var user = context.User as SocketGuildUser;
            Embed sameChannel = await SameChannelAsBot(context.Guild, user, "SkipTrackAsync");
            if (sameChannel != null)
            {
                return sameChannel;
            }

            try
            {
                //Get Guild Player
                var player = _lavaNode.GetPlayer(context.Guild);

                //Make sure Guild Play isn't null
                if (player == null)
                    return await EmbedHandler.CreateErrorEmbed("Music, Skip", $"Could not aquire player.\nAre you using the bot right now?");

                /* Check The queue, if it is less than one (meaning we only have the current song available to skip) it wont allow the user to skip.
                     User is expected to use the Stop command if they're only wanting to skip the current song. */
                if (player.Queue.Count < 1)
                {
                    return await StopAsync(context);
                }
                else
                {
                    //Save the current song for use after we skip it
                    var currentTrack = player.Track;

                    //Skip the current song.
                    await player.SkipAsync();

                    //Log information to Console & Discord
                    LoggingService.Log("SkipTrackAsync", $"Bot skipped: \"{currentTrack.Title}\" ({context.Guild.Id})");
                    return await EmbedHandler.CreateBasicEmbed("Music, Skip",
                        $"I have successfully skiped [{currentTrack.Title}]({currentTrack.Url}).\n" +
                        $"**Now playing**: [{player.Track.Title}]({player.Track.Url}).");
                }
            }

            //Throws the error in discord
            catch (Exception ex)
            {
                return await EmbedHandler.CreateErrorEmbed("Music, Skip", ex.Message);
            }

        }
        #endregion

        #region Stop
        public async Task<Embed> StopAsync(SocketCommandContext context)
        {
            var user = context.User as SocketGuildUser;
            //Checks If User is in the same Voice Channel as the bot.
            Embed sameChannel = await SameChannelAsBot(context.Guild, user, "StopAsync");
            if (sameChannel != null)
            {
                return sameChannel;
            }

            try
            {
                //Get Guild Player
                var player = _lavaNode.GetPlayer(context.Guild);

                //Make sure Guild Play isn't null
                if (player == null)
                    return await EmbedHandler.CreateErrorEmbed("Music, Stop", $"Could not aquire player.\nAre you using the bot right now?");

                //If the player is Playing
                if (player.PlayerState is PlayerState.Playing)
                {
                    //Clear queue
                    player.Queue.ToList().ForEach(x => player.Queue.Remove(x));

                    //Stop player
                    await player.StopAsync();

                    //Log information to Console & Discord
                    LoggingService.Log("StopAsync", $"Bot has stopped playback. ({context.Guild.Id})");
                    return await EmbedHandler.CreateBasicEmbed("Music, Stop", "I Have stopped playback & the playlist has been cleared.");
                }

                //If the bot is not playing anything 
                return await EmbedHandler.CreateErrorEmbed("Music, Stop", $"The bot is currently not playing music.");
            }

            //Throws the error in discord
            catch (Exception ex)
            {
                //Log information to Discord
                return await EmbedHandler.CreateErrorEmbed("Music, Stop", ex.Message);
            }

        }
        #endregion

        #region Pause
        public async Task<Embed> PauseAsync(SocketCommandContext context)
        {
            //Checks If User is in the same Voice Channel as the bot.
            var user = context.User as SocketGuildUser;
            Embed sameChannel = await SameChannelAsBot(context.Guild, user, "PauseAsync");
            if (sameChannel != null)
            {
                return sameChannel;
            }

            try
            {
                //Gets Guild Player
                var player = _lavaNode.GetPlayer(context.Guild);

                //If the Player isn't playing return
                if (!(player.PlayerState is PlayerState.Playing))
                {
                    return await EmbedHandler.CreateBasicEmbed("Music, Pause", $"There is nothing to pause.");
                }

                //Pause the Player
                await player.PauseAsync();

                //Log information to Console & Discord
                LoggingService.Log("PauseAsync", $"Paused: \"{player.Track.Title}\" - {player.Track.Author} ({context.Guild.Id})");
                return await EmbedHandler.CreateBasicEmbed("Music, Pause", $"**Paused:** {player.Track.Title} - {player.Track.Author}.");
            }

            //Throws the error in discord
            catch (InvalidOperationException ex)
            {
                return await EmbedHandler.CreateErrorEmbed("Music, Pause", ex.Message);
            }
        }
        #endregion

        #region Resume
        public async Task<Embed> ResumeAsync(SocketCommandContext context)
        {
            //Checks If User is in the same Voice Channel as the bot.
            var user = context.User as SocketGuildUser;
            Embed sameChannel = await SameChannelAsBot(context.Guild, user, "ResumeAsync");
            if (sameChannel != null)
            {
                return sameChannel;
            }

            try
            {
                //Get Guild Player
                var player = _lavaNode.GetPlayer(context.Guild);

                //If the Player isn't Paused 
                if (!(player.PlayerState is PlayerState.Paused))
                {
                    return await EmbedHandler.CreateErrorEmbed("Music, Resume", $"Player wasn't Paused.");
                }

                //Resume the Player
                await player.ResumeAsync();

                //Log information to Console & Discord
                LoggingService.Log("ResumeAsync", $"Resumed: \"{player.Track.Title}\" - {player.Track.Author} ({context.Guild.Id})");
                return await EmbedHandler.CreateBasicEmbed("Music, Resume", $"**Resumed:** {player.Track.Title} - {player.Track.Author}.");
            }

            //Throws the error in discord
            catch (InvalidOperationException ex)
            {
                return await EmbedHandler.CreateErrorEmbed("Music, Resume", ex.Message);
            }

        }
        #endregion

        #region Queue
        public async Task<Embed> GetQueueAsync(SocketCommandContext context)
        {
            //Checks If User is in the same Voice Channel as the bot.
            var user = context.User as SocketGuildUser;
            Embed sameChannel = await SameChannelAsBot(context.Guild, user, "ListAsync");
            if (sameChannel != null)
            {
                return sameChannel;
            }

            try
            {
                //Get the Player and make sure it isn't null.
                var player = _lavaNode.GetPlayer(context.Guild);
                if (player == null)
                    return await EmbedHandler.CreateErrorEmbed("Music", $"Could not aquire player.\nAre you using the bot right now? ");

                //If the player is not playing anything notify the user
                if (player.PlayerState != PlayerState.Playing)
                    return await EmbedHandler.CreateErrorEmbed("Music, List", "Player doesn't seem to be playing anything right now.");

                //Get Guild Config
                GlobalData.GuildConfigs.TryGetValue(context.Guild.Id, out GuildConfig config);
                bool isLooping = config.isLooping;

                StringBuilder playerBuilder = new StringBuilder();
                double tickLocation = player.Track.Position.TotalSeconds / (player.Track.Duration.TotalSeconds / 20);


                if (!player.Track.IsStream)
                {
                    //Track Current Position
                    playerBuilder.Append($"{player.Track.Position.ToString(@"hh\:mm\:ss")} ");

                    //Dot Location
                    int rounded = (int)Math.Round(tickLocation, 0, MidpointRounding.AwayFromZero);
                    for (int i = 0; i < 20; i++)
                    {
                        if (i == rounded)
                        {
                            playerBuilder.Append("🔘");
                            continue;
                        }
                        playerBuilder.Append("▬");
                    }

                    //Track Duration
                    playerBuilder.Append($" {player.Track.Duration}");
                }
                else
                {
                    playerBuilder.Append($"This is a stream.");
                }

                StringBuilder builder = new StringBuilder();
                builder.Append(isLooping ? "**Now Looping: " : "**Now Playing: ");
                builder.Append($"[{player.Track.Title}]({player.Track.Url})**" + Environment.NewLine);
                builder.Append(playerBuilder + Environment.NewLine);

                //If the queue is empty
                if (player.Queue.Count < 1 && player.Track != null)
                {
                    //Log information to Discord
                    builder.Append("Nothing else is queued");
                }

                //If the queue isnt empty
                else
                {
                    //Create a string builder we can use to format how we want our list to be displayed.
                    var descriptionBuilder = new StringBuilder();

                    var trackNum = 2;
                    //Foreach track in queue
                    foreach (LavaTrack track in player.Queue)
                    {
                        //Add to the description builder
                        descriptionBuilder.Append($"{trackNum}: [{track.Title}]({track.Url})" + Environment.NewLine);

                        //Increment next track number
                        trackNum++;
                    }

                    builder.Append(descriptionBuilder);
                }

                //Log information to Discord
                return await EmbedHandler.CreateBasicEmbed("Music, List", builder.ToString());
            }

            //Throws the error in discord
            catch (Exception ex)
            {
                //Log information to Discord
                return await EmbedHandler.CreateErrorEmbed("Music, List", ex.Message);
            }
        }
        #endregion

        #region Set Volume
        public async Task<Embed> SetVolumeAsync(SocketCommandContext context, int? volume)
        {
            //Checks If User is in the same Voice Channel as the bot.
            var user = context.User as SocketGuildUser;
            Embed sameChannel = await SameChannelAsBot(context.Guild, user, "SetVolumeAsync");
            if (sameChannel != null)
            {
                return sameChannel;
            }

            //Get Guild Config
            GlobalData.GuildConfigs.TryGetValue(context.Guild.Id, out GuildConfig config);

            //Get Player
            var player = _lavaNode.GetPlayer(context.Guild);

            //If volume is not specified display current volume
            if (volume is null)
                return await EmbedHandler.CreateBasicEmbed("Music, Volume", $"Volume is set to {config.volume}");

            //Checks if the volume is the range 1-150
            if (volume > 150 || volume <= 0)
                return await EmbedHandler.CreateBasicEmbed("Music, Volume", $"Volume must be between 1 and 150.");

            try
            {
                //Changes the volume 
                _mySQL.UpdateGuildConfig(context.Guild, "volume", $"{volume}");

                //Updates player volume
                await player.UpdateVolumeAsync((ushort)volume);

                //Log information to Console & Discord
                LoggingService.Log("SetVolumeAsync", $"Bot Volume set to: {player.Volume} ({context.Guild.Id})");
                return await EmbedHandler.CreateBasicEmbed("Music, Volume", $"Volume has been set to {player.Volume}.");
            }

            //Throws the error in discord
            catch (InvalidOperationException ex)
            {
                return await EmbedHandler.CreateErrorEmbed("Music, Volume", ex.Message);
            }
        }
        #endregion

        #region Loop
        public async Task<Embed> LoopAsync(SocketCommandContext context, string arg)
        {
            //Get Guild Config
            GlobalData.GuildConfigs.TryGetValue(context.Guild.Id, out GuildConfig config);

            //If an argument is given 
            var user = context.User as SocketGuildUser;
            if (arg != null)
            {
                //Check if argument is "status"
                if (arg.ToLower().TrimEnd(' ') == "status")
                    //Display looping status 
                    return await EmbedHandler.CreateBasicEmbed("Loop, Status", $"Looping is {(config.isLooping ? "enabled" : "disabled")}.");
                else
                    return await EmbedHandler.CreateErrorEmbed("Loop, Status", $"{arg} is not a valid argument.");
            }

            //Checks If User is in the same Voice Channel as the bot.
            Embed sameChannel = await SameChannelAsBot(context.Guild, user, "LoopAsync");
            if (sameChannel != null)
            {
                return sameChannel;
            }

            //Change isLooping to opposite value
            bool isLooping = !config.isLooping;

            //Save Config
            _mySQL.UpdateGuildConfig(context.Guild, "isLooping", $"{(isLooping == true ? "1" : "0")}");

            //Log information to Console & Discord
            LoggingService.Log("LoopAsync", $"Looping is now {(isLooping ? "enabled" : "disabled")}. ({context.Guild.Id})");
            return await EmbedHandler.CreateBasicEmbed("Looping Status", $"Looping is now {(isLooping ? "enabled" : "disabled")}.");
        }
        #endregion

        #region Seek
        public async Task<Embed> SeekAsync(SocketCommandContext context, string seekTo)
        {
            Embed sameChannel = await SameChannelAsBot(context.Guild, context.User as SocketGuildUser, "SeekAsync");
            if (sameChannel != null)
            {
                return sameChannel;
            }

            var player = _lavaNode.GetPlayer(context.Guild);

            if (player.Track.IsStream)
                return await EmbedHandler.CreateErrorEmbed("Music, Seek", $"**{player.Track.Title} is stream.**");

            //var position = TimeSpan.ParseExact(seekTo, "HH\\:mm\\:ss", CultureInfo.InvariantCulture);
            var count = seekTo.Count(f => f == ':');

            TimeSpan ts = new TimeSpan();
            if (count == 1)
                ts = DateTime.ParseExact(seekTo, "mm\\:ss", CultureInfo.InvariantCulture).TimeOfDay;
            else if (count == 2)
                ts = DateTime.ParseExact(seekTo, "HH\\:mm\\:ss", CultureInfo.InvariantCulture).TimeOfDay;
            else
                return await EmbedHandler.CreateErrorEmbed("Music, Seek", $"**Unsupported time format**");


            var duration = player.Track.Duration;
            if (ts.TotalSeconds > duration.TotalSeconds)
                return await EmbedHandler.CreateErrorEmbed("Music, Seek", $"**Value must be no bigger than {duration.ToString(@"hh\:mm\:ss")}!**");

            var savedTrack = player.Track;
            await player.SeekAsync(ts);

            Thread.Sleep(100);
            LoggingService.Log("SeekAsync", $"{(player.Track is null ? $"{savedTrack.Title} ended!" : $"{player.Track.Title} - position changed to: {player.Track.Position}")}");
            return await EmbedHandler.CreateBasicEmbed("Music, Seek", $"{(player.Track is null ? $"**{savedTrack.Title} ended!**" : $"**Position changed to: {player.Track.Position}!**")}");
        }
        #endregion

        #region Lyrics
        public async Task<Embed> GetLyricsAsync(SocketCommandContext context)
        {
            //Checks If User is in the same Voice Channel as the bot.
            Embed sameChannel = await SameChannelAsBot(context.Guild, (SocketGuildUser)context.Message.Author, "GetLyricsAsync");
            if (sameChannel != null)
            {
                return sameChannel;
            }

            //Get Guild Player
            var player = _lavaNode.GetPlayer(context.Guild);

            //If the player isn't playing return
            if (player.PlayerState != PlayerState.Playing)
                return await EmbedHandler.CreateErrorEmbed("Music, Lyrics", "The bot is currently not playing music.");

            //Get Context
            IDMChannel dmChannel = await context.Message.Author.GetOrCreateDMChannelAsync();

            //Gets Lyrics from Genius
            string lyrics = await player.Track.FetchLyricsFromGeniusAsync();

            //If no lyrics found form Genius try OVH
            if (lyrics.Length < 2)
                lyrics = await player.Track.FetchLyricsFromOVHAsync();

            //If no lyrics found return
            if (lyrics.Length < 2)
                return await EmbedHandler.CreateErrorEmbed("Music, Lyrics", "Sorry we couldn't find the lyrics you requested.");

            string file = $"lyrics-{context.Message.Author.Id}-{DateTime.UtcNow.ToString("HH-mm-ss_dd-MM-yyyy")}.txt";

            //Create File and Save lyrics
            File.Create(file).Dispose();
            File.WriteAllText(file, lyrics);

            //Check if the file is empty
            if (new System.IO.FileInfo(file).Length == 0)
            {
                File.Delete(file);
                return await EmbedHandler.CreateErrorEmbed("Music, Lyrics", "Sorry we couldn't find the lyrics you requested.");
            }

            //Send File
            await dmChannel.SendFileAsync(file, $"We found lyrics for the song: {player.Track.Title}");

            //Delete File
            File.Delete(file);
            return await EmbedHandler.CreateBasicEmbed("Music, Lyrics", $"{context.Message.Author.Mention} Please check your DMs for the lyrics you requested.");
        }
        #endregion

        #region Playlists

        public async Task<Embed> Playlist(SocketCommandContext context, string arg1 = null, string arg2 = null, string arg3 = null, string arg4 = null)
        {
            #region Explanations
            /* arg1 = command
             * arg2 = playlist name
             * arg3 = what to do in the playlist
             * arg4 = selected song
             */
            #endregion

            #region Cases
            //Command Argument Check
            switch (arg1.ToLower())
            {
                case "show":
                    return await Playlist_Show(context, arg2);
                case "load":
                    return await Playlist_Play(context, arg2);
                case "modify":
                    return await Playlist_ChangePlaylist(context, arg2, arg3, arg4);
                case "create":
                    return await Playlist_CreatePlaylist(context, arg2);
                case "remove":
                    return await Playlist_RemovePlaylist(context, arg2);
                default:
                    return await EmbedHandler.CreateErrorEmbed("Music, Playlist", $"{arg1} is not a valid argument.");
            }
            #endregion
        }

        private async Task<Embed> Playlist_Show(SocketCommandContext context, string arg2)
        {
            #region JSON variables
            //Get Guild Config
            GlobalData.GuildConfigs.TryGetValue(context.Guild.Id, out GuildConfig config);

            //Get Guild Playlists
            JArray playlists = config.playlists;
            #endregion

            #region Code
            if (playlists is null)
                return await EmbedHandler.CreateBasicEmbed("Music, Playlist", "No playlists where found!");

            StringBuilder builder = new StringBuilder();
            bool found = false;
            if (!(playlists is null))
            {
                //If arg2 (playlist name) is null show all playlists 
                if (arg2 is null)
                {
                    builder.Append($"**Playlists:**\n");
                }

                int playlistCount = 1;
                foreach (var playlist in playlists)
                {
                    if (!(playlist is null))
                    {
                        //Get Playlist Info
                        var playlistInfo = playlist.ToObject<Playlist>();


                        //If arg2 (playlist name) is null show all playlists 
                        if (arg2 is null)
                        {
                            //Shows only playlist name
                            builder.Append($"{playlistCount}. {playlistInfo.name} (Songs: {(playlistInfo.songs != null ? playlistInfo.songs.Length.ToString() : "0")})\n");

                            playlistCount++;
                            found = true;
                            continue;
                        }
                        //Show songs in playlist
                        else
                        {
                            //Check if the playlist name matches with the selected one by the user
                            if (playlistInfo.name.ToLower() != arg2.ToLower())
                                continue;

                            //Checks if there are any songs in the playlist
                            if (playlistInfo.songs.Length == 0)
                                return await EmbedHandler.CreateBasicEmbed("Music, Playlist", $"**{playlistInfo.name}** is empty.");

                            #region Display Playlist Songs

                            //Displays playlist name
                            builder.Append($"**Songs in {playlistInfo.name}:**\n");

                            int songCount = 1;
                            //Songs loop
                            foreach (JObject song in playlist["songs"])
                            {
                                //Displays song
                                var songInfo = song.ToObject<Song>();
                                builder.Append($"{songCount}. [{songInfo.name}]({songInfo.url})\n");
                                songCount++;
                            }
                            found = true;

                            //Break out of Playlists loop
                            break;
                            #endregion
                        }
                    }
                }
            }

            //True if Playlist found OR all Playlist names shown
            if (found)
                return await EmbedHandler.CreateBasicEmbed("Music, Playlist", $"{builder}");
            if (!found && arg2 == null)
                return await EmbedHandler.CreateBasicEmbed("Music, Playlist", "No playlists where found!");
            else
                return await EmbedHandler.CreateBasicEmbed("Music, Playlist", $"{arg2} was not found.");
            #endregion
        }

        private async Task<Embed> Playlist_Play(SocketCommandContext context, string arg2)
        {
            #region Checks
            var user = context.User as SocketGuildUser;

            #region Same Channel As Bot Check
            //Checks If User is in the same Voice Channel as the bot.
            Embed sameChannel = await SameChannelAsBot(context.Guild, user, "PlayAsync");
            if (sameChannel != null)
            {
                return sameChannel;
            }
            #endregion

            #region Selected Playlist Check
            //Checks if playlist is selected
            if (arg2 is null)
                return await EmbedHandler.CreateBasicEmbed("Music, Playlist", $"No playlist selected.");
            #endregion

            #endregion

            #region JSON variables
            //Get Guild Config
            GlobalData.GuildConfigs.TryGetValue(context.Guild.Id, out GuildConfig config);

            //Get Guild Playlists
            JArray playlists = config.playlists;
            #endregion

            #region Code
            bool found = false;
            if (!(playlists is null))
            {
                foreach (var playlist in playlists)
                {
                    if (!(playlist is null))
                    {
                        #region Playlist name check
                        //Get Song Info
                        var playlistInfo = playlist.ToObject<Playlist>();

                        //If the name of the playlist doesnt match the requested one
                        //go to the next playlist
                        if (playlistInfo.name.ToLower() != arg2.ToLower())
                            continue;
                        #endregion

                        //Disables the "Playlist not found" message
                        found = true;

                        await SendMessageAsync(await EmbedHandler.CreateBasicEmbed("Music, Playlist",
                                    $"**{playlistInfo.name}** is being loaded as we speak!"), context);

                        var player = _lavaNode.GetPlayer(context.Guild);
                        foreach (JObject song in playlist["songs"])
                        {
                            //Get song Info
                            var songInfo = song.ToObject<Song>();
                            LavaTrack track = null;

                            //Search for the song
                            var search = await _lavaNode.SearchAsync(songInfo.url);

                            if (search.LoadStatus.Equals(LoadStatus.LoadFailed))
                            {
                                await SendMessageAsync(await EmbedHandler.CreateErrorEmbed("Music, Playlist",
                                    $"Failed to load [{songInfo.name}]({songInfo.url}).\n" +
                                    $"Please check if the url is working!"), context);
                                continue;
                            }

                            if (search.LoadStatus.Equals(LoadStatus.NoMatches))
                            {
                                await SendMessageAsync(await EmbedHandler.CreateErrorEmbed("Music, Playlist",
                                    $"Failed to load {songInfo.url}.\n" +
                                    $"Please check if the url is working!"), context);
                                continue;
                            }

                            //Get First track found
                            track = search.Tracks.FirstOrDefault();

                            //If the Bot is already playing music, or if it is paused but still has music in the playlist, Add the requested track to the queue.
                            if (player.Track != null && player.PlayerState is PlayerState.Playing || player.PlayerState is PlayerState.Paused)
                            {
                                player.Queue.Enqueue(track);

                                //Continue to the next track
                                continue;
                            }

                            await player.UpdateVolumeAsync((ushort)config.volume);

                            //Play track
                            await player.PlayAsync(track);

                            //Send Message to Discord
                            await context.Channel.SendMessageAsync(embed:
                                await EmbedHandler.CreateBasicEmbed("Music, Play", $"Now " + (config.isLooping == true ? "looping" : "playing") +
                                $": [{track.Title}]({track.Url})"));
                        }
                    }
                    //If the playlist is already found there is not point of
                    //looking the next playlist
                    break;
                }
            }

            if (found)
            {
                LoggingService.Log("Playlist", $"{arg2} was loaded successfully.");
                return await EmbedHandler.CreateBasicEmbed("Music, Playlist", $"**{arg2}** was loaded.");
            }
            else
                return await EmbedHandler.CreateBasicEmbed("Music, Playlist", $"**{arg2}** was not found.\n" +
                                $"Use {config.prefix}playlist show to see all playlist.");
            #endregion
        }

        private async Task<Embed> Playlist_ChangePlaylist(SocketCommandContext context, string arg2, string arg3, string arg4)
        {
            #region Cases
            if (arg4 is null)
                return await EmbedHandler.CreateErrorEmbed("Music, Playlist", $"No song selected.");

            switch (arg3)
            {
                case "remove":
                    return await Playlist_RemoveSong(context, arg2, arg4);
                case "add":
                    return await Playlist_AddSong(context, arg2, arg4);
                default:
                    break;
            }
            return null;
            #endregion
        }

        private async Task<Embed> Playlist_RemoveSong(SocketCommandContext context, string arg2, string arg4)
        {
            #region Code
            #region Check if arg4 is url
            if (Uri.IsWellFormedUriString(arg4, UriKind.Absolute))
                return await EmbedHandler.CreateErrorEmbed("Music, Playlist", $"You can't use links. Use the name of the song.");
            #endregion

            #region Variables
            //Get Guild Config
            GlobalData.GuildConfigs.TryGetValue(context.Guild.Id, out GuildConfig config);

            //Get Guild Playlists
            JArray playlists = config.playlists;

            bool playlistFound = false;
            bool songFound = false;
            #endregion

            #region Remove Song From Playlist
            foreach (var playlist in playlists)
            {
                //Get Playlist Info
                var playlistInfo = playlist.ToObject<Playlist>();

                //If Playlist name doesnt match selected one continue
                if (playlistInfo.name.ToLower() != arg2.ToLower())
                    continue;

                //If the playlist is empty
                if (playlistInfo.songs.Length == 0)
                    return await EmbedHandler.CreateBasicEmbed("Music, Playlist", $"**{playlistInfo.name}** is empty.");

                //Get Playlist songs
                List<JObject> newSongs = new List<JObject>(playlist["songs"].ToObject<List<JObject>>());

                var songs = playlist["songs"].ToObject<Song[]>();
                int songIndex = 0;

                foreach (JObject song in playlist["songs"])
                {
                    var songInfo = song.ToObject<Song>();
                    if (songInfo.name.ToLower() != arg4.ToLower())
                    {
                        songIndex++;
                        continue;
                    }

                    //Remove song if found
                    songFound = true;
                    newSongs.RemoveAt(songIndex);
                    playlist["songs"] = JToken.FromObject(newSongs);

                    //Save Config
                    _mySQL.UpdateGuildConfig(context.Guild, "playlists", JsonConvert.SerializeObject(JToken.FromObject(playlists), Formatting.None));

                    return await EmbedHandler.CreateBasicEmbed("Music, Playlist", $"**{songInfo.name}** was removed from **{playlistInfo.name}**.");
                }

                playlistFound = true;
            }
            #endregion

            #region Response
            if (!playlistFound)
                return await EmbedHandler.CreateErrorEmbed("Music, Playlist", $"The playlist \"{arg2}\" was not found.");
            if (!songFound)
                return await EmbedHandler.CreateErrorEmbed("Music, Playlist", $"The song \"{arg4}\" wasn't found in \"{arg2}\".");

            return null; //This will never run :O
            #endregion
            #endregion
        }

        private async Task<Embed> Playlist_AddSong(SocketCommandContext context, string arg2, string arg4)
        {
            #region Code
            //Get Guild Config
            //Get Guild Config
            GlobalData.GuildConfigs.TryGetValue(context.Guild.Id, out GuildConfig config);

            //Get Guild Playlists
            JArray playlists = config.playlists;

            //Playlists loop
            foreach (var playlist in playlists)
            {
                var playlistInfo = playlist.ToObject<Playlist>();
                if (playlistInfo.name.ToLower() != arg2.ToLower())
                    continue;

                //Playlist songs limit
                if (playlist["songs"].Count() == 100)
                    return await EmbedHandler.CreateErrorEmbed("Music, Playlist", $"You have reached the limit for songs in **{playlistInfo.name}**");

                #region Varibales
                List<JObject> newSongs = new List<JObject>();

                LavaTrack track = null;
                var search = new SearchResponse();
                #endregion

                #region Checks if the song is already in the playlist
                var f = (object)playlist["songs"];

                //If the specified song is a link
                if (Uri.IsWellFormedUriString(arg4, UriKind.Absolute))
                    search = await _lavaNode.SearchAsync(arg4);
                else
                    search = await _lavaNode.SearchYouTubeAsync(arg4);

                //Select the first Track
                track = search.Tracks.FirstOrDefault();

                //Check if the song is already added to the playlist ONLY if there are songs in the playlist
                if (playlist["songs"].HasValues)
                {
                    newSongs = playlist["songs"].ToObject<List<JObject>>();
                    if (playlistInfo.songs.Length > 0)
                    {
                        foreach (JObject song in playlist["songs"])
                        {
                            //Get Song Info
                            var songInfo = song.ToObject<Song>();
                            if (track.Url == songInfo.url || track.Title == songInfo.name)
                            {
                                return await EmbedHandler.CreateErrorEmbed("Music, Playlist", $"**{track.Title}** already exists in this playlist.");
                            }
                        }
                    }
                }
                #endregion

                #region Add song to playlist and save config
                //Remove forbidden chars from the title
                string safeTrackName = track.Title.Replace("\"", "");

                if (safeTrackName.Length == 0)
                    return await EmbedHandler.CreateErrorEmbed("Music, Playlist", "Forbidden Track name!");

                //Adds the new song
                newSongs.Add(JObject.FromObject(new Song() { name = $"{safeTrackName}", url = $"{track.Url}" }));
                playlist["songs"] = JToken.FromObject(newSongs);

                //Saves Config
                _mySQL.UpdateGuildConfig(context.Guild, "playlists", JsonConvert.SerializeObject(JToken.FromObject(playlists), Formatting.None));
                return await EmbedHandler.CreateBasicEmbed("Music, Playlist", $"**{track.Title}** was added to **{playlistInfo.name}**.");
                #endregion
            }

            return await EmbedHandler.CreateErrorEmbed("Music, Playlist", $"The playlist \"{arg2}\" was not found.");
            #endregion
        }

        private async Task<Embed> Playlist_CreatePlaylist(SocketCommandContext context, string arg2)
        {
            #region Code
            //Get Guild Config
            GlobalData.GuildConfigs.TryGetValue(context.Guild.Id, out GuildConfig config);

            //Get Guild Playlists
            JArray playlists = config.playlists;

            List<JObject> newPlaylists = new List<JObject>();
            if (playlists != null)
            {
                foreach (var playlist in playlists)
                {
                    newPlaylists.Add((JObject)playlist);
                }
            }
            

            if (playlists != null)
            {
                #region Playlist limit check
                if (playlists.Count == 100)
                    return await EmbedHandler.CreateErrorEmbed("Music, Playlist", $"You have reached this guilds playlist limit.");
                #endregion

                #region Playlist name check
                foreach (var playlist in playlists)
                {
                    var playlistInfo = playlist.ToObject<Playlist>();
                    if (playlistInfo.name.ToLower() == arg2.ToLower())
                        return await EmbedHandler.CreateErrorEmbed($"Music, Playlist", "Playlist with this name already exists!");
                }
                #endregion
            }

            #region Create playlist & save config
            //Creates new playlist 
            Playlist newPlaylist = new Playlist() { name = arg2, songs = null };

            //Adds playlist to Config
            newPlaylists.Add(JObject.FromObject(newPlaylist));
            

            //Saves Config
            _mySQL.UpdateGuildConfig(context.Guild, "playlists", JsonConvert.SerializeObject(JToken.FromObject(newPlaylists), Formatting.None));
            return await EmbedHandler.CreateBasicEmbed("Music, Playlist", $"{arg2} was created.");
            #endregion
            #endregion
        }

        private async Task<Embed> Playlist_RemovePlaylist(SocketCommandContext context, string arg2)
        {
            #region Code
            //Get Guild Config
            GlobalData.GuildConfigs.TryGetValue(context.Guild.Id, out GuildConfig config);

            //Get Guild Playlists
            JArray playlists = config.playlists;

            List<JObject> newPlaylists = new List<JObject>();
            foreach (var playlist in playlists)
            {
                newPlaylists.Add((JObject)playlist);
            }

            int playlistIndex = 0;
            foreach (var playlist in playlists)
            {
                #region Playlist name check
                var playlistInfo = playlist.ToObject<Playlist>();
                if (playlistInfo.name.ToLower() != arg2.ToLower())
                {
                    playlistIndex++;
                    continue;
                }
                #endregion

                #region Remove playlist & save config
                //Removes playlist from Config
                newPlaylists.RemoveAt(playlistIndex);

                //Saves Config
                _mySQL.UpdateGuildConfig(context.Guild, "playlists", JsonConvert.SerializeObject(JToken.FromObject(newPlaylists), Formatting.None));
                return await EmbedHandler.CreateBasicEmbed("Music, Playlist", $"*{playlistInfo.name}* was removed.");
                #endregion
            }

            return await EmbedHandler.CreateErrorEmbed("Music, Playlist", $"The playlist \"{arg2}\" was not found.");
            #endregion
        }

        #endregion

        #region Shuffle Queue
        public async Task<Embed> ShuffleAsync(SocketCommandContext context)
        {
            var user = context.User as SocketGuildUser;

            Embed sameChannel = await SameChannelAsBot(context.Guild, user, "PlayAsync");
            if (sameChannel != null) //Checks If User is in the same Voice Channel as the bot.
            {
                return sameChannel;
            }

            //Get Guild Player 
            _lavaNode.TryGetPlayer(context.Guild, out LavaPlayer player);

            if (player.Queue.Count <= 1)
                return await EmbedHandler.CreateBasicEmbed("Music, Shuffle", "Not enough songs in queue!");

            //Shuffle Queue
            player.Queue.Shuffle();

            //Display new Queue
            return await GetQueueAsync(context);
        }
        #endregion

        #region TrackEnded
        public async Task TrackEnded(TrackEndedEventArgs args)
        {
            //Check if Shoud Play Next 
            //ShouldPlayNext() false only when TrackEndReason is STOPPED
            if (!args.Reason.ShouldPlayNext())
                return;

            //Get Guild Config
            GlobalData.GuildConfigs.TryGetValue(args.Player.TextChannel.Guild.Id, out GuildConfig config);

            //If looping is enabled
            if (config.isLooping == true)
            {
                //Play same song
                await args.Player.PlayAsync(args.Track);
                return;
            }

            //Get song from Queue
            if (!args.Player.Queue.TryDequeue(out var queueable))
                return;

            if (!(queueable is LavaTrack next))
            {
                await args.Player.TextChannel.SendMessageAsync("Next item in queue is not a track.");
                return;
            }

            //Get endedTrack 
            LavaTrack endedTrack = args.Track;
            LavaTrack track = next;

            if(config.volume != args.Player.Volume)
                await args.Player.UpdateVolumeAsync((ushort)config.volume);

            //Play next Track
            await args.Player.PlayAsync(track);

            LoggingService.Log("TrackEnded", $"Now playing \"{track.Title}\" ({args.Player.VoiceChannel.Guild.Id})");
            await args.Player.TextChannel.SendMessageAsync(
                embed: await EmbedHandler.CreateBasicEmbed("Music, Next Song", $"**[{endedTrack.Title}]({endedTrack.Url})** finished playing.\n" +
                $"Now playing: **[{track.Title}]({track.Url})**"));
        }
        #endregion

        #region Same Channel as Bot
        private async Task<Embed> SameChannelAsBot(IGuild guild, SocketGuildUser user, string src)
        {
            if (!_lavaNode.HasPlayer(guild)) //Checks if the guild has a player available.
                return await EmbedHandler.CreateErrorEmbed(src, "I'm not connected to a voice channel.");

            if (user.VoiceChannel is null)
                return await EmbedHandler.CreateErrorEmbed(src, "You can't use this command because you aren't in a Voice Channel!");

            if (_lavaNode.GetPlayer(guild).VoiceChannel.Id != user.VoiceChannel.Id)
                return await EmbedHandler.CreateErrorEmbed(src, "You can't use this command because you aren't in the same channel as the bot!");
            else
                return null;
        }
        #endregion

        #region Send Message
        private async Task<Discord.Rest.RestUserMessage> SendMessageAsync(Embed embed, SocketCommandContext context)
        {
            return await context.Channel.SendMessageAsync(embed: embed);
        }
        #endregion
    }

    class Playlist
    {
        public string name { get; set; }
        public JObject[] songs { get; set; }
    }

    class Song
    {
        public string name { get; set; }
        public string url { get; set; }
    }
}