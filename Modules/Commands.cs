using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Discord_Bot.DataStrucs;
using Discord_Bot.Handlers;
using Microsoft.Extensions.DependencyInjection;
using MySql.Data.MySqlClient;

namespace Discord_Bot.Modules
{
    public class Commands : ModuleBase<SocketCommandContext>
    {
        private CommandService _commandService;
        private MySqlConnection _connection;
        private MySQL _mySQL;
        private Music _music;
        private GuildConfigHandler _guildConfigHandler;
        private HelpModule _helpModule;
        public Commands(IServiceProvider serviceProvider)
        {
            _commandService = serviceProvider.GetRequiredService<CommandService>();
            _mySQL = serviceProvider.GetRequiredService<MySQL>();
            _connection = _mySQL.connection;
            _music = serviceProvider.GetRequiredService<Music>();
            _guildConfigHandler = serviceProvider.GetRequiredService<GuildConfigHandler>();
            _helpModule = serviceProvider.GetRequiredService<HelpModule>();
        }

        #region Help Commands
        [Command("Help")]
        [Summary("Shows all commands.")]
        public async Task Help()
            => await Context.User.SendMessageAsync(embed: await _helpModule.Help(Context, _commandService));

        [Command("About")]
        [Summary("Gives more info about a specific command.")]
        public async Task About(
            [Summary("The name of the command you want to know more about")] string command)
            => await Context.User.SendMessageAsync(embed: await _helpModule.About(Context, _commandService, command));
        #endregion

        #region Music Commands
        [Command("Join")]
        [Summary("The Bot joins the VC you're in.")]
        public async Task Join()
            => await ReplyAsync(embed: await _music.JoinAsync(Context));

        [Command("Leave")]
        [Summary("The Bot leaves the VC.")]
        public async Task Leave()
            => await ReplyAsync(embed: await _music.LeaveAsync(Context));

        [Command("Play")]
        [Summary("The Bot plays a song.")]
        public async Task Play(
            [Summary("Song name/url")][Remainder] string search)
            => await _music.Play(Context, search);

        [Command("Playnext")]
        [Summary("The Bot adds the song on top of the queue.")]
        public async Task PlayNext(
            [Summary("Song name/url")][Remainder] string search)
            => await _music.Play(Context, search, true);

        [Command("Skip")]
        [Summary("The Bot skips the current song.")]
        public async Task Skip()
            => await ReplyAsync(embed: await _music.SkipTrackAsync(Context));

        [Command("Stop")]
        [Summary("The Bot stops the current song and clears the queue.")]
        public async Task Stop()
            => await ReplyAsync(embed: await _music.StopAsync(Context));

        [Command("Pause")]
        [Summary("The Bot pauses the current song.")]
        public async Task Pause()
            => await ReplyAsync(embed: await _music.PauseAsync(Context));

        [Command("Resume")]
        [Summary("The Bot resumes the current song.")]
        public async Task Resume()
            => await ReplyAsync(embed: await _music.ResumeAsync(Context));

        [Command("Queue")]
        [Summary("Shows the queue.")]
        public async Task List()
            => await ReplyAsync(embed: await _music.GetQueueAsync(Context));

        [Command("Volume")]
        [Summary("Changes bot volume.")]
        public async Task Volume(
            [Summary("Arguments:\n" +
            "**Value (integer)** - changes the value to given integer\n" +
            "**If no value given - displays current volume**")]int? volume = null) 
            => await ReplyAsync(embed: await _music.SetVolumeAsync(Context, volume));


        [Command("Loop")]
        [Summary("Starts looping current song.")]
        public async Task Loop(
            [Summary("Arguments:\n" +
            "**status** - shows if looping is enabled/disabled\n" +
            "**If no argument is given - will enable/disable looping**")]string argument = null)
            => await ReplyAsync(embed: await _music.LoopAsync(Context, argument));

        [Command("Seek")]
        [Summary("Skips the current song to given time.")]
        public async Task Seek(
            [Summary("Arguments:\n" +
            "**hh:mm::ss** - Example (02:10:33) hours:minutes:seconds\n" +
            "**mm:ss** - Example (50:10) minutes:seconds")]string time)
            => await ReplyAsync(embed: await _music.SeekAsync(Context, time));

