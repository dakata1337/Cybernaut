using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Cybernaut.DataStructs;
using Cybernaut.Handlers;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Victoria;
using Victoria.Enums;
using Victoria.EventArgs;

namespace Cybernaut.Services
{
    public sealed class LavaLinkService
    {
        private readonly LavaNode _lavaNode;

        public LavaLinkService(LavaNode lavaNode)
            => _lavaNode = lavaNode;

        private string GetConfigLocation(IGuild guild)
        {
            #region Code
            return $@"{GlobalData.Config.ConfigLocation}\{guild.Id}.json";
            #endregion
        }
                                                                                                                                                                                                                                         /*
         [================Actual Code================]
                                                                                                                                                                                                                                         */
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

            #endregion

            #region Code
            try
            {
                await _lavaNode.JoinAsync(voiceState.VoiceChannel, textChannel);

                #region On join volume/islooping change
                dynamic json = GetJSONAsync(guild);

                var jObj = JsonConvert.DeserializeObject(json);
                jObj["volume"] = JToken.FromObject(70);
                jObj["islooping"] = false;

                string output = JsonConvert.SerializeObject(jObj, Formatting.Indented);
                File.WriteAllText(GetConfigLocation(guild), output, new UTF8Encoding(false));
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

        public async Task<Embed> PlayAsync(SocketGuildUser user, IGuild guild, string query, IVoiceState voiceState, ITextChannel textChannel)
        {
            #region Checks
            #region Config Check
            Embed embed = await ConfigCheck(guild); //checks if the bot is configured
            if (embed != null)
            {
                return embed;
            }
            #endregion

            #region User Channel Check
            if (user.VoiceChannel == null) //Checks if the user who sent the command is in a VC
            {
                return await EmbedHandler.CreateErrorEmbed("Music, Join/Play", "You can't use this command because you aren't in a Voice Channel!");
            }
            #endregion

            #region Channel Check
            Embed sameChannel = await SameChannelAsBot(guild, user, "PlayAsync");
            if (sameChannel != null) //Checks If User is in the same Voice Channel as the bot.
            {
                return sameChannel;
            }
            #endregion

            #region Player Check
            if (!_lavaNode.HasPlayer(guild)) //Checks if the guild has a player available.
            {
                return await EmbedHandler.CreateErrorEmbed("Music, Play", "I'm not connected to a voice channel.");
            }
            #endregion
            #endregion

            #region Code
            try
            {
                //Get the player for that guild.
                var player = _lavaNode.GetPlayer(guild);

                LavaTrack track;
                var search = await _lavaNode.SearchYouTubeAsync(query);
                
                //If we couldn't find anything, tell the user.
                if (search.LoadStatus == LoadStatus.NoMatches)
                {
                    return await EmbedHandler.CreateErrorEmbed("Music, Play", $"I wasn't able to find anything for {query}.");
                }

                #region Update Volume
                dynamic json = GetJSONAsync(guild);
                var jObj = JsonConvert.DeserializeObject(json);

                await player.UpdateVolumeAsync((ushort)jObj.volume);
                #endregion

                //Get the first track from the search results.
                track = search.Tracks.FirstOrDefault();

                //If the Bot is already playing music, or if it is paused but still has music in the playlist, Add the requested track to the queue.
                if (player.Track != null && player.PlayerState is PlayerState.Playing || player.PlayerState is PlayerState.Paused)
                {
                    player.Queue.Enqueue(track);
                    await LoggingService.LogInformationAsync("PlayAsync", $"{track.Title} has been added to the music queue. ({guild.Id})");
                    return await EmbedHandler.CreateBasicEmbed("Music, Play", $"{track.Title} has been added to queue.", Color.Blue);
                }

                //Player was not playing anything, so lets play the requested track.
                //NOTE: I need to use some type of database which contains Rick Roll links
                try
                {
                    if (track.Title.Contains("Rick Astley") || track.Title.Contains("Never gonna") || track.Title.Contains("Cute Little Puppy Doing Cute things"))
                    {
                        await player.PlayAsync(track);
                        return await EmbedHandler.SendImage("https://media.giphy.com/media/Vuw9m5wXviFIQ/giphy.gif", $"Everyone who is in the channel {voiceState.VoiceChannel.Name} got rick rolled.");
                    }

                    await player.PlayAsync(track);

                    bool isLooping = jObj.islooping;

                    string status = isLooping == true ? "looping" : "playing";
                    await LoggingService.LogInformationAsync("PlayAsync", $"Bot now {status}: {track.Title} ({guild.Id})");
                    return await EmbedHandler.CreateBasicEmbed("Music, Play", $"Now {status}: {track.Title}\nUrl: {track.Url}", Color.Blue);
                }
                catch (Exception ex) { return await EmbedHandler.CreateErrorEmbed("Music, Play", ex.Message); }
            }

            //Throws the error in discord
            catch (Exception ex)
            {
                return await EmbedHandler.CreateErrorEmbed("Music, Play", ex.Message);
            }
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

                //removes the vc id from the config !VERY IMPORTANT!
                dynamic json = GetJSONAsync(guild);

                var jObj = JsonConvert.DeserializeObject(json);
                jObj["islooping"] = false;                      //sets islooping to false

                string output = JsonConvert.SerializeObject(jObj, Formatting.Indented);
                File.WriteAllText(GetConfigLocation(guild), output, new UTF8Encoding(false));


                //Leave the voice channel.
                await _lavaNode.LeaveAsync(player.VoiceChannel);

                await LoggingService.LogInformationAsync("LeaveAsync", $"Bot has left. ({guild.Name})");
                return await EmbedHandler.CreateBasicEmbed("LeaveAsync", $"I'm sorry that i gave you up :'(.", Color.Purple);
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
                    if (player.Queue.Count < 1 && player.Track != null)
                    {
                        dynamic json = GetJSONAsync(guild);
                        var jObj = JsonConvert.DeserializeObject(json);

                        if (jObj.islooping == true)
                        {
                            StringBuilder stringBuilder = new StringBuilder();

                            double duration = player.Track.Duration.TotalSeconds;
                            double position = player.Track.Position.TotalSeconds;

                            double tickDuration = duration / 20;
                            double tickLocation = position / tickDuration;

                            int x = (int)Math.Round(tickLocation, 0, MidpointRounding.AwayFromZero);
                            for (int i = 0; i < 20; i++)
                            {
                                if (i == x)
                                {
                                    stringBuilder.Append("🔘");
                                    continue;
                                }
                                stringBuilder.Append("▬");
                            }
                            return await EmbedHandler.CreateBasicEmbed($"Music, List", $"Now Looping: {player.Track.Title}\n" +
                                $"**{player.Track.Position.ToString(@"hh\:mm\:ss")} " +
                                $"{stringBuilder} " +
                                $"{player.Track.Duration}**", Color.Blue);
                        }

                        return await EmbedHandler.CreateBasicEmbed($"Now Playing: {player.Track.Title}", "Nothing Else Is Queued.", Color.Blue);
                    }
                    else
                    {
                        var trackNum = 2;
                        foreach (LavaTrack track in player.Queue)
                        {
                            descriptionBuilder.Append($"{trackNum}: [{track.Title}]({track.Url}) - {track.Id}\n");
                            trackNum++;
                        }
                        return await EmbedHandler.CreateBasicEmbed("Music, List", $"Now Playing: [{player.Track.Title}]({player.Track.Url}) \n{descriptionBuilder}", Color.Blue);
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

                await LoggingService.LogInformationAsync("StopAsync", $"Bot has stopped playback. ({guild.Name})");
                return await EmbedHandler.CreateBasicEmbed("Music, Stop", "I Have stopped playback & the playlist has been cleared.", Color.Blue);
            }

            //Throws the error in discord
            catch (Exception ex)
            {
                return await EmbedHandler.CreateErrorEmbed("Music, Stop", ex.Message);
            }
            #endregion
        }

        public async Task<Embed> SetVolumeAsync(IGuild guild, int volume, SocketGuildUser user, ITextChannel textChannel)
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

            #endregion

            #region Code
            if (volume > 150 || volume <= 0) //Checks if the volume is the range 1-150
            {
                return await EmbedHandler.CreateBasicEmbed("Music, Volume", $"Volume must be between 1 and 150.", Color.Blue);
            }
            try 
            {
                dynamic json = GetJSONAsync(guild);
                var jObj = JsonConvert.DeserializeObject(json);

                jObj["volume"] = volume; //changes the volume 

                string output = JsonConvert.SerializeObject(jObj, Formatting.Indented);         //saves the config
                File.WriteAllText(GetConfigLocation(guild), output, new UTF8Encoding(false));

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
            dynamic json = GetJSONAsync(guild);
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
                File.WriteAllText(GetConfigLocation(guild), output, new UTF8Encoding(false));

                await LoggingService.LogInformationAsync("LoopAsync", $"Looping is now disabled. ({guild.Id})");
                return await EmbedHandler.CreateBasicEmbed("Looping Disabled", $"Looping is now disabled!", Color.Blue);
            }
            else
            {
                jObj.islooping = true;
                string output = JsonConvert.SerializeObject(jObj, Formatting.Indented);
                File.WriteAllText(GetConfigLocation(guild), output, new UTF8Encoding(false));

                await LoggingService.LogInformationAsync("LoopAsync", $"Looping is now enabled. ({guild.Id})");
                return await EmbedHandler.CreateBasicEmbed("Looping Enabled", $"Looping is now enabled!", Color.Blue);
            }
            #endregion
        }

