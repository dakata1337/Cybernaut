using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Permissions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Cybernaut.DataStructs;
using Cybernaut.Handlers;
using Cybernaut.Modules;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Victoria;
using Victoria.Enums;
using Victoria.EventArgs;
using Victoria.Responses.Rest;

namespace Cybernaut.Services
{
    public sealed class LavaLinkService
    {
        private readonly LavaNode _lavaNode;
        public LavaLinkService(LavaNode lavaNode)
            => _lavaNode = lavaNode;

        public async Task<Embed> JoinAsync(IGuild guild, IVoiceState voiceState, ITextChannel textChannel, SocketGuildUser user)
        {
            #region Checks

            #region Player Check
            //Checks if the bot is already connected
            if (_lavaNode.HasPlayer(guild)) 
            {
                return await EmbedHandler.CreateErrorEmbed("Music, Join", "I'm already connected to a voice channel!");
            }
            #endregion

            #region User VC Check
            if (user.VoiceChannel is null)
                return await EmbedHandler.CreateErrorEmbed("Music, Join", "You can't use this command because you aren't in a Voice Channel!");
            #endregion

            #endregion

            #region Code
            try
            {
                //The Bot joins the channel
                await _lavaNode.JoinAsync(voiceState.VoiceChannel, textChannel);

                #region On join volume/islooping change
                //Get Guild Config
                var jObj = GetService.GetJObject(guild);

                //Set config volume to 100 (default)
                jObj["volume"] = JToken.FromObject(100);
                //Set islooping to false (default)
                jObj["islooping"] = false;

                //Convert JObject to string
                string output = JsonConvert.SerializeObject(jObj, Formatting.Indented);

                //Save config
                File.WriteAllText(GetService.GetConfigLocation(guild), output, new UTF8Encoding(false));
                #endregion

                //Log information to Console & Discord
                await LoggingService.LogInformationAsync("JoinAsync", $"Bot joined \"{voiceState.VoiceChannel.Name}\" ({voiceState.VoiceChannel.Guild.Id})");
                return await EmbedHandler.CreateBasicEmbed("Music, Join", $"Joined {voiceState.VoiceChannel.Name}.\n" +
                    $"**WARNING!** - to avoid earrape lower the volume ({jObj["Prefix"]}volume).\n Current volume is {jObj["volume"]}.", Color.Purple);
            }
            catch (Exception ex)
            {
                //Log information to Discord
                return await EmbedHandler.CreateErrorEmbed("Music, Join", ex.Message);
            }
            #endregion
        }

