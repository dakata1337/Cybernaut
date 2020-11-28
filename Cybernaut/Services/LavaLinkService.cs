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
            #region Config Check
            Embed embed = await ConfigCheck(guild); //checks if the bot is configured
            if (embed != null)
            {
                return embed;
            }
            #endregion

            #region Player Check
            if (_lavaNode.HasPlayer(guild)) //Checks if the bot is already connected
            {
                return await EmbedHandler.CreateErrorEmbed("Music, Join", "I'm already connected to a voice channel!");
            }
            #endregion

            #region User Channel Check
            if (user.VoiceChannel == null) //Checks if the user who sent the command is in a VC
            {
                return await EmbedHandler.CreateErrorEmbed("Music, Join/Play", "You can't use this command because you aren't in a Voice Channel!");
            }
            #endregion
            #endregion

            #region Code
            try
            {
                await _lavaNode.JoinAsync(voiceState.VoiceChannel, textChannel);

                #region On join volume/islooping change
                dynamic json = GetService.GetJSONAsync(guild).ToString();

                var jObj = JsonConvert.DeserializeObject(json);
                jObj["volume"] = JToken.FromObject(70);
                jObj["islooping"] = false;

                string output = JsonConvert.SerializeObject(jObj, Formatting.Indented);
                File.WriteAllText(GetService.GetConfigLocation(guild).ToString(), output, new UTF8Encoding(false));
                #endregion

                await LoggingService.LogInformationAsync("JoinAsync", $"Bot joined {voiceState.VoiceChannel.Name} ({voiceState.VoiceChannel.Guild.Id})");
                return await EmbedHandler.CreateBasicEmbed("Music, Join", $"Joined {voiceState.VoiceChannel.Name}.\n" +
                    $"**WARNING!** - to avoid earrape lower the volume ({jObj["Prefix"]}volume).\n Current volume is {jObj["volume"]}.", Color.Purple);
            }
            catch (Exception ex)
            {
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
                #region Config Check
                Embed embed = await ConfigCheck(context.Guild); //checks if the bot is configured
                if (embed != null)
                {
                    await SendMessage(embed, context);
                    return;
                }
                #endregion

                #region User Channel Check
                if (user.VoiceChannel == null) //Checks if the user who sent the command is in a VC
                {
                    await SendMessage(await EmbedHandler.CreateErrorEmbed("Music, Join/Play", "You can't use this command because you aren't in a Voice Channel!"), context);
                    return;
                }
                #endregion

                #region Player Check
                if (!_lavaNode.HasPlayer(context.Guild)) //Checks if the guild has a player available.
                {
                    await SendMessage(await EmbedHandler.CreateErrorEmbed("Music, Play", "I'm not connected to a voice channel."), context);
                    return;
                }
                #endregion

                #region Same Channel As Bot Check
                Embed sameChannel = await SameChannelAsBot(context.Guild, user, "PlayAsync");
                if (sameChannel != null) //Checks If User is in the same Voice Channel as the bot.
                {
                    await SendMessage(sameChannel, context);
                    return;
                }
                #endregion
                #endregion

                #region Code
                try
                {
                    bool keepRunning = true;

                    #region Player Creation / Query Search
                    //Get the player for that guild.
                    var player = _lavaNode.GetPlayer(context.Guild);

                    LavaTrack track = null;
                    var search = new SearchResponse();

                    //Search for the query
                    if (Uri.IsWellFormedUriString(query, UriKind.Absolute))
                    {
                        search = await _lavaNode.SearchAsync(query);
                        keepRunning = false;
                        track = search.Tracks.FirstOrDefault();
                    }
                    else
                    {
                        search = await _lavaNode.SearchYouTubeAsync(query);
                    }

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

                    #region Update Volume
                    dynamic json = GetService.GetJSONAsync(context.Guild);
                    var jObj = JsonConvert.DeserializeObject(json);

                    bool isLooping = jObj.islooping;
                    string status = isLooping == true ? "looping" : "playing";

                    await player.UpdateVolumeAsync((ushort)jObj.volume);
                    #endregion

                    if (keepRunning)
                    {
                        #region Send available songs list
                        StringBuilder builder = new StringBuilder();
                        builder.Append("**You have 1 minute to select a song**\n");
                        for (int i = 0; i < 5; i++)
                        {
                            builder.Append($"{i + 1}. {search.Tracks[i].Title}\n");
                        }

                        var select = await context.Channel.SendMessageAsync(null, false,
                            await EmbedHandler.CreateBasicEmbed("Music, Select", builder.ToString(), Color.Blue));
                        #endregion

                        #region Reaction Check
                        Emoji[] emojis = GetService.GetNumbersEmojisAndCancel();
                        //await select.AddReactionsAsync(emojis);
                        foreach (var item in emojis)
                        {
                            await select.AddReactionAsync(item);
                        }

                        int count = 0;
                        while (keepRunning)
                        {
                            for (int i = 0; i < emojis.Length && keepRunning; i++)
                            {
                                var reactedUsers = await select.GetReactionUsersAsync(emojis[i], 1).FlattenAsync();
                                foreach (var reactedUser in reactedUsers)
                                {
                                    //If the user who requested the song reacted
                                    if (!reactedUser.IsBot && reactedUser.Id == context.Message.Author.Id)
                                    {
                                        //If the user selected the last emoji (cancel)
                                        if (i == emojis.Length - 1)
                                        {
                                            await context.Channel.SendMessageAsync(null, false, await EmbedHandler.CreateBasicEmbed("Music, Select", $"{user.Username} canceled the selection.", Color.Blue));
                                            keepRunning = false;
                                            return;
                                        }

                                        //Reacted
                                        keepRunning = false;
                                        track = search.Tracks[i];
                                        break;
                                    }
                                }
                            }
                            count++;

                            if (count == 17)
                            {
                                await context.Channel.SendMessageAsync(null, false, await EmbedHandler.CreateBasicEmbed("Time is up!", "You won't be able to select a song.", Color.Blue));
                                return;
                            }
                            Thread.Sleep(1500);
                        }
                        #endregion
                    }

                    #region Final Checks
                    //If the Bot is already playing music, or if it is paused but still has music in the playlist, Add the requested track to the queue.
                    if (player.Track != null && player.PlayerState is PlayerState.Playing || player.PlayerState is PlayerState.Paused)
                    {
                        player.Queue.Enqueue(track);
                        await LoggingService.LogInformationAsync("PlayAsync", $"{track.Title} has been added to the music queue. ({context.Guild.Id})");
                        await SendMessage(await EmbedHandler.CreateBasicEmbed("Music, Play", $"{track.Title} has been added to queue.", Color.Blue), context);
                        return;
                    }
                    #endregion

                    await player.PlayAsync(track);
                    await LoggingService.LogInformationAsync("PlayAsync", $"Bot now {status}: {track.Title} ({context.Guild.Id})");
                    await SendMessage(await EmbedHandler.CreateBasicEmbed("Music, Play", $"Now {status}: [{track.Title}]({track.Url})", Color.Blue), context);
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

        public async Task<Embed> LeaveAsync(IGuild guild, SocketGuildUser user)
        {
            #region Checks

            #region Config Check
            Embed embed = await ConfigCheck(guild); //checks if the bot is configured
            if (embed != null)
            {
                return embed;
            }
            #endregion

            #region Channel Check
            Embed sameChannel = await SameChannelAsBot(guild, user, "LeaveAsync");
            if (sameChannel != null) //Checks If User is in the same Voice Channel as the bot.
            {
                return sameChannel;
            }
            #endregion

            #endregion

            #region Code
            try
            {
                //Get The Player Via GuildID.
                var player = _lavaNode.GetPlayer(guild);

                //if The Player is playing, Stop it.
                if (player.PlayerState is PlayerState.Playing)
                {
                    await player.StopAsync();
                }

                dynamic json = GetService.GetJSONAsync(guild);

                var jObj = JsonConvert.DeserializeObject(json);
                jObj["islooping"] = false;                      //sets islooping to false

                string output = JsonConvert.SerializeObject(jObj, Formatting.Indented);
                File.WriteAllText(GetService.GetConfigLocation(guild).ToString(), output, new UTF8Encoding(false));


                //Leave the voice channel.
                await _lavaNode.LeaveAsync(player.VoiceChannel);
                await LoggingService.LogInformationAsync("LeaveAsync", $"Bot has left. ({guild.Id})");
                return await EmbedHandler.CreateBasicEmbed("LeaveAsync", $"I'm sorry that I gave you up :'(.", Color.Purple);
            }

            //Throws the error in discord
            catch (InvalidOperationException ex)
            {
                return await EmbedHandler.CreateErrorEmbed("LeaveAsync", ex.Message);
            }
            #endregion
        }

        public async Task<Embed> ListAsync(IGuild guild, SocketGuildUser user)
        {
            #region Checks

            #region Config Check
            Embed embed = await ConfigCheck(guild); //checks if the bot is configured
            if (embed != null)
            {
                return embed;
            }
            #endregion

            #region Channel Check
            Embed sameChannel = await SameChannelAsBot(guild, user, "ListAsync");
            if (sameChannel != null) //Checks If User is in the same Voice Channel as the bot.
            {
                return sameChannel;
            }
            #endregion

            #endregion

            #region Code
            try
            {
                /* Create a string builder we can use to format how we want our list to be displayed. */
                var descriptionBuilder = new StringBuilder();

                /* Get The Player and make sure it isn't null. */
                var player = _lavaNode.GetPlayer(guild);
                if (player == null)
                    return await EmbedHandler.CreateErrorEmbed("Music", $"Could not aquire player.\nAre you using the bot right now? ");

                if (player.PlayerState is PlayerState.Playing)
                {
                    dynamic json = GetService.GetJSONAsync(guild);
                    var jObj = JsonConvert.DeserializeObject(json);

                    #region Player Builder
                    StringBuilder playerBuilder = new StringBuilder();
                    double tickLocation = player.Track.Position.TotalSeconds / (player.Track.Duration.TotalSeconds / 20);

                    playerBuilder.Append($"{player.Track.Position.ToString(@"hh\:mm\:ss")} ");

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
                    playerBuilder.Append($" {player.Track.Duration}");
                    #endregion

                    if (player.Queue.Count < 1 && player.Track != null)
                    {
                        if (jObj.islooping == true)
                        {
                            return await EmbedHandler.CreateBasicEmbed($"Music, List", $"**Now Looping: [{player.Track.Title}]({player.Track.Url})\n" +
                                $"{playerBuilder}**", Color.Blue);
                        }
                        return await EmbedHandler.CreateBasicEmbed($"Music, List", $"**Now Playing: [{player.Track.Title}]({player.Track.Url})\n{playerBuilder}**" +
                            $"\nNothing Else Is Queued.", Color.Blue);
                    }
                    else
                    {
                        var trackNum = 2;
                        foreach (LavaTrack track in player.Queue)
                        {
                            descriptionBuilder.Append($"{trackNum}: [{track.Title}]({track.Url})\n");
                            trackNum++;
                        }
                        return await EmbedHandler.CreateBasicEmbed("Music, List", $"**Now Playing: [{player.Track.Title}]({player.Track.Url})\n{playerBuilder}**\n{descriptionBuilder}", Color.Blue);
                    }
                }
                else
                {
                    return await EmbedHandler.CreateErrorEmbed("Music, List", "Player doesn't seem to be playing anything right now.");
                }
            }

            //Throws the error in discord
            catch (Exception ex)
            {
                return await EmbedHandler.CreateErrorEmbed("Music, List", ex.Message.Contains("was not present in the dictionary") ? "You can't use this command because you aren't in the same channel as the bot!" : ex.Message);
            }
            #endregion
        }

        public async Task<Embed> SkipTrackAsync(IGuild guild, SocketGuildUser user)
        {
            #region Checks

            #region Config Check
            Embed embed = await ConfigCheck(guild); //checks if the bot is configured
            if (embed != null)
            {
                return embed;
            }
            #endregion

            #region Channel Check
            Embed sameChannel = await SameChannelAsBot(guild, user, "SkipTrackAsync");
            if (sameChannel != null) //Checks If User is in the same Voice Channel as the bot.
            {
                return sameChannel;
            }
            #endregion

            #endregion

            #region Code
            try
            {
                var player = _lavaNode.GetPlayer(guild);

                if (player == null)
                    return await EmbedHandler.CreateErrorEmbed("Music, Skip", $"Could not aquire player.\nAre you using the bot right now?");

                /* Check The queue, if it is less than one (meaning we only have the current song available to skip) it wont allow the user to skip.
                     User is expected to use the Stop command if they're only wanting to skip the current song. */
                if (player.Queue.Count < 1)
                {
                    return await StopAsync(guild, user);
                }
                else
                {
                    try
                    {
                        /* Save the current song for use after we skip it. */
                        var currentTrack = player.Track;
                        /* Skip the current song. */
                        await player.SkipAsync();
                        await LoggingService.LogInformationAsync("SkipTrackAsync", $"Bot skipped: {currentTrack.Title} ({guild.Id})");
                        return await EmbedHandler.CreateBasicEmbed("Music, Skip", $"I have successfully skiped {currentTrack.Title}.\n**Now playing**: {player.Track.Title}.", Color.Blue);
                    }
                    catch (Exception ex)
                    {
                        return await EmbedHandler.CreateErrorEmbed("Music, Skip", ex.Message);
                    }

                }
            }

            //Throws the error in discord
            catch (Exception ex)
            {
                return await EmbedHandler.CreateErrorEmbed("Music, Skip", ex.Message);
            }
            #endregion
        }

        public async Task<Embed> StopAsync(IGuild guild, SocketGuildUser user)
        {
            #region Checks

            #region Config Check
            Embed embed = await ConfigCheck(guild); //checks if the bot is configured
            if (embed != null)
            {
                return embed;
            }
            #endregion

            #region Channel Check
            Embed sameChannel = await SameChannelAsBot(guild, user, "StopAsync");
            if (sameChannel != null) //Checks If User is in the same Voice Channel as the bot.
            {
                return sameChannel;
            }
            #endregion

            #endregion

            #region Code
            try
            {
                var player = _lavaNode.GetPlayer(guild);

                if (player == null)
                    return await EmbedHandler.CreateErrorEmbed("Music, Stop", $"Could not aquire player.\nAre you using the bot right now?");

                if (player.PlayerState is PlayerState.Playing)
                    await player.StopAsync();

                await LoggingService.LogInformationAsync("StopAsync", $"Bot has stopped playback. ({guild.Id})");
                return await EmbedHandler.CreateBasicEmbed("Music, Stop", "I Have stopped playback & the playlist has been cleared.", Color.Blue);
            }

            //Throws the error in discord
            catch (Exception ex)
            {
                return await EmbedHandler.CreateErrorEmbed("Music, Stop", ex.Message);
            }
            #endregion
        }

        public async Task<Embed> SetVolumeAsync(IGuild guild, int? volume, SocketGuildUser user, ITextChannel textChannel)
        {
            #region Checks

            #region Config Check
            Embed embed = await ConfigCheck(guild); //checks if the bot is configured
            if (embed != null)
            {
                return embed;
            }
            #endregion

            #region Channel Check
            Embed sameChannel = await SameChannelAsBot(guild, user, "SetVolumeAsync");
            if (sameChannel != null) //Checks If User is in the same Voice Channel as the bot.
            {
                return sameChannel;
            }
            #endregion

            #region Is Null Check
            dynamic json = GetService.GetJSONAsync(guild);
            var jObj = JsonConvert.DeserializeObject(json);

            if (volume is null)
                return await EmbedHandler.CreateBasicEmbed("Music, Volume", $"Volume is set to {jObj["volume"]}", Color.Blue);
            #endregion

            #endregion

            #region Code
            if (volume > 150 || volume <= 0) //Checks if the volume is the range 1-150
                return await EmbedHandler.CreateBasicEmbed("Music, Volume", $"Volume must be between 1 and 150.", Color.Blue);

            try 
            {
                jObj["volume"] = volume; //changes the volume 

                string output = JsonConvert.SerializeObject(jObj, Formatting.Indented);         //saves the config
                File.WriteAllText(GetService.GetConfigLocation(guild).ToString(), output, new UTF8Encoding(false));

                var player = _lavaNode.GetPlayer(guild);
                await player.UpdateVolumeAsync((ushort)jObj.volume);
                await LoggingService.LogInformationAsync("SetVolumeAsync", $"Bot Volume set to: {jObj.volume} ({guild.Id})");
                return await EmbedHandler.CreateBasicEmbed("Music, Volume", $"Volume has been set to {jObj.volume}.", Color.Blue);
            }

            //Throws the error in discord
            catch (InvalidOperationException ex)
            {
                return await EmbedHandler.CreateErrorEmbed("Music, Volume", ex.Message);
            }
            #endregion
        }

        public async Task<Embed> PauseAsync(IGuild guild, SocketGuildUser user)
        {
            #region Checks

            #region Config Check
            Embed embed = await ConfigCheck(guild); //checks if the bot is configured
            if (embed != null)
            {
                return embed;
            }
            #endregion

            #region Channel Check
            Embed sameChannel = await SameChannelAsBot(guild, user, "PauseAsync");
            if (sameChannel !=null) //Checks If User is in the same Voice Channel as the bot.
            {
                return sameChannel;
            }
            #endregion

            #endregion

            #region Code
            try
            {
                var player = _lavaNode.GetPlayer(guild);
                if (!(player.PlayerState is PlayerState.Playing))
                {
                    await player.PauseAsync();
                    return await EmbedHandler.CreateBasicEmbed("Music, Pause", $"There is nothing to pause.", Color.Blue);

                }

                await player.PauseAsync();
                await LoggingService.LogInformationAsync("PauseAsync", $"Paused: {player.Track.Title} - {player.Track.Author} ({guild.Id})");
                return await EmbedHandler.CreateBasicEmbed("Music, Pause", $"**Paused:** {player.Track.Title} - {player.Track.Author}.", Color.Blue);
            }

            //Throws the error in discord
            catch (InvalidOperationException ex)
            {
                return await EmbedHandler.CreateErrorEmbed("Music, Pause", ex.Message);
            }
            #endregion
        }

        public async Task<Embed> ResumeAsync(IGuild guild, SocketGuildUser user)
        {
            #region Checks

            #region Config Check
            Embed embed = await ConfigCheck(guild); //checks if the bot is configured
            if (embed != null)
            {
                return embed;
            }
            #endregion

            #region Channel Check
            Embed sameChannel = await SameChannelAsBot(guild, user, "ResumeAsync");
            if (sameChannel != null) //Checks If User is in the same Voice Channel as the bot.
            {
                return sameChannel;
            }
            #endregion

            #endregion

            #region Code
            try
            {
                var player = _lavaNode.GetPlayer(guild);

                if (player.PlayerState is PlayerState.Paused)
                {
                    await player.ResumeAsync();
                }
                await LoggingService.LogInformationAsync("ResumeAsync", $"Resumed: {player.Track.Title} - {player.Track.Author} ({guild.Id})");
                return await EmbedHandler.CreateBasicEmbed("Music, Resume", $"**Resumed:** {player.Track.Title} - {player.Track.Author}.", Color.Blue);
            }

            //Throws the error in discord
            catch (InvalidOperationException ex)
            {
                return await EmbedHandler.CreateErrorEmbed("Music, Resume", ex.Message);
            }
            #endregion
        }

        public async Task<Embed> LoopAsync(IGuild guild, ITextChannel textChannel, SocketGuildUser user, string arg)
        {
            #region Checks

            #region Config Check
            Embed embed = await ConfigCheck(guild); //checks if the bot is configured
            if (embed != null)
            {
                return embed;
            }
            #endregion

            #region Argument check
            dynamic json = GetService.GetJSONAsync(guild);
            var jObj = JsonConvert.DeserializeObject(json);

            if (arg != null)
            {
                switch (arg.ToLower().TrimEnd(' '))
                {
                    case "status":
                        bool looping = jObj.islooping;
                        if (looping)
                            return await EmbedHandler.CreateBasicEmbed("Loop, Status", $"Looping is enabled.", Color.Blue);
                        else
                            return await EmbedHandler.CreateBasicEmbed("Loop, Status", $"Looping is disabled.", Color.Blue);
                    default:
                        return await EmbedHandler.CreateErrorEmbed("Loop, Status", $"{arg} is not a valid argument."); ;
                }
            }
            #endregion

            #region Channel Check
            Embed sameChannel = await SameChannelAsBot(guild, user, "LoopAsync");
            if (sameChannel != null) //Checks If User is in the same Voice Channel as the bot.
            {
                return sameChannel;
            }
            #endregion

            #endregion

            #region Code
            if (jObj.islooping == true)
            {
                jObj.islooping = false;
                string output = JsonConvert.SerializeObject(jObj, Formatting.Indented);
                File.WriteAllText(GetService.GetConfigLocation(guild).ToString(), output, new UTF8Encoding(false));

                await LoggingService.LogInformationAsync("LoopAsync", $"Looping is now disabled. ({guild.Id})");
                return await EmbedHandler.CreateBasicEmbed("Looping Disabled", $"Looping is now disabled!", Color.Blue);
            }
            else
            {
                jObj.islooping = true;
                string output = JsonConvert.SerializeObject(jObj, Formatting.Indented);
                File.WriteAllText(GetService.GetConfigLocation(guild).ToString(), output, new UTF8Encoding(false));

                await LoggingService.LogInformationAsync("LoopAsync", $"Looping is now enabled. ({guild.Id})");
                return await EmbedHandler.CreateBasicEmbed("Looping Enabled", $"Looping is now enabled!", Color.Blue);
            }
            #endregion
        }

        public async Task<Embed> GetLyricsAsync(SocketCommandContext context)
        {
            #region Checks

            #region Config Check
            Embed embed = await ConfigCheck(context.Guild); //checks if the bot is configured
            if (embed != null)
            {
                return embed;
            }
            #endregion

            #region Channel Check
            Embed sameChannel = await SameChannelAsBot(context.Guild, (SocketGuildUser)context.Message.Author, "GetLyricsAsync");
            if (sameChannel != null) //Checks If User is in the same Voice Channel as the bot.
            {
                return sameChannel;
            }
            #endregion

            #region Player Check
            var player = _lavaNode.GetPlayer(context.Guild);
            if (player.PlayerState != PlayerState.Playing)
                return await EmbedHandler.CreateErrorEmbed("Music, Lyrics", "The bot is currently not playing music.");
            #endregion

            #endregion

            #region Code
            await context.Message.Author.GetOrCreateDMChannelAsync();

            string lyrics = await player.Track.FetchLyricsFromGeniusAsync();

            if (lyrics == string.Empty)
                lyrics = await player.Track.FetchLyricsFromOVHAsync();
            else if (lyrics == string.Empty)
                return await EmbedHandler.CreateErrorEmbed("Music, Lyrics", "Sorry we couldn't find the lyrics you requested.");

            string file = $"lyrics-{DateTime.UtcNow.ToString("HH-mm-ss_dd-MM-yyyy")}.txt";

            //Create File and Save lyrics
            File.Create(file).Dispose();
            File.WriteAllText(file, lyrics);

            //Send File
            if(new System.IO.FileInfo(file).Length == 0)
            {
                File.Delete(file);
                return await EmbedHandler.CreateErrorEmbed("Music, Lyrics", "Sorry we couldn't find the lyrics you requested.");
            }

            await context.Message.Author.SendFileAsync(file, $"We found lyrics for the song: {player.Track.Title}");

            //Delete File
            File.Delete(file);
            return await EmbedHandler.CreateBasicEmbed("Music, Lyrics", $"{context.Message.Author.Mention} Please check your DMs for the lyrics you requested.", Color.Blue);
            #endregion
        }

        public async Task TrackEnded(TrackEndedEventArgs args)
        {
            #region Code
            LavaTrack track = null;

            dynamic json = GetService.GetJSONAsync(args.Player.VoiceChannel.Guild);
            var jObj = JsonConvert.DeserializeObject(json);

            if (!args.Reason.ShouldPlayNext())
                return;

            if (jObj.islooping == true)
            {
                track = args.Track;
            }
            else
            {
                if (!args.Player.Queue.TryDequeue(out var queueable))
                    return;

                if (!(queueable is LavaTrack next))
                {
                    await args.Player.TextChannel.SendMessageAsync("Next item in queue is not a track.");
                    return;
                }
                track = next;
            }

            await args.Player.PlayAsync(track);

            if (!jObj.islooping)                                                                                                                                                                                                      /* display the Discord servers name but whatever i guess */
            {
                await LoggingService.LogInformationAsync("TrackEnded", $"Now playing {track.Title} - {track.Author} ({args.Player.VoiceChannel.Guild.Id})");
                await args.Player.TextChannel.SendMessageAsync(
                embed: await EmbedHandler.CreateBasicEmbed("Music, Next Song", $"Now playing: {track.Title}\nUrl: {track.Url}", Color.Blue));
            }
            #endregion
        }

        private async Task<Embed> SameChannelAsBot(IGuild guild, SocketGuildUser user, string src)
        {
            #region Checks

            #region Player Check
            if (!_lavaNode.HasPlayer(guild))
                return await EmbedHandler.CreateErrorEmbed(src, "I'm not connected to a voice channel.");
            #endregion

            #region User Channel Check
            if (user.VoiceChannel is null)
                return await EmbedHandler.CreateErrorEmbed(src, "You can't use this command because you aren't in a Voice Channel!"); ;
            #endregion

            #endregion

            #region Code
            if (_lavaNode.GetPlayer(guild).VoiceChannel.Id != user.VoiceChannel.Id)
                return await EmbedHandler.CreateErrorEmbed(src, "You can't use this command because you aren't in the same channel as the bot!");
            else
                return null;
            #endregion
        }

        private async Task<Embed> ConfigCheck(IGuild guild)
        {
            #region Code
            string configFile = GetService.GetConfigLocation(guild).ToString();
            if (!File.Exists(configFile))
            {
                return await EmbedHandler.CreateBasicEmbed("Configuration needed!", $"Please type `{GlobalData.Config.DefaultPrefix}prefix YourPrefixHere` to configure the bot.", Color.Orange);
            }
            return null;
            #endregion
        }

        private Task SendMessage(Embed embed = null, SocketCommandContext context = null)
        {
            #region Code
            if (!(embed is null && context is null))
            {
                context.Channel.SendMessageAsync(null, false, embed);
            }

            return Task.CompletedTask;
            #endregion
        }
    }
}
