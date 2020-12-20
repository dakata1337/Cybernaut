using Cybernaut.DataStructs;
using Cybernaut.Handlers;
using Discord;
using Discord.Net;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Cybernaut.Services
{
    class UserManagementService
    {
        private readonly DiscordSocketClient _client;
        public UserManagementService(IServiceProvider services)
        {
            _client = services.GetRequiredService<DiscordSocketClient>();
        }

        public async Task<Task> InitializeAsync()
        {
            await LoggingService.Log("UMS", Discord.LogSeverity.Info, "Loading UMS");
            var userManagementService = new Thread(async () =>
            {
                #region Code
                #if DEBUG
                Stopwatch stopwatch = new Stopwatch();
                #endif
                while (true)
                {
                    #if DEBUG
                    stopwatch.Restart();
                    #endif
                    string[] configs = Directory.GetFiles(GlobalData.Config.ConfigLocation, "*.json");
                    for (int i = 0; i < configs.Length; i++)
                    {
                        string configFile = configs[i];
                        string json = string.Empty;
                        try
                        {
                            using (StreamReader sr = File.OpenText(configs[i]))
                            {
                                json = sr.ReadToEnd();
                            }
                        }
                        catch (IOException)
                        {
                            continue;
                        }

                        var jObj = (JObject)JsonConvert.DeserializeObject(json);

                        await MuteCheck(jObj, configFile);
                    }
                    #if DEBUG
                    stopwatch.Stop();
                    long ticks = stopwatch.ElapsedTicks;
                    double ms = (1000000000.0 * ticks / Stopwatch.Frequency) / 1000000.0;
                    await LoggingService.LogInformationAsync("UMS", $"{configs.Length} configs checked in {Math.Round(ms, 2).ToString().Replace(",", ".")}ms");
                    #endif
                    Thread.Sleep(30 * 1000); //30 seconds delay
                }
                #endregion
            });

            #region Start Threads
            userManagementService.Start();
            #endregion

            await LoggingService.Log("UMS", Discord.LogSeverity.Info, "UMS loaded");
            return Task.CompletedTask;
        }

        public async Task<Task> MuteCheck(JObject jObj, string configFile)
        {
            #region User Muted Check
            JObject[] mutedUsers = jObj["mutedUsers"].ToObject<JObject[]>();
            if (mutedUsers == null)
                return Task.CompletedTask;

            List<JObject> newList = new List<JObject>(mutedUsers);

            int count = 0;
            foreach (JObject mutedUser in mutedUsers)
            {
                var userInfo = mutedUser.ToObject<UserInfo>();
                var timeSpan = userInfo.ExpiresOn.Subtract(DateTime.Now);
                if (timeSpan.CompareTo(TimeSpan.Zero) < 0)
                {
                    newList.RemoveAt(count);
                    jObj["mutedUsers"] = JToken.FromObject(newList.ToArray());

                    var user = _client.GetUser(userInfo.Id);
                    var guild = _client.GetGuild(userInfo.GuildId);
                    var dmChannel = await user.GetOrCreateDMChannelAsync();

                    try
                    {
                        await dmChannel.SendMessageAsync($"Your mute in {guild.Name} has just ended.");
                    }
                    catch (HttpException)
                    {
                        await guild.DefaultChannel.SendMessageAsync($"{user.Username}'s mute ended but he doesn't allow direct msgs.");
                    }

                    await LoggingService.LogInformationAsync("Guild", $"{user.Username}'s mute ended.");
                    //Saving to file
                    File.WriteAllText(configFile,
                        JsonConvert.SerializeObject(jObj, Formatting.Indented));
                }
                count++;
            }

            return Task.CompletedTask;
            #endregion
        }
    }
}