        public async Task TrackEnded(TrackEndedEventArgs args)
        {
            #region Code
            LavaTrack track = null;

            if (!args.Reason.ShouldPlayNext())
            {
                return;
            }

            dynamic json = GetJSONAsync(args.Player.VoiceChannel.Guild);
            var jObj = JsonConvert.DeserializeObject(json);

            if (jObj.islooping == true)
            {
                track = args.Track;
            }
            else
            {
                if (!args.Player.Queue.TryDequeue(out var queueable))
                {
                    return;
                }

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

            #endregion

            #region Code
            var player = _lavaNode.GetPlayer(context.Guild);
            await context.Message.Author.GetOrCreateDMChannelAsync();
            var LyricsFromGenius = await player.Track.FetchLyricsFromGeniusAsync();
            var LyricsFromOVH = await player.Track.FetchLyricsFromOVHAsync();


            if (LyricsFromGenius != string.Empty)
                await context.Message.Author.SendMessageAsync($"We found lyrics for the song: {player.Track.Title}\n========\n" + LyricsFromGenius);
            else if (LyricsFromOVH != string.Empty)
                await context.Message.Author.SendMessageAsync($"We found lyrics for the song: {player.Track.Title}\n========\n" + LyricsFromOVH);
            else
                return await EmbedHandler.CreateErrorEmbed("Music, Lyrics", "Sorry we couldn't find the lyrics you requested.");

            return await EmbedHandler.CreateBasicEmbed("Music, Lyrics", "Please check your DMs for the lyrics you requested.", Color.Blue);
            #endregion
        }

        private async Task<Embed> SameChannelAsBot(IGuild guild, SocketGuildUser user, string src)
        {
            #region Checks

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
            string configFile = GetConfigLocation(guild);
            if (!File.Exists(configFile))
            {
                return await EmbedHandler.CreateBasicEmbed("Configuration needed!", $"Please type `{GlobalData.Config.DefaultPrefix}prefix YourPrefixHere` to configure the bot.", Color.Orange);
            }
            return null;
            #endregion
        }

        private string GetJSONAsync(IGuild guild)
        {
            #region Code
            string configFile = GetConfigLocation(guild);
            var json = File.ReadAllText(configFile);
            return json;
            #endregion
        }
    }
}
