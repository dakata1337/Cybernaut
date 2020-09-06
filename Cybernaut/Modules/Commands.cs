using Cybernaut.Handlers;
using Cybernaut.Services;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System.Threading.Tasks;

namespace Cybernaut.Modules
{
    public class Commands : ModuleBase<SocketCommandContext>
    {
        public LavaLinkService AudioService { get; set; }
        ConfigService configService = new ConfigService();
        AutoMessagingService autoMessagingService = new AutoMessagingService(); //Used for testing purposes 
        CommandsService commandsService = new CommandsService();

        [Command("test")]
        public async Task JoinAndPlay()
        {
            await ReplyAsync(embed: await EmbedHandler.DisplayInfoAsync(Context, Color.Purple));
        }

        [Command("prefix"), RequireUserPermission(GuildPermission.Administrator)]
        public async Task ChangePrefix(string prefix)
        {
            await ReplyAsync(embed: await configService.ChangePrefix(Context, prefix));
        }

        [Command("whitelist"), RequireUserPermission(GuildPermission.ManageChannels)]
        public async Task Whitelist(string arg, IChannel channel)
        {
            await ReplyAsync(embed: await configService.WhiteList(Context, channel.Id, arg));
        }

        [Command("invite")]
        public async Task GetInvite()
        {
            await ReplyAsync(embed: await commandsService.GetInvite(Context));
        }

        /* Used for testing purposes
        [Command("join")]
        public async Task FakeJoin()
        {
            await autoMessagingService.OnGuildJoin(Context.Guild);
        }
        */

        /*Music Commands*/

        [Command("Join")]
        public async Task Join()
        {
            await ReplyAsync(embed: await AudioService.JoinAsync(Context.Guild, Context.User as IVoiceState, Context.Channel as ITextChannel));
        }

        [Command("Leave")]
        public async Task Leave()
        {
            await ReplyAsync(embed: await AudioService.LeaveAsync(Context.Guild, Context.User as SocketGuildUser));
        }

        [Command("Play")]
        public async Task Play([Remainder] string search)
        {
            await ReplyAsync(embed: await AudioService.PlayAsync(Context.User as SocketGuildUser, Context.Guild, search, Context.User as IVoiceState, Context.Channel as ITextChannel));
        }

        [Command("Stop")]
        public async Task Stop()
        {
            await ReplyAsync(embed: await AudioService.StopAsync(Context.Guild, Context.User as SocketGuildUser));
        }

        [Command("List")]
        public async Task List()
        {
            await ReplyAsync(embed: await AudioService.ListAsync(Context.Guild));
        }

        [Command("Skip")]
        public async Task Skip()
        {
            await ReplyAsync(embed: await AudioService.SkipTrackAsync(Context.Guild, Context.User as SocketGuildUser));
        }

        [Command("Volume")]
        public async Task Volume(int volume)
        {
            await ReplyAsync(embed: await AudioService.SetVolumeAsync(Context.Guild, volume, Context.User as SocketGuildUser, Context.Channel as ITextChannel));
        }

        [Command("Pause")]
        public async Task Pause()
        {
            await ReplyAsync(embed: await AudioService.PauseAsync(Context.Guild, Context.User as SocketGuildUser));
        }

        [Command("Resume")]
        public async Task Resume()
        {
            await ReplyAsync(embed: await AudioService.ResumeAsync(Context.Guild, Context.User as SocketGuildUser));
        }

        [Command("Loop")]
        public async Task Loop(string argument = null)
        {
            await ReplyAsync(embed: await AudioService.LoopAsync(Context.Guild, Context.Channel as ITextChannel, Context.User as SocketGuildUser, argument));
        }
    }
}