        public Task Play(SocketGuildUser user, SocketCommandContext context, string query, IVoiceState voiceState)
        {
            #region Code
            var thread = new Thread(async () =>
            {
                #region Checks

                #region Same Channel As Bot Check
                Embed sameChannel = await SameChannelAsBot(context.Guild, user, "PlayAsync");
                //Checks If User is in the same Voice Channel as the bot.
                if (sameChannel != null) 
                {
                    await SendMessage(sameChannel, context);
                    return;
                }
                #endregion

                #endregion

                #region Code
                try
                {
                    #region Player Creation / Query Search
                    //Get the player for that guild.
                    var player = _lavaNode.GetPlayer(context.Guild);

                    LavaTrack track = null;
                    var search = new SearchResponse();

                    //Search for the query
                    if (Uri.IsWellFormedUriString(query, UriKind.Absolute))
                    {
                        #region If the Query is a link 
                        search = await _lavaNode.SearchAsync(query);

                        #region LoadFailed/NoMatches Check
                        //If load failed, tell the user
                        if (search.LoadStatus == LoadStatus.LoadFailed)
                        {
                            await SendMessage(await EmbedHandler.CreateErrorEmbed("Music, Play", $"Failed to load {query}.\n{search.Exception.Message}"), context);
                            return;
                        }

                        //If we couldn't find anything, tell the user.
                        if (search.LoadStatus == LoadStatus.NoMatches)
                        {
                            await SendMessage(await EmbedHandler.CreateErrorEmbed("Music, Play", $"I wasn't able to find anything for {query}."), context);
                            return;
                        }
                        #endregion

                        track = search.Tracks.FirstOrDefault();
                        #endregion
                    }
                    else
                    {
                        #region If the Query isn't a link
                        search = await _lavaNode.SearchYouTubeAsync(query);

                        #region LoadFailed/NoMatches Check
                        //If load failed, tell the user
                        if (search.LoadStatus == LoadStatus.LoadFailed)
                        {
                            await SendMessage(await EmbedHandler.CreateErrorEmbed("Music, Play", $"Failed to load {query}.\n{search.Exception.Message}"), context);
                            return;
                        }

                        //If we couldn't find anything, tell the user.
                        if (search.LoadStatus == LoadStatus.NoMatches)
                        {
                            await SendMessage(await EmbedHandler.CreateErrorEmbed("Music, Play", $"I wasn't able to find anything for {query}."), context);
                            return;
                        }
                        #endregion

                        #region Send available songs list
                        //Show the first 5 songs from the search
                        StringBuilder builder = new StringBuilder();
                        builder.Append("**You have 1 minute to select a song**\n");
                        for (int i = 0; i < 5; i++)
                        {
                            builder.Append($"{i + 1}. {search.Tracks[i].Title}\n");
                        }

                        //Send the message to the channel
                        var select = await context.Channel.SendMessageAsync(null, false,
                            await EmbedHandler.CreateBasicEmbed("Music, Select", builder.ToString()));
                        #endregion

                        #region Reaction Check
                        //All Emotes
                        var emotes = new[]
                        {
                            new Emoji("1️⃣"),
                            new Emoji("2️⃣"),
                            new Emoji("3️⃣"),
                            new Emoji("4️⃣"),
                            new Emoji("5️⃣"),
                            new Emoji("🚫")
                        };

                        //React to the comment
                        await select.AddReactionsAsync(emotes);

                        //Set endtime one minute from running the command
                        var endTime = DateTime.UtcNow.AddMinutes(1);

                        bool stopOuter = true;
                        while (stopOuter)
                        {
                            //Goes through all emotes
                            for (int i = 0; i < emotes.Length; i++)
                            {
                                if (!stopOuter)
                                    break;

                                //Gets all reacted users for the current emote
                                var reactedUsers = await select.GetReactionUsersAsync(emotes[i], 1).FlattenAsync();
                                foreach (var reactedUser in reactedUsers)
                                {
                                    //If the user who requested the song reacted
                                    if (!reactedUser.IsBot && reactedUser.Id == context.Message.Author.Id)
                                    {
                                        //If the user selected the last emote (cancel)
                                        if (i == emotes.Length - 1)
                                        {
                                            await context.Channel.SendMessageAsync(null, false, await EmbedHandler.CreateBasicEmbed("Music, Select", $"{user.Username} cancelled the selection."));
                                            return;
                                        }

                                        //This is used to break out of all loops
                                        stopOuter = false;

                                        //Set the track to the selected one
                                        track = search.Tracks[i];
                                        break;
                                    }
                                }
                            }
                            //This will be true if one minute has passed
                            if (endTime < DateTime.UtcNow)
                            {
                                await context.Channel.SendMessageAsync(null, false, await EmbedHandler.CreateBasicEmbed("Time is up!", "You won't be able to select a song."));
                                return;
                            }

                            //Wait .5s before next Scan
                            Thread.Sleep(500);
                        }
                        #endregion

                        #endregion
                    }

                    #endregion

                    #region Update Volume
                    //Get Guild Config
                    var jObj = GetService.GetJObject(context.Guild);

                    //Get islooping Bool
                    bool isLooping = (bool)jObj["islooping"];
                    string status = isLooping == true ? "looping" : "playing";

                    //Update Player Volume
                    await player.UpdateVolumeAsync((ushort)jObj["volume"]);
                    #endregion

                    #region Plays / Adds song to queue
                    //If the Bot is already playing music, or if it is paused but still has music in the playlist, Add the requested track to the queue.
                    if (player.Track != null && player.PlayerState is PlayerState.Playing || player.PlayerState is PlayerState.Paused)
                    {
                        player.Queue.Enqueue(track);
                        await LoggingService.LogInformationAsync("PlayAsync", $"{track.Title} has been added to the music queue. ({context.Guild.Id})");
                        await SendMessage(await EmbedHandler.CreateBasicEmbed("Music, Play", $"{track.Title} has been added to queue."), context);
                        return;
                    }

                    //Play the track
                    await player.PlayAsync(track);

                    //Log information to Console & Discord
                    await LoggingService.LogInformationAsync("PlayAsync", $"Bot now {status}: {track.Title} ({context.Guild.Id})");
                    await SendMessage(await EmbedHandler.CreateBasicEmbed("Music, Play", $"Now {status}: [{track.Title}]({track.Url})"), context);
                    return;
                    #endregion
                }
                catch (Exception ex) //Throws the error in discord
                {
                    await SendMessage(await EmbedHandler.CreateErrorEmbed("Music, Play", ex.Message), context);
                }
                #endregion
            });

            thread.IsBackground = true;
            thread.Start();
            return Task.CompletedTask;
            #endregion
        }

