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
                    //Get all files with extension .json
                    string[] configs = Directory.GetFiles(GlobalData.Config.ConfigLocation, "*.json");

                    for (int i = 0; i < configs.Length; i++)
                    {
                        //Current Config
                        string configFile = configs[i];

                        //JSON data
                        string json = string.Empty;

                        //Try reading the file
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

                        //Deserialize config to JObject
                        var jObj = (JObject)JsonConvert.DeserializeObject(json);

                        //Check for Muted Users 
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
            //get mutedUsers 
            JObject[] mutedUsers = jObj["mutedUsers"].ToObject<JObject[]>();

            //If there are none return
            if (mutedUsers == null)
                return Task.CompletedTask;

            //Make List with User Info
            List<JObject> newList = new List<JObject>(mutedUsers);

            int userCounter = 0;
            foreach (JObject mutedUser in mutedUsers)
            {
                //Create User object
                var userInfo = mutedUser.ToObject<UserInfo>();

                //Convert to timeSpan
                var timeSpan = userInfo.ExpiresOn.Subtract(DateTime.Now);

                //Check if the time has passed
                if (timeSpan.CompareTo(TimeSpan.Zero) < 0)
                {
                    //Remove user from the list
                    newList.RemoveAt(userCounter);

                    //Save the list to the config
                    jObj["mutedUsers"] = JToken.FromObject(newList.ToArray());

                    //Get user, guild & DM channel
                    var user = _client.GetUser(userInfo.Id);
                    var guild = _client.GetGuild(userInfo.GuildId);
                    var dmChannel = await user.GetOrCreateDMChannelAsync();

                    //Try notifying the user
                    try
                    {
                        await dmChannel.SendMessageAsync($"Your mute in {guild.Name} has just ended.");
                    }
                    catch (HttpException)
                    {
                        //If the user doesnt allow DM send it to the server
                        await guild.DefaultChannel.SendMessageAsync($"{user.Mention}'s mute ended but he doesn't allow direct msgs.");
                    }

                    await LoggingService.LogInformationAsync("Guild", $"{user.Username}'s mute ended.");
                    //Save Config to File
                    File.WriteAllText(configFile,
                        JsonConvert.SerializeObject(jObj, Formatting.Indented));
                }
                userCounter++;
            }

            return Task.CompletedTask;
            #endregion
        }
    }
}
