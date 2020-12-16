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
                #region Muted users check
                #if DEBUG
                Stopwatch stopwatch = new Stopwatch();
                #endif
                while (true)
                {
                    #if DEBUG
                    stopwatch.Restart();
                    #endif
                    string[] configs = System.IO.Directory.GetFiles(GlobalData.Config.ConfigLocation, "*.json");
                    for (int i = 0; i < configs.Length; i++)
                    {
                        string configFile = configs[i];

                        //await LoggingService.LogInformationAsync("UMS", $"Checking: {configFile}");


                        /*
                         * There is a slight chance of the program braking here because
                         * If the file is being used at the same time as the confing is
                         * being read the program will explode.
                         * *Edit* Should be ok like this... i think.
                         */

                        string json = string.Empty;
                        try
                        {
                            using (FileStream stream = new FileInfo(configFile).Open(FileMode.Open, FileAccess.Read, FileShare.None))
                            {
                                stream.Close();
                                json = File.ReadAllText(configFile);
                            }
                        }
                        catch (IOException) //This code will run if this event occurs 
                        {
                            continue;
                        }

                        var jObj = (JObject)JsonConvert.DeserializeObject(json);
                        JObject[] mutedUsers = jObj["mutedUsers"].ToObject<JObject[]>();
                        List<JObject> newList = new List<JObject>();

                        /* 
                         * TODO:
                         * Must add some type of spam detection. Maybe kick after 10 msgs?
                         * Must make the mute check into a function and call it.
                         */

                        //Checks if the user is muted already
                        if (!(mutedUsers is null))
                        {
                            newList = new List<JObject>(mutedUsers);
                            int count = 0;
                            foreach (JObject mutedUser in mutedUsers)
                            {
                                if (!(mutedUser is null))
                                {
                                    var userInfo = mutedUser.ToObject<UserInfo>();
                                    var timeSpan = userInfo.ExpiresOn.Subtract(DateTime.Now);
                                    if (timeSpan.CompareTo(TimeSpan.Zero) < 0)
                                    {
                                        newList.RemoveAt(count);
                                        jObj["mutedUsers"] = JToken.FromObject(newList.ToArray());

                                        var user = _client.GetUser(userInfo.Id);
                                        var guild = _client.GetGuild(userInfo.GuildId);
                                        await user.GetOrCreateDMChannelAsync();

                                        try
                                        {
                                            await user.SendMessageAsync($"Your mute in {guild.Name} has just ended.");
                                        }
                                        catch (HttpException)
                                        {
                                            await LoggingService.LogInformationAsync("Guild", $"{user.Username}'s mute ended but he doesn't allow direct msgs.");
                                            //Saving to file
                                            File.WriteAllText(configFile,
                                                JsonConvert.SerializeObject(jObj, Formatting.Indented));
                                            continue;
                                        }

                                        await LoggingService.LogInformationAsync("Guild", $"{user.Username}'s mute ended.");
                                        //Saving to file
                                        File.WriteAllText(configFile,
                                            JsonConvert.SerializeObject(jObj, Formatting.Indented));
                                    }
                                }
                                count++;
                            }
                        }
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
    }
}