        public async Task<Embed> LeaveAsync(SocketCommandContext context, SocketGuildUser user)
        {
            #region Checks

            #region Channel Check
            Embed sameChannel = await SameChannelAsBot(context.Guild, user, "LeaveAsync");
            if (sameChannel != null) //Checks If User is in the same Voice Channel as the bot.
            {
                return sameChannel;
            }
            #endregion

            #endregion

            #region Code
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
                await LoggingService.LogInformationAsync("LeaveAsync", $"Bot has left \"{vc}\". ({context.Guild.Id})");
                return await EmbedHandler.CreateBasicEmbed("LeaveAsync", $"I'm sorry that I gave you up :'(.", Color.Purple);
            }

            //Throws the error in discord
            catch (InvalidOperationException ex)
            {
                return await EmbedHandler.CreateErrorEmbed("LeaveAsync", ex.Message);
            }
            #endregion
        }

        public async Task<Embed> ListAsync(SocketCommandContext context, SocketGuildUser user)
        {
            #region Checks

            #region Channel Check
            //Checks If User is in the same Voice Channel as the bot.
            Embed sameChannel = await SameChannelAsBot(context.Guild, user, "ListAsync");
            if (sameChannel != null) 
            {
                return sameChannel;
            }
            #endregion

            #endregion

            #region Code
            try
            {
                //Create a string builder we can use to format how we want our list to be displayed.
                var descriptionBuilder = new StringBuilder();

                //Get the Player and make sure it isn't null.
                var player = _lavaNode.GetPlayer(context.Guild);
                if (player == null)
                    return await EmbedHandler.CreateErrorEmbed("Music", $"Could not aquire player.\nAre you using the bot right now? ");

                //If the player is not playing anything notify the user
                if (player.PlayerState != PlayerState.Playing)
                    return await EmbedHandler.CreateErrorEmbed("Music, List", "Player doesn't seem to be playing anything right now.");

                //Get Guild Config
                var jObj = GetService.GetJObject(context.Guild);

                #region Player Builder
                StringBuilder playerBuilder = new StringBuilder();
                double tickLocation = player.Track.Position.TotalSeconds / (player.Track.Duration.TotalSeconds / 20);

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
                #endregion

                //If the queue is empty
                if (player.Queue.Count < 1 && player.Track != null)
                {
                    //Log information to Discord
                    return await EmbedHandler.CreateBasicEmbed($"Music, List", 
                        (bool)jObj["islooping"] ? "**Now Looping: " : "**Now Playing: " + 
                        $"[{player.Track.Title}]({player.Track.Url})\n{playerBuilder}**" +
                        $"\nNothing else is queued.");
                }

                //If the queue isnt empty
                else
                {
                    var trackNum = 2;
                    //Foreach track in queue
                    foreach (LavaTrack track in player.Queue)
                    {
                        //Add to the description builder
                        descriptionBuilder.Append($"{trackNum}: [{track.Title}]({track.Url})\n");

                        //Increment next track number
                        trackNum++;
                    }

                    //Log information to Discord
                    return await EmbedHandler.CreateBasicEmbed("Music, List", 
                        (bool)jObj["islooping"] ? "**Now Looping: " : "**Now Playing: " + 
                        $"[{player.Track.Title}]({player.Track.Url})\n{playerBuilder}**" +
                        $"\n{descriptionBuilder}");
                }
            }

            //Throws the error in discord
            catch (Exception ex)
            {
                //Log information to Discord
                return await EmbedHandler.CreateErrorEmbed("Music, List", ex.Message);
            }
            #endregion
        }

        public async Task<Embed> SkipTrackAsync(SocketCommandContext context, SocketGuildUser user)
        {
            #region Checks

            #region Channel Check
            //Checks If User is in the same Voice Channel as the bot.
            Embed sameChannel = await SameChannelAsBot(context.Guild, user, "SkipTrackAsync");
            if (sameChannel != null) 
            {
                return sameChannel;
            }
            #endregion

            #endregion

            #region Code
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
                    return await StopAsync(context, user);
                }
                else
                {
                    //Save the current song for use after we skip it
                    var currentTrack = player.Track;

                    //Skip the current song.
                    await player.SkipAsync();

                    //Log information to Console & Discord
                    await LoggingService.LogInformationAsync("SkipTrackAsync", $"Bot skipped: {currentTrack.Title} ({context.Guild.Id})");
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
            #endregion
        }

