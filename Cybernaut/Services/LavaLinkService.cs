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

        public async Task<Embed> JoinAsync(IGuild guild, IVoiceState voiceState, ITextChannel textChannel)
        {
            Embed embed = await ConfigCheck(guild); //checks if the bot is configured
            if (embed != null)
            {
                return embed;
            }
            /*===============================================================================================*/
            #region Command
            if (_lavaNode.HasPlayer(guild)) //Checks if the bot is already connected
            {
                return await EmbedHandler.CreateErrorEmbed("Music, Join", "I'm already connected to a voice channel!");
            }
            if (voiceState.VoiceChannel is null) //Checks if the user who sent the command is in a VC
            {
                return await EmbedHandler.CreateErrorEmbed("Music, Join", "You can't use this command because you aren't in a Voice Channel!");
            }

            try //finally i'm kinda liking the code now...
            {
                await _lavaNode.JoinAsync(voiceState.VoiceChannel, textChannel);

                //Oh my God. I can't be bothered to fix this terrible mess... atleast it works lol 
                //int f = 0;
                //GlobalData.connected.TryGetValue(textChannel.Id.ToString(), out f);
                //GlobalData.BotChannelsVoice[f] = voiceState.VoiceChannel.Id.ToString(); //saves voice channel id in array
                //GlobalData.BotChannelsText[f] = textChannel.Id.ToString();              //saves text channel id in array
                //GlobalData.BotVolume[f] = 70;                                           //saves volume in array

                #region On join volume/islooping change
                dynamic json = GetJSONAsync(guild);

                var jObj = JsonConvert.DeserializeObject(json);
                jObj["volume"] = JToken.FromObject(70);
                jObj["islooping"] = false;
                jObj["voiceChannelID"] = voiceState.VoiceChannel.Id;

                string output = JsonConvert.SerializeObject(jObj, Formatting.Indented);
                File.WriteAllText(GetConfigLocation(guild), output, new UTF8Encoding(false));
                #endregion
                await LoggingService.LogInformationAsync("JoinAsync", $"Bot joined {voiceState.VoiceChannel.Name} ({voiceState.VoiceChannel.Guild.Id})");
                return await EmbedHandler.CreateBasicEmbed("Music, Join", $"Joined {voiceState.VoiceChannel.Name}.\n**WARNING!** - to avoid earrape lower the volume ({jObj["Prefix"]}volume).\n Current volume is {jObj["volume"]}.", Color.Purple);
            }
            catch (Exception ex)
            {
                return await EmbedHandler.CreateErrorEmbed("Music, Join", ex.Message);
            }
            #endregion
        }

        public async Task<Embed> PlayAsync(SocketGuildUser user, IGuild guild, string query, IVoiceState voiceState, ITextChannel textChannel)
        {
            Embed embed = await ConfigCheck(guild); //checks if the bot is configured
            if (embed != null)
            {
                return embed;
            }
            /*===============================================================================================*/
            #region Command
            if (user.VoiceChannel == null) //Checks if the user who sent the command is in a VC
            {
                return await EmbedHandler.CreateErrorEmbed("Music, Join/Play", "You can't use this command because you aren't in a Voice Channel!");
            }

            if (!InSameChannel(guild,user)) //Checks If User is in the same Voice Channel as the bot.
            {
                return await EmbedHandler.CreateErrorEmbed("Music, Join/Play", "You can't use this command because you aren't in the same Voice Channel as the bot!");
            }


            if (!_lavaNode.HasPlayer(guild)) //Checks if the guild has a player available.
            {
                //int f = 0;
                //GlobalData.connected.TryGetValue(textChannel.Id.ToString(), out f);
                //GlobalData.BotChannelsVoice[f] = voiceState.VoiceChannel.Id.ToString(); //saves voice channel id in array
                //GlobalData.BotChannelsText[f] = textChannel.Id.ToString();              //saves text channel id in array
                //GlobalData.BotVolume[f] = 80;                                           //saves volume in array
                return await EmbedHandler.CreateErrorEmbed("Music, Play", "I'm not connected to a voice channel.");
            }

            try
            {
                //Get the player for that guild.
                var player = _lavaNode.GetPlayer(guild);

                LavaTrack track;
                var search = await _lavaNode.SearchYouTubeAsync(query);

                /*
                var search = Uri.IsWellFormedUriString(query, UriKind.Absolute) ?
                    await _lavaNode.SearchAsync(query)
                    : await _lavaNode.SearchYouTubeAsync(query);
                */

                //If we couldn't find anything, tell the user.
                if (search.LoadStatus == LoadStatus.NoMatches)
                {
                    return await EmbedHandler.CreateErrorEmbed("Music, Play", $"I wasn't able to find anything for {query}.");
                }

                #region Update Volume
                //Old way
                //for (int i = 0; i < GlobalData.Config.whitelistedChannels.Count; i++) //Checks if !volume was used and if yes changes the volume of the bot
                //{
                //    if (textChannel.Id.ToString() == GlobalData.BotChannelsText[i])
                //    {
                //        await player.UpdateVolumeAsync((ushort)GlobalData.BotVolume[i]);
                //        break;
                //    }
                //}

                //New way
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

                    //GlobalData.connected.TryGetValue(textChannel.Id.ToString(), out int index);
                    //await LoggingService.LogInformationAsync(GlobalData.isLooping[index] == true ? "Music Play (looping)" : "Music Play", $"Bot Now Playing: {track.Title}\nUrl: {track.Url}");
                    //return await EmbedHandler.CreateBasicEmbed(GlobalData.isLooping[index] == true ? "Music Play (looping)" : "Music Play", $"Now Playing: {track.Title}\nUrl: {track.Url}", Color.Blue);

                    bool isLooping = jObj.islooping;

                    await LoggingService.LogInformationAsync(isLooping == true ? "PlayAsync (looping)" : "PlayAsync", $"Bot Now Playing: {track.Title} ({guild.Id})");
                    return await EmbedHandler.CreateBasicEmbed(isLooping == true ? "Music Play (looping)" : "Music Play", $"Now Playing: {track.Title}\nUrl: {track.Url}", Color.Blue);
                }
                catch (Exception e) { return await EmbedHandler.CreateErrorEmbed("Music, Play", e.Message); }
            }

            //Throws the error in discord
            catch (Exception ex)
            {
                return await EmbedHandler.CreateErrorEmbed("Music, Play", ex.Message.Contains("was not present in the dictionary.") ? $"You can't use this command because you aren't in the same channel as the bot." : ex.Message);
            }
            #endregion
        }

        public async Task<Embed> LeaveAsync(IGuild guild, SocketGuildUser user)
        {
            Embed embed = await ConfigCheck(guild); //checks if the bot is configured
            if (embed != null)
            {
                return embed;
            }
            /*===============================================================================================*/
            #region Command
            if (!InSameChannel(guild, user)) //Checks If User is in the same Voice Channel as the bot.
            {
                return await EmbedHandler.CreateErrorEmbed("Music Leave", $"You can't use this command because you aren't in the same channel as the bot");
            }

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
                jObj["voiceChannelID"] = JToken.FromObject(0);  //sets the vc id to 0
                jObj["islooping"] = false;                      //sets islooping to false

                string output = JsonConvert.SerializeObject(jObj, Formatting.Indented);
                File.WriteAllText(GetConfigLocation(guild), output, new UTF8Encoding(false));


                //Leave the voice channel.
                await _lavaNode.LeaveAsync(player.VoiceChannel);

                await LoggingService.LogInformationAsync("Music, Leave", $"Bot has left. ({guild.Name})");
                return await EmbedHandler.CreateBasicEmbed("Music, Leave", $"I'm sorry that i gave you up :'(.", Color.Purple);
            }

            //Throws the error in discord
            catch (InvalidOperationException ex)
            {
                return await EmbedHandler.CreateErrorEmbed("Music, Leave", ex.Message);
            }
            #endregion
        }

        public async Task<Embed> ListAsync(IGuild guild)
        {
            Embed embed = await ConfigCheck(guild); //checks if the bot is configured
            if (embed != null)
            {
                return embed;
            }
            /*===============================================================================================*/
            #region Command
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
                    /*If the queue count is less than 1 and the current track IS NOT null then we wont have a list to reply with.
                        In this situation we simply return an embed that displays the current track instead. */
                    if (player.Queue.Count < 1 && player.Track != null)
                    {
                        return await EmbedHandler.CreateBasicEmbed($"Now Playing: {player.Track.Title}", "Nothing Else Is Queued.", Color.Blue);
                    }
                    else
                    {
                        /* Now we know if we have something in the queue worth replying with, so we itterate through all the Tracks in the queue.
                         *  Next Add the Track title and the url however make use of Discords Markdown feature to display everything neatly.
                            This trackNum variable is used to display the number in which the song is in place. (Start at 2 because we're including the current song.*/
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
                return await EmbedHandler.CreateErrorEmbed("Music, List", ex.Message);
            }
            #endregion
        }

        public async Task<Embed> SkipTrackAsync(IGuild guild, SocketGuildUser user)
        {
            Embed embed = await ConfigCheck(guild); //checks if the bot is configured
            if (embed != null)
            {
                return embed;
            }
            /*===============================================================================================*/
            #region Command
            if (!InSameChannel(guild, user)) //Checks If User is in the same Voice Channel as the bot.
            {
                return await EmbedHandler.CreateErrorEmbed("Music, Skip", $"You can't use this command because you aren't in the same channel as the bot");
            }

            try
            {
                var player = _lavaNode.GetPlayer(guild);
                /* Check if the player exists */
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
            Embed embed = await ConfigCheck(guild); //checks if the bot is configured
            if (embed != null)
            {
                return embed;
            }
            /*===============================================================================================*/
            #region Command
            if (!InSameChannel(guild, user)) //Checks If User is in the same Voice Channel as the bot.
            {
                return await EmbedHandler.CreateErrorEmbed("Music, Stop", $"You can't use this command because you aren't in the same channel as the bot");
            }
            try
            {
                var player = _lavaNode.GetPlayer(guild);

                if (player == null)
                    return await EmbedHandler.CreateErrorEmbed("Music, Stop", $"Could not aquire player.\nAre you using the bot right now?");

                /* Check if the player exists, if it does, check if it is playing.
                     If it is playing, we can stop.*/
                if (player.PlayerState is PlayerState.Playing)
                {
                    await player.StopAsync();
                }

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
            Embed embed = await ConfigCheck(guild); //checks if the bot is configured
            if (embed != null)
            {
                return embed;
            }
            /*===============================================================================================*/
            #region Command
            if (!InSameChannel(guild, user)) //Checks If User is in the same Voice Channel as the bot.
            {
                return await EmbedHandler.CreateErrorEmbed("Music, Volume", $"You can't use this command because you aren't in the same channel as the bot");
            }

            if (volume > 150 || volume <= 0) //Checks if the volume is the range 1-150
            {
                return await EmbedHandler.CreateBasicEmbed("Music, Volume", $"Volume must be between 1 and 150.", Color.Blue);
            }
            try //Saves the volume
            {
                //int i = 0;
                //for (i = 0; i < GlobalData.Config.whitelistedChannels.Count; i++)
                //{
                //    if (textChannel.Id.ToString() == GlobalData.BotChannelsText[i])
                //    {
                //        GlobalData.BotVolume[i] = volume;
                //        break;
                //    }
                //}

                dynamic json = GetJSONAsync(guild);
                var jObj = JsonConvert.DeserializeObject(json);

                jObj["volume"] = volume;//changes the volume 

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
                return await EmbedHandler.CreateErrorEmbed("Music, Volume", ex.Message.ToString());
            }
            #endregion
        }

        public async Task<Embed> PauseAsync(IGuild guild, SocketGuildUser user)
        {
            Embed embed = await ConfigCheck(guild); //checks if the bot is configured
            if (embed != null)
            {
                return embed;
            }
            /*===============================================================================================*/
            #region Command
            if (!InSameChannel(guild, user)) //Checks If User is in the same Voice Channel as the bot.
            {
                return await EmbedHandler.CreateErrorEmbed("Music, Pause", $"You can't use this command because you aren't in the same channel as the bot");
            }

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
                return await EmbedHandler.CreateErrorEmbed("Music, Pause", ex.Message.ToString());
            }
            #endregion
        }

        public async Task<Embed> ResumeAsync(IGuild guild, SocketGuildUser user)
        {
            Embed embed = await ConfigCheck(guild); //checks if the bot is configured
            if (embed != null)
            {
                return embed;
            }
            /*===============================================================================================*/
            #region Command
            if (!InSameChannel(guild, user)) //Checks If User is in the same Voice Channel as the bot.
            {
                return await EmbedHandler.CreateErrorEmbed("Music, Resume", $"You can't use this command because you aren't in the same channel as the bot");
            }

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
                return await EmbedHandler.CreateErrorEmbed("Music, Resume", ex.Message.ToString());
            }
            #endregion
        }

        public async Task<Embed> LoopAsync(IGuild guild, ITextChannel textChannel, SocketGuildUser user, string arg)
        {
            Embed embed = await ConfigCheck(guild); //checks if the bot is configured
            if (embed != null)
            {
                return embed;
            }
            /*===============================================================================================*/
            #region Command

            dynamic json = GetJSONAsync(guild);
            var jObj = JsonConvert.DeserializeObject(json);

            if (arg != null)
            {
                switch (arg.ToLower().TrimEnd(' '))
                {
                    case "status":
                        bool looping = jObj.islooping;
                        if (looping)
                            return await EmbedHandler.CreateBasicEmbed("Looping Status", $"Looping is enabled.", Color.Blue);
                        else
                            return await EmbedHandler.CreateBasicEmbed("Looping Status", $"Looping is disabled.", Color.Blue);
                    default:
                        return await EmbedHandler.CreateBasicEmbed("Looping Status", $"{arg} is not a valid argument.", Color.Blue); ;
                }
            }

            if (!InSameChannel(guild, user)) //Checks If User is in the same Voice Channel as the bot.
            {
                return await EmbedHandler.CreateErrorEmbed("Looping", $"You can't use this command because you aren't in the same channel as the bot");
            }

            #region old
            //GlobalData.connected.TryGetValue(textChannel.Id.ToString(), out int index);
            //if (GlobalData.isLooping[index] == true)
            //{
            //    GlobalData.isLooping[index] = false;
            //    await LoggingService.LogToFile("LoopAsync", $"Looping is now disabled! ({guild.Name})");
            //    return await EmbedHandler.CreateBasicEmbed("Looping Disabled", $"Looping is now disabled!", Color.Blue);
            //}
            //else
            //{
            //    GlobalData.isLooping[index] = true;
            //    await LoggingService.LogToFile("LoopAsync", $"Looping is now enabled! ({guild.Name})");
            //    return await EmbedHandler.CreateBasicEmbed("Looping Enabled", $"Looping is now enabled!", Color.Blue);
            //}
            #endregion

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
            #region Command
            LavaTrack track = null;

            if (!args.Reason.ShouldPlayNext())
            {
                return;
            }

            #region Old
            //int i = 0;
            //for (i = 0; i < GlobalData.Config.whitelistedChannels.Count; i++)
            //{
            //    if (args.Player.TextChannel.Id.ToString() == GlobalData.BotChannelsText[i])
            //    {
            //        if (GlobalData.isLooping[i] == true)
            //        {
            //            track = args.Track;
            //            break;
            //        }
            //        else
            //        {
            //            if (!args.Player.Queue.TryDequeue(out var queueable))
            //            {
            //                //await args.Player.TextChannel.SendMessageAsync("Playback Finished.");
            //                return;
            //            }

            //            if (!(queueable is LavaTrack next))
            //            {
            //                await args.Player.TextChannel.SendMessageAsync("Next item in queue is not a track.");
            //                return;
            //            }
            //            track = next;
            //        }
            //    }

            #endregion

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
                    //await args.Player.TextChannel.SendMessageAsync("Playback Finished.");
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

        public bool InSameChannel(IGuild guild, SocketGuildUser user)
        {
            #region Old way
            //How did this even worked?
            //bool status = true;
            //for (int i = 0; i < GlobalData.Config.whitelistedChannels.Count; i++)
            //{
            //    if (user.VoiceChannel.Id.ToString() == GlobalData.BotChannelsVoice[i])
            //    {
            //        status = true;
            //        break;
            //    }
            //    else
            //    {
            //        status = false;
            //    }
            //}
            //return status;
            #endregion 

            if (user.VoiceChannel is null)
                return false;

            dynamic json = GetJSONAsync(guild);
            var jObj = JsonConvert.DeserializeObject(json);

            if (jObj.voiceChannelID != user.VoiceChannel.Id)
                return false;
            else
                return true;
        }

        private string GetJSONAsync(IGuild guild)
        {
            string configFile = GetConfigLocation(guild);
            var json = File.ReadAllText(configFile);
            return json;
        }

        private string GetConfigLocation(IGuild guild)
        {
            return $@"{GlobalData.Config.ConfigLocation}\{guild.Id}.json";
        }

        private async Task<Embed> ConfigCheck(IGuild guild)
        {
            string configFile = GetConfigLocation(guild);
            if (!File.Exists(configFile))
            {
                return await EmbedHandler.CreateBasicEmbed("Configuration needed!", $"Please type `{GlobalData.Config.DefaultPrefix}prefix YourPrefixHere` to configure the bot.", Color.Orange);
            }
            return null;
        }

    }
}