        [Command("Lyrics")]
        [Summary("Gets the lyrics of the current song.")]
        public async Task Lyrics()
            => await ReplyAsync(embed: await _music.GetLyricsAsync(Context));

        [Command("Playlist")]
        public async Task Playlist(
            [Summary("Arguments:\n" +
            "show - If no playlist name is specified - shows all playlists, otherwise it shows all songs in the playlist\n" +
            "load - Loads playlist (name required)\n" +
            "modify - Modifies playlist (name required)\n" +
            "create - Creates new playlist (name required)\n" +
            "remove - Removes playlist (name required)\n")]string arg1, 
            [Summary("Playlist name")]string arg2 = null, 
            [Summary("What you want to do to the playlist. Arguments:\n" +
            "add - Adds specified song to the playlist\n" +
            "remove - Removes specified song from the playlist\n" +
            "Example:\n" +
            "``playlist modify add/remove Rick Astley - Never Gonna Give You Up (Video)")]string arg3 = null, 
            [Summary("The name/url of the song")][Remainder]string arg4 = null) =>
            await ReplyAsync(embed: await _music.Playlist(Context, arg1, arg2, arg3, arg4));

        [Command("Shuffle")]
        [Summary("Shuffles all songs in queue.")]
        public async Task Shuffle()
            => await ReplyAsync(embed: await _music.ShuffleAsync(Context));
        #endregion

        #region Fun Commands
        [Command("Spin")]
        [Summary("Replies with an image.")]
        public async Task Spin()
            => await ReplyAsync(embed: new EmbedBuilder()
                .WithImageUrl("https://media2.giphy.com/media/DrwExaEgwjDAMldPcU/giphy.gif")
                .WithTitle("Spin!")
                .WithCurrentTimestamp()
                .Build());

        [Command("Move")]
        [RequireUserPermission(GuildPermission.Administrator)]
        [Summary("Moves the user from channel to channe, N times.")]
        public async Task Spin(IGuildUser user, int times)
        {
            try
            {
                if (user.VoiceChannel is null)
                {
                    await ReplyAsync(embed: await EmbedHandler.CreateErrorEmbed("Fun, Move", $"{user.Username} isn't in a Voice Channel!"));
                    return;
                }

                if (Context.Guild.VoiceChannels.Count < 2)
                    return;

                if (times > 20)
                    return;

                await ReplyAsync(embed: await EmbedHandler.CreateBasicEmbed("Fun, Move", $"{user.Mention} is being moved!"));

                SocketVoiceChannel orignalChannel = (SocketVoiceChannel)user.VoiceChannel;

                var channels = Context.Guild.VoiceChannels.Take(2).ToArray();

                for (int i = 0; i < times; i++)
                {
                    await user.ModifyAsync(x => x.Channel = channels.ElementAt(times % 2 == 0 ? 0 : 1));
                    await Task.Delay(750);
                }

                //Return the User to the original channel
                await user.ModifyAsync(x => x.Channel = orignalChannel);
                await ReplyAsync(embed: await EmbedHandler.CreateBasicEmbed("Fun, Move", $"{user.Mention} is not being moved!"));
            }
            catch(Exception e)
            {
                await ReplyAsync(embed: await EmbedHandler.CreateErrorEmbed("Fun, Move", $"Something went wrong: {e.Message}"));
                return;
            }
        }
        #endregion

        #region Administration
        [Command("Prefix")]
        [RequireUserPermission(GuildPermission.Administrator)]
        [Summary("Changes Guild prefix.")]
        public async Task Prefix(
            [Summary("The prefix you want to use in your Discord server")]string prefix)
            => await ReplyAsync(embed: await _guildConfigHandler.ChangePrefix(Context, prefix));

        [Command("Whitelist")]
        [RequireUserPermission(GuildPermission.Administrator)]
        [Summary("Adds Text Channels to Whitelist.")]
        public async Task Whitelist(
            [Summary("Arguments:\n" +
            "**add** - adds the specified channel to the whitelist\n" +
            "**remove** - removes specified channel from whitelist\n" +
            "**list** - lists all whitelisted channels")]string argument, IChannel channel = null)
            => await ReplyAsync(embed: await _guildConfigHandler.WhiteList(Context, argument, channel));
        #endregion
    }
}