        public async Task<Embed> StopAsync(SocketCommandContext context, SocketGuildUser user)
        {
            #region Checks

            #region Channel Check
            //Checks If User is in the same Voice Channel as the bot.
            Embed sameChannel = await SameChannelAsBot(context.Guild, user, "StopAsync");
            if (sameChannel != null) 
            {
                return sameChannel;
            }
            #endregion

            #endregion

            #region Code
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
                    await LoggingService.LogInformationAsync("StopAsync", $"Bot has stopped playback. ({context.Guild.Id})");
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
            #endregion
        }

        public async Task<Embed> SetVolumeAsync(SocketCommandContext context, int? volume, SocketGuildUser user, ITextChannel textChannel)
        {
            #region Checks

            #region Channel Check
            //Checks If User is in the same Voice Channel as the bot.
            Embed sameChannel = await SameChannelAsBot(context.Guild, user, "SetVolumeAsync");
            if (sameChannel != null) 
            {
                return sameChannel;
            }
            #endregion

            #region If volume is null
            //Get Guild Config
            var jObj = GetService.GetJObject(context.Guild);

            //If volume is not specified display current volume
            if (volume is null)
                return await EmbedHandler.CreateBasicEmbed("Music, Volume", $"Volume is set to {jObj["volume"]}");
            #endregion

            #endregion

            #region Code
            //Checks if the volume is the range 1-150
            if (volume > 150 || volume <= 0) 
                return await EmbedHandler.CreateBasicEmbed("Music, Volume", $"Volume must be between 1 and 150.");

            try 
            {
                //Changes the volume 
                jObj["volume"] = volume;

                //Saves the config 
                string output = JsonConvert.SerializeObject(jObj, Formatting.Indented);         
                File.WriteAllText(GetService.GetConfigLocation(context.Guild).ToString(), output, new UTF8Encoding(false));

                //Updates player volume
                var player = _lavaNode.GetPlayer(context.Guild);
                await player.UpdateVolumeAsync((ushort)jObj["volume"]);

                //Log information to Console & Discord
                await LoggingService.LogInformationAsync("SetVolumeAsync", $"Bot Volume set to: {jObj["volume"]} ({context.Guild.Id})");
                return await EmbedHandler.CreateBasicEmbed("Music, Volume", $"Volume has been set to {(string)jObj["volume"]}.");
            }

            //Throws the error in discord
            catch (InvalidOperationException ex)
            {
                return await EmbedHandler.CreateErrorEmbed("Music, Volume", ex.Message);
            }
            #endregion
        }

        public async Task<Embed> PauseAsync(SocketCommandContext context, SocketGuildUser user)
        {
            #region Checks

            #region Channel Check
            //Checks If User is in the same Voice Channel as the bot.
            Embed sameChannel = await SameChannelAsBot(context.Guild, user, "PauseAsync");
            if (sameChannel !=null) 
            {
                return sameChannel;
            }
            #endregion

            #endregion

            #region Code
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
                await LoggingService.LogInformationAsync("PauseAsync", $"Paused: {player.Track.Title} - {player.Track.Author} ({context.Guild.Id})");
                return await EmbedHandler.CreateBasicEmbed("Music, Pause", $"**Paused:** {player.Track.Title} - {player.Track.Author}.");
            }

