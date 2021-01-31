using System;

namespace Discord_Bot
{
    class Program
    {
        static void Main(string[] args)
            => new DiscordService().InitializeAsync().ConfigureAwait(false);
    }
}
