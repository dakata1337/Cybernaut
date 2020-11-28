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
                while (true)
                {
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

                        dynamic jsonObj = JsonConvert.DeserializeObject(json);
                        JObject[] ogArray = jsonObj.mutedUsers.ToObject<JObject[]>();
                        List<JObject> newList = new List<JObject>();

                        /* 
                         * TODO:
                         * Must add some type of spam detection. Maybe kick after 10 msgs?
                         * Must make the mute check into a function and call it.
                         */

                        //Checks if the user is muted already
                        if (!(ogArray is null))
                        {
                            newList = new List<JObject>(ogArray);
                            int count = 0;
                            foreach (JObject item in ogArray)
                            {
                                if (!(item is null))
                                {
                                    var userInfo = item.ToObject<UserInfo>();
                                    var timeSpan = userInfo.ExpiresOn.Subtract(DateTime.Now);
                                    if (timeSpan.CompareTo(TimeSpan.Zero) < 0)
                                    {
                                        newList.RemoveAt(count);
                                        jsonObj["mutedUsers"] = JToken.FromObject(newList.ToArray());

                                        var user = _client.GetUser(userInfo.Id);
                                        await user.GetOrCreateDMChannelAsync();

                                        try
                                        {
                                            await user.SendMessageAsync($"Your mute in {_client.GetGuild(userInfo.GuildId).Name} has just ended.");
                                        }
                                        catch (HttpException)
                                        {
                                            await LoggingService.LogInformationAsync("Guild", $"{_client.GetUser(userInfo.Id).Username}'s mute ended but he doesn't allow direct msgs.");
                                            //Saving to file
                                            File.WriteAllText(configFile,
                                                JsonConvert.SerializeObject(jsonObj, Formatting.Indented));
                                            continue;
                                        }

                                        await LoggingService.LogInformationAsync("Guild", $"{_client.GetUser(userInfo.Id).Username}'s mute ended.");
                                        //Saving to file
                                        File.WriteAllText(configFile,
                                            JsonConvert.SerializeObject(jsonObj, Formatting.Indented));
                                    }
                                }
                                count++;
                            }
                        }
                    }
                    Thread.Sleep(60 * 1000); //60 seconds delay
                }
            });

            #region Start Threads
            userManagementService.Start();
            #endregion

            await LoggingService.Log("UMS", Discord.LogSeverity.Info, "UMS loaded");
            return Task.CompletedTask;
        }
    }
}