            //Throws the error in discord
            catch (InvalidOperationException ex)
            {
                return await EmbedHandler.CreateErrorEmbed("Music, Pause", ex.Message);
            }
            #endregion
        }

        public async Task<Embed> ResumeAsync(SocketCommandContext context, SocketGuildUser user)
        {
            #region Checks

            #region Channel Check
            //Checks If User is in the same Voice Channel as the bot.
            Embed sameChannel = await SameChannelAsBot(context.Guild, user, "ResumeAsync");
            if (sameChannel != null)
            {
                return sameChannel;
            }
            #endregion

            #endregion

            #region Code
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
                await LoggingService.LogInformationAsync("ResumeAsync", $"Resumed: {player.Track.Title} - {player.Track.Author} ({context.Guild.Id})");
                return await EmbedHandler.CreateBasicEmbed("Music, Resume", $"**Resumed:** {player.Track.Title} - {player.Track.Author}.");
            }

            //Throws the error in discord
            catch (InvalidOperationException ex)
            {
                return await EmbedHandler.CreateErrorEmbed("Music, Resume", ex.Message);
            }
            #endregion
        }

        public async Task<Embed> LoopAsync(SocketCommandContext context, ITextChannel textChannel, SocketGuildUser user, string arg)
        {
            #region Checks

            #region Argument check
            //Get Guild Config
            var jObj = GetService.GetJObject(context.Guild);

            //If an argument is given 
            if (arg != null)
            {
                //Check if argument is "status"
                if(arg.ToLower().TrimEnd(' ') == "status")
                {
                    //Display looping status 
                    return await EmbedHandler.CreateBasicEmbed("Loop, Status", $"Looping is {((bool)jObj["islooping"] ? "enabled" : "disabled")}.");
                }
                else
                    return await EmbedHandler.CreateErrorEmbed("Loop, Status", $"{arg} is not a valid argument.");
            }
            #endregion

            #region Channel Check
            //Checks If User is in the same Voice Channel as the bot.
            Embed sameChannel = await SameChannelAsBot(context.Guild, user, "LoopAsync");
            if (sameChannel != null) 
            {
                return sameChannel;
            }
            #endregion

            #endregion

            #region Code
            //Change isLooping to opposite value
            jObj["islooping"] = !(bool)jObj["islooping"];

            //Save Config
            string output = JsonConvert.SerializeObject(jObj, Formatting.Indented);
            File.WriteAllText(GetService.GetConfigLocation(context.Guild).ToString(), output, new UTF8Encoding(false));

            //Log information to Console & Discord
            await LoggingService.LogInformationAsync("LoopAsync", $"Looping is now {((bool)jObj["islooping"] ? "enabled" : "disabled")}. ({context.Guild.Id})");
            return await EmbedHandler.CreateBasicEmbed("Looping Enabled", $"Looping is now {((bool)jObj["islooping"] ? "enabled" : "disabled")}.");
            #endregion
        }

        public async Task<Embed> GetLyricsAsync(SocketCommandContext context)
        {
            #region Checks

            #region Channel Check
            //Checks If User is in the same Voice Channel as the bot.
            Embed sameChannel = await SameChannelAsBot(context.Guild, (SocketGuildUser)context.Message.Author, "GetLyricsAsync");
            if (sameChannel != null) 
            {
                return sameChannel;
            }
            #endregion

            #region Player Check
            //Get Guild Player
            var player = _lavaNode.GetPlayer(context.Guild);

            //If the player isn't playing return
            if (player.PlayerState != PlayerState.Playing)
                return await EmbedHandler.CreateErrorEmbed("Music, Lyrics", "The bot is currently not playing music.");
            #endregion

            #endregion

            #region Code
            //Get Context
            var dmChannel = (IDMChannel)context.Message.Author.GetOrCreateDMChannelAsync();

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
            if(new System.IO.FileInfo(file).Length == 0)
            {
                File.Delete(file);
                return await EmbedHandler.CreateErrorEmbed("Music, Lyrics", "Sorry we couldn't find the lyrics you requested.");
            }

            //Send File
            await dmChannel.SendFileAsync(file, $"We found lyrics for the song: {player.Track.Title}");

            //Delete File
            File.Delete(file);
            return await EmbedHandler.CreateBasicEmbed("Music, Lyrics", $"{context.Message.Author.Mention} Please check your DMs for the lyrics you requested.");
            #endregion
        }

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
            switch (arg1)
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
            var jObj = GetService.GetJObject(context.Guild);

            //Get Guild Playlists
            JObject[] playlists = jObj["playlists"].ToObject<JObject[]>();
            #endregion

            #region Code
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
                foreach (JObject playlist in playlists)
                {
                    if (!(playlist is null))
                    {
                        //Get Playlist Info
                        var playlistInfo = playlist.ToObject<Playlist>();


                        //If arg2 (playlist name) is null show all playlists 
                        if (arg2 is null) 
                        {
                            //Shows only playlist name
                            builder.Append($"{playlistCount}. {playlistInfo.name} (Songs: {playlistInfo.songs.Length})\n");
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
            if(found)
                return await EmbedHandler.CreateBasicEmbed("Music, Playlist", $"{builder}");
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
            var jObj = GetService.GetJObject(context.Guild);
            JObject[] playlists = jObj["playlists"].ToObject<JObject[]>();
            #endregion

            #region Code
            bool found = false;
            if (!(playlists is null))
            {
                foreach (JObject playlist in playlists)
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

                        var player = _lavaNode.GetPlayer(context.Guild);
                        foreach (JObject song in playlist["songs"])
                        {
                            //Get song Info
                            var songInfo = song.ToObject<Song>();
                            LavaTrack track = null;

                            //Search for the song
                            var search = await _lavaNode.SearchAsync(songInfo.url);

                            if (search.LoadStatus.Equals(LoadStatus.LoadFailed))
                                return await EmbedHandler.CreateErrorEmbed("Music, Playlist", $"Failed to load [{songInfo.name}]({songInfo.url}).");
                            
                            if (search.LoadStatus.Equals(LoadStatus.NoMatches))
                                return await EmbedHandler.CreateErrorEmbed("Music, Playlist", $"[{songInfo.name}]({songInfo.url}) was not found.");

                            //Get First track found
                            track = search.Tracks.FirstOrDefault();

                            //If the Bot is already playing music, or if it is paused but still has music in the playlist, Add the requested track to the queue.
                            if (player.Track != null && player.PlayerState is PlayerState.Playing || player.PlayerState is PlayerState.Paused)
                            {
                                player.Queue.Enqueue(track);

                                //Continue to the next track
                                continue;
                            }

                            //Play track
                            await player.PlayAsync(track);

                            //Send Message to Discord
                            await context.Channel.SendMessageAsync(embed: 
                                await EmbedHandler.CreateBasicEmbed("Music, Play", $"Now " + ((bool)jObj["islooping"] == true ? "looping" : "playing") + 
                                $": [{track.Title}]({track.Url})"));
                            Thread.Sleep(50);
                        }
                    }
                    //If the playlist is already found there is not point of
                    //looking the next playlist
                    break;
                }
            }

            if (found)
            {
                await LoggingService.LogInformationAsync("Playlist", $"{arg2} was loaded successfully.");
                return await EmbedHandler.CreateBasicEmbed("Music, Playlist", $"{arg2} was loaded.");
            }
            else
                return await EmbedHandler.CreateBasicEmbed("Music, Playlist", $"{arg2} was not found.\n" +
                                $"Use ```{jObj["Prefix"]}playlist show``` to see all playlist.");
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
            var jObj = GetService.GetJObject(context.Guild);

            bool playlistFound = false;
            bool songFound = false;
            #endregion

            #region Remove Song From Playlist
            foreach (var playlist in jObj["playlists"])
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
                    File.WriteAllText(GetService.GetConfigLocation(context.Guild),
                            JsonConvert.SerializeObject(jObj, Formatting.Indented));

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
            var jObj = GetService.GetJObject(context.Guild);

            //Playlists loop
            foreach (var playlist in jObj["playlists"])
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
                //Adds the new song
                newSongs.Add(JObject.FromObject(new Song() { name = $"{track.Title}", url = $"{track.Url}"}));
                playlist["songs"] = JToken.FromObject(newSongs);

                //Saves Config
                File.WriteAllText(GetService.GetConfigLocation(context.Guild),
                        JsonConvert.SerializeObject(jObj, Formatting.Indented));
                return await EmbedHandler.CreateBasicEmbed("Music, Playlist", $"**{track.Title}** was added to **{playlistInfo.name}**.");
                #endregion
            }

            return await EmbedHandler.CreateErrorEmbed("Music, Playlist", $"The playlist \"{arg2}\" was not found.");
            #endregion
        }

        private async Task<Embed> Playlist_CreatePlaylist(SocketCommandContext context, string arg2)
        {
            #region Code
            var jObj = GetService.GetJObject(context.Guild);
            List<JObject> newPlaylists = new List<JObject>(jObj["playlists"].ToObject<List<JObject>>());

            #region Playlist limit check
            if (jObj["playlists"].Count() == 100)
                return await EmbedHandler.CreateErrorEmbed("Music, Playlist", $"You have reached this guilds playlist limit.");
            #endregion

            #region Playlist name check
            foreach (var playlist in jObj["playlists"])
            {
                var playlistInfo = playlist.ToObject<Playlist>();
                if (playlistInfo.name.ToLower() == arg2.ToLower())
                    return await EmbedHandler.CreateErrorEmbed($"Music, Playlist", "Playlist with this name already exists!");
            }
            #endregion

            #region Create playlist & save config
            //Creates new playlist 
            Playlist newPlaylist = new Playlist() { name = arg2, songs = null };

            //Adds playlist to Config
            newPlaylists.Add(JObject.FromObject(newPlaylist));
            jObj["playlists"] = JToken.FromObject(newPlaylists);

            //Saves Config
            File.WriteAllText(GetService.GetConfigLocation(context.Guild),
                    JsonConvert.SerializeObject(jObj, Formatting.Indented));
            return await EmbedHandler.CreateBasicEmbed("Music, Playlist", $"{arg2} was created.");
            #endregion
            #endregion
        }

        private async Task<Embed> Playlist_RemovePlaylist(SocketCommandContext context, string arg2)
        {
            #region Code
            var jObj = GetService.GetJObject(context.Guild);
            List<JObject> newPlaylists = new List<JObject>(jObj["playlists"].ToObject<List<JObject>>());

            int playlistIndex = 0;
            foreach (var playlist in jObj["playlists"])
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
                jObj["playlists"] = JToken.FromObject(newPlaylists);

                //Saves Config
                File.WriteAllText(GetService.GetConfigLocation(context.Guild),
                        JsonConvert.SerializeObject(jObj, Formatting.Indented));
                return await EmbedHandler.CreateBasicEmbed("Music, Playlist", $"{playlistInfo.name} was removed.");
                #endregion
            }

            return await EmbedHandler.CreateErrorEmbed("Music, Playlist", $"The playlist \"{arg2}\" was not found.");
            #endregion
        }

        public async Task<Embed> ShuffleAsync(SocketCommandContext context)
        {
            #region Checks
            var user = context.User as SocketGuildUser;

            #region Same Channel As Bot Check
            Embed sameChannel = await SameChannelAsBot(context.Guild, user, "PlayAsync");
            if (sameChannel != null) //Checks If User is in the same Voice Channel as the bot.
            {
                return sameChannel;
            }
            #endregion

            #endregion

            #region Code
            //Get Guild Player 
            _lavaNode.TryGetPlayer(context.Guild, out LavaPlayer player);

            //Shuffle Queue
            player.Queue.Shuffle();

            //Display new Queue
            return await ListAsync(context, user);
            #endregion
        }

        public async Task TrackEnded(TrackEndedEventArgs args)
        {
            #region Code

            //Check if Shoud Play Next 
            //ShouldPlayNext() false only when TrackEndReason is STOPPED
            if (!args.Reason.ShouldPlayNext()) 
                return;

            //Get Guild Config
            var jObj = GetService.GetJObject(args.Player.VoiceChannel.Guild);

            //If looping is enabled
            if ((bool)jObj["islooping"] == true)
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

            //Play next Track
            await args.Player.PlayAsync(track);

            await LoggingService.LogInformationAsync("TrackEnded", $"Now playing {track.Title} ({args.Player.VoiceChannel.Guild.Id})");
            await args.Player.TextChannel.SendMessageAsync(
                embed: await EmbedHandler.CreateBasicEmbed("Music, Next Song", $"**[{endedTrack.Title}]({endedTrack.Url})** finished.\n" +
                $"Now playing: **[{track.Title}]({track.Url})**"));
            #endregion
        }

        private async Task<Embed> SameChannelAsBot(IGuild guild, SocketGuildUser user, string src)
        {
            #region Checks

            if (!_lavaNode.HasPlayer(guild)) //Checks if the guild has a player available.
                return await EmbedHandler.CreateErrorEmbed(src, "I'm not connected to a voice channel.");

            if (user.VoiceChannel is null)
                return await EmbedHandler.CreateErrorEmbed(src, "You can't use this command because you aren't in a Voice Channel!");

            if (_lavaNode.GetPlayer(guild).VoiceChannel.Id != user.VoiceChannel.Id)
                return await EmbedHandler.CreateErrorEmbed(src, "You can't use this command because you aren't in the same channel as the bot!");
            else
                return null;
            #endregion
        }

        private Task SendMessage(Embed embed = null, SocketCommandContext context = null)
        {
            #region Code
            if (!(embed is null && context is null))
            {
                context.Channel.SendMessageAsync(embed: embed);
            }

            return Task.CompletedTask;
            #endregion
        }
    }
}
