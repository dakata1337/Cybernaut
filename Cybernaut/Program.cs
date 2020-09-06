using Cybernaut.Services;
using System.Threading.Tasks;

namespace JustABot
{
    class Program
    {
        private static Task Main()
            => new DiscordService().InitializeAsync();
    }
}
