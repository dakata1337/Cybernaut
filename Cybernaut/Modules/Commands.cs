using Cybernaut.Handlers;
using Cybernaut.Services;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System;
using System.Globalization;
using System.Threading.Tasks;

namespace Cybernaut.Modules
{
    public class Commands : ModuleBase<SocketCommandContext>
    {
        public LavaLinkService AudioService { get; set; }

        #region Testing Commands
        /* Used for testing purposes */
        //[Command("join")]
        //public async Task FakeJoin()
        //{
        //    await autoMessagingService.OnGuildJoin(Context.Guild);
        //}
        #endregion

        #region Regular Commands
        [Command("status")]
        public async Task JoinAndPlay()
        {
            await ReplyAsync(embed: await EmbedHandler.DisplayInfoAsync(Context, Color.Purple));
        }

        [Command("invite")]
        public async Task GetInvite()
        {
            await ReplyAsync(embed: await CommandsService.GetInvite(Context));
        }
        #endregion

        #region Administration commands
        [Command("prefix"), RequireUserPermission(GuildPermission.Administrator)]
        public async Task ChangePrefix(string prefix)
        {
            await ReplyAsync(embed: await ConfigService.ChangePrefix(Context, prefix));
        }

        [Command("whitelist"), RequireUserPermission(GuildPermission.ManageChannels)]
        public async Task Whitelist(string arg, IChannel channel = null)
        {
            await ReplyAsync(embed: await ConfigService.WhiteList(Context, channel, arg));
        }

        [Command("auth"), RequireUserPermission(GuildPermission.Administrator)]
        public async Task Authentication(string arg = null, IRole role = null)
        {
            await ReplyAsync(embed: await ConfigService.Authentication(arg, role, Context));
        }

        [Command("mute"), RequireUserPermission(GuildPermission.MuteMembers)]
        public async Task Mute(IUser user, string time)
        {
            await ReplyAsync(embed: await CommandsService.Mute(user, time, Context));
        }

        [Command("unmute"), RequireUserPermission(GuildPermission.MuteMembers)]
        public async Task Mute(IUser user)
        {
            await ReplyAsync(embed: await CommandsService.Unmute(user, Context));
        }

        [Command("kick"), RequireUserPermission(GuildPermission.KickMembers)]
        public async Task Kick(IUser user, [Remainder]string reason = null)
        {
            await ReplyAsync(embed: await CommandsService.Kick(user, reason, Context));
        }

        [Command("Ban"), RequireUserPermission(GuildPermission.KickMembers)]
        public async Task Ban(IUser user, [Remainder] string reason = null)
        {
            await ReplyAsync(embed: await CommandsService.Ban(user, reason, Context));
        }
        #endregion

        #region Music Commands
        [Command("Join")]
        public async Task Join()
        {
            await ReplyAsync(embed: await AudioService.JoinAsync(Context.Guild, Context.User as IVoiceState, Context.Channel as ITextChannel, Context.User as SocketGuildUser));
        }

        [Command("Leave")]
        public async Task Leave()
        {
            await ReplyAsync(embed: await AudioService.LeaveAsync(Context, Context.User as SocketGuildUser));
        }

        [Command("Play")]
        public async Task Play([Remainder] string search)
        {
            await AudioService.Play(Context.User as SocketGuildUser, Context, search, Context.User as IVoiceState);
        }

        [Command("Stop")]
        public async Task Stop()
        {
            await ReplyAsync(embed: await AudioService.StopAsync(Context, Context.User as SocketGuildUser));
        }

        [Command("List")]
        public async Task List()
        {
            await ReplyAsync(embed: await AudioService.ListAsync(Context, Context.User as SocketGuildUser));
        }

        [Command("Skip")]
        public async Task Skip()
        {
            await ReplyAsync(embed: await AudioService.SkipTrackAsync(Context, Context.User as SocketGuildUser));
        }

        [Command("Volume")]
        public async Task Volume(int? volume = null)
        {
            await ReplyAsync(embed: await AudioService.SetVolumeAsync(Context, volume, Context.User as SocketGuildUser, Context.Channel as ITextChannel));
        }

        [Command("Pause")]
        public async Task Pause()
        {
            await ReplyAsync(embed: await AudioService.PauseAsync(Context, Context.User as SocketGuildUser));
        }

        [Command("Resume")]
        public async Task Resume()
        {
            await ReplyAsync(embed: await AudioService.ResumeAsync(Context, Context.User as SocketGuildUser));
        }

        [Command("Loop")]
        public async Task Loop(string argument = null)
        {
            await ReplyAsync(embed: await AudioService.LoopAsync(Context, Context.Channel as ITextChannel, Context.User as SocketGuildUser, argument));
        }

        [Command("Lyrics")]
        public async Task Lyrics()
        {
            await ReplyAsync(embed: await AudioService.GetLyricsAsync(Context));
        }

        [Command("Playlist")]
        public async Task Playlist(string arg1, string arg2 = null, string arg3 = null, [Remainder] string arg4 = null)
        {
            await ReplyAsync(embed: await AudioService.Playlist(Context, arg1, arg2, arg3, arg4));
        }

        [Command("Shuffle")]
        public async Task Shuffle()
        {
            await ReplyAsync(embed: await AudioService.ShuffleAsync(Context));
        }
        #endregion
    }
}