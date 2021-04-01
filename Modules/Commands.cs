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
        private Music _music;
        private CryptoModule _cryptoModule;
        public Commands(IServiceProvider serviceProvider)
        {
            _commandService = serviceProvider.GetRequiredService<CommandService>();
            _music = serviceProvider.GetRequiredService<Music>();
            _cryptoModule = serviceProvider.GetRequiredService<CryptoModule>();
        }

        #region Help Commands
        [Command("Help")]
        [Summary("Shows all commands.")]
        public async Task Help()
        {
            GlobalData.GuildConfigs.TryGetValue(Context.Guild.Id, out var guildConfig);
            EmbedBuilder embedBuilder = new EmbedBuilder();
            foreach (CommandInfo command in _commandService.Commands.ToList())
            {
                if (command.Name.ToLower() == "help") continue;

                embedBuilder.AddField(command.Name, string.Join("\n", new string[] { 
                    command.Summary ?? "No description available\nUsage:",
                    $"```{guildConfig.prefix}{command.Name.ToLower()} {(string.Join(' ', command.Parameters) ?? string.Empty)}```"}));
            }

            await ReplyAsync(
                message: "Here's a list of commands and their description: ",
                embed: embedBuilder.Build()
            );
        }
        #endregion

        #region Fun Commands
        [Command("Spin")]
        [Summary("Replies with an image.")]
        public async Task Spin() =>
            await ReplyAsync(embed: await EmbedHandler.CreateBasicEmbed("Spin", string.Empty, "https://media2.giphy.com/media/DrwExaEgwjDAMldPcU/giphy.gif"));
        #endregion

        #region Administrator Commands
        [Command("Prefix"), RequireUserPermission(GuildPermission.Administrator)]
        [Summary("Changes Guild prefix.")]
        public async Task Prefix(string prefix) =>
            await ReplyAsync(embed: await GuildConfigHandler.ChangePrefix(Context, prefix));

        [Command("Whitelist"), RequireUserPermission(GuildPermission.Administrator)]
        [Summary("Adds TextChannels to Whitelist.")]
        public async Task Whitelist(string argument, IChannel channel = null) =>
            await ReplyAsync(embed: await GuildConfigHandler.WhiteList(Context, argument, channel));

        [Command("Move"), RequireUserPermission(GuildPermission.Administrator)]
        public async Task MoveUser(IGuildUser user, int times)
        {
            string embedTitle = "Fun, Move";
            if (user.VoiceChannel is null)
            { 
                await ReplyAsync(embed: await EmbedHandler.CreateErrorEmbed(embedTitle, $"{user.Username} isn't in a Voice Channel!")); return;
            }

                var voiceChannels = Context.Guild.VoiceChannels.Where(x => x.Name == "Move-1" || x.Name == "Move-2").ToArray();
            if (voiceChannels.Length < 2)
            {
                await ReplyAsync(embed: await EmbedHandler.CreateErrorEmbed(embedTitle, $"The Guild needs to have 2 channels called \"Move-1\" and \"Move-2\"")); return;
            }

            int moveLimit = 20;
            if (times > moveLimit)
            {
                await ReplyAsync(embed: await EmbedHandler.CreateErrorEmbed(embedTitle, $"You're not allowed to move the user more than {moveLimit}")); return;
            }

            await ReplyAsync(embed: await EmbedHandler.CreateBasicEmbed(embedTitle, $"{user.Username} is being moved!"));
            SocketVoiceChannel orignalChannel = (SocketVoiceChannel)user.VoiceChannel;
            for (int i = 0; i < times; i++)
            {
                await user.ModifyAsync(x => x.Channel = voiceChannels.ElementAt(i % 2 == 0 ? 0 : 1));
                await Task.Delay(750);
            }

            // Return the User to the original channel
            await user.ModifyAsync(x => x.Channel = orignalChannel);
            await ReplyAsync(embed: await EmbedHandler.CreateBasicEmbed(embedTitle, $"{user.Username} was send back to the channel he was in!"));
        }
        #endregion

        #region Music Commands
        [Command("Join")]
        [Alias("j")]
        public async Task Join(IVoiceChannel voiceChannel = null) =>
            await ReplyAsync(embed: await _music.JoinAsync(Context, voiceChannel));

        [Command("Leave")]
        [Alias("l")]
        public async Task Leave() =>
            await ReplyAsync(embed: await _music.LeaveAsync(Context));

        [Command("Play")]
        [Alias("p")]
        public async Task Play([Remainder] string songNameOrURL) =>
            await ReplyAsync(embed: await _music.PlayAsync(Context, songNameOrURL, false)); // False = Add to queue 

        [Command("Playnext")]
        [Alias("pn")]
        public async Task PlayNext([Remainder] string songNameOrURL) =>
            await ReplyAsync(embed: await _music.PlayAsync(Context, songNameOrURL, true)); // True = Play next 

        [Command("Skip")]
        [Alias("sk")]
        public async Task Skip() =>
            await ReplyAsync(embed: await _music.SkipTrackAsync(Context));

        [Command("Stop")]
        [Alias("sp")]
        public async Task Stop() =>
            await ReplyAsync(embed: await _music.StopAsync(Context));

        [Command("Pause")]
        [Alias("ps")]
        public async Task Pause() =>
            await ReplyAsync(embed: await _music.PauseAsync(Context));

        [Command("Resume")]
        [Alias("rs")]
        public async Task Resume() =>
            await ReplyAsync(embed: await _music.ResumeAsync(Context));

        [Command("Queue")]
        [Alias("q")]
        public async Task Queue() =>
            await ReplyAsync(embed: await _music.GetQueueAsync(Context));

        [Command("Volume")]
        [Alias("v")]
        public async Task Volume(int? volume = null) =>
            await ReplyAsync(embed: await _music.SetVolumeAsync(Context, volume));

        [Command("Loop")]
        [Alias("l")]
        public async Task Loop(string arg = null) =>
            await ReplyAsync(embed: await _music.LoopAsync(Context, arg));

        [Command("Seek")]
        [Alias("s")]
        public async Task Seek(string seekTo) =>
            await ReplyAsync(embed: await _music.SeekAsync(Context, seekTo));
        #endregion

        #region Cryptocurrency Commands
        [Command("Crypto")]
        [Alias("c")]
        public async Task Crypto(string cryptoName = null) =>
            await ReplyAsync(embed: await _cryptoModule.GetPriceAsync(cryptoName));
        #endregion
    }
}
