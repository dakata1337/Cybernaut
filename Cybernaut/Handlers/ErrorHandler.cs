using Cybernaut.DataStructs;
using Cybernaut.Services;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Cybernaut.Handlers
{
    class ErrorHandler
    {
        public static async Task ExecutionErrorHandler(IResult result, ICommandContext context)
        {
            #region Code
            string msg = string.Empty;
            string title = "Command Error";

            #region Messages
            switch (result.Error)
            {
                case CommandError.BadArgCount:
                    msg = "This command is not supposed to be used like this.";
                    break;
                case CommandError.UnmetPrecondition:
                    msg = result.ErrorReason;
                    break;
                case CommandError.ObjectNotFound:
                    msg = "The specified object was not found.";
                    break;
                case CommandError.UnknownCommand:
                    msg = "Unknown command.";
                    break;
                default:
                    await LoggingService.LogCriticalAsync("CommandError",
                            $"{context.Message.Author}: {context.Message.Content} => {result.ErrorReason}  ({context.Guild.Id} | {context.Guild.Name})");
                    msg = $"Internal Bot error. Please report to {GlobalData.Config.BotOwner}";
                    break;
            }
            #endregion

            if (msg != null)
            {
                await context.Channel.SendMessageAsync(embed: await EmbedHandler.CreateErrorEmbed(title, msg));
                await LoggingService.LogCriticalAsync("ErrorHandler", $"Error: {result.Error }\nError reason: {result.ErrorReason}");
            }
            #endregion
        }
    }
}
