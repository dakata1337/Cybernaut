using Cybernaut.DataStructs;
using Cybernaut.Handlers;
using Discord;
using Discord.WebSocket;
using System.Threading.Tasks;
using System.Collections.Generic;
using Discord.Commands;
using System;
using Discord.Rest;
using System.Linq;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Threading;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Text;

namespace Cybernaut.Services
{
    public class AutoMessagingService
    {
        public Task OnUserJoin(SocketGuildUser user)
        {
            #region Code
            if (user.IsBot)
                return Task.CompletedTask;

            var AuthThread = new Thread(async () => await UserAuth(user));
            AuthThread.Start();

            return Task.CompletedTask;
            #endregion
        }

        public Task OnUserLeft(SocketGuildUser user)
        {
            #region Delete Users CAPTCHAs
            SocketGuild guild = user.Guild;
            string configLocation = GetService.GetConfigLocation(guild);

            var jObj = GetService.GetJObject(guild);

            JObject[] ogArray = jObj["usersCAPTCHA"].ToObject<JObject[]>();
            List<JObject> newList = new List<JObject>();

            #region CPATCHA Checks
            if (!(ogArray is null))
            {
                newList = new List<JObject>(ogArray);
                foreach (JObject item in ogArray)
                {
                    if (!(item is null))
                    {
                        var userCAPTCHA = item.ToObject<CAPTCHAs>();
                        if (userCAPTCHA.userID != user.Id)
                            continue;
                        else
                        {
                            newList.Remove(item);
                            jObj["usersCAPTCHA"] = JToken.FromObject(newList.ToArray());
                            File.Delete(@$"captchas/{user.Guild.Id}-{userCAPTCHA.userID}.png");
                            File.WriteAllText(configLocation, JsonConvert.SerializeObject(jObj, Formatting.Indented));
                            break;
                        }
                    }
                }
            }
            #endregion
            #endregion
            return Task.CompletedTask;
        }

        public async Task<Task> UserAuth(SocketGuildUser user)
        {
            #region Code
            #region Reading config
            string configFile = GetService.GetConfigLocation(user.Guild);

            var json = File.ReadAllText(configFile);
            var jObj = GetService.GetJObject(user.Guild);
            #endregion

            if ((bool)jObj["GiveRoleOnJoin"] == false)
                return Task.CompletedTask;

            //Get Auth Role
            IRole role = user.Guild.GetRole((ulong)jObj["RoleOnJoin"]);

            //Checks if an auth role is set
            if (role is null)
                return Task.CompletedTask;

            #region RequireCAPTCHA Check
            //If RequireCAPTCHA is false give role straight away
            if (jObj["RequireCAPTCHA"].ToObject<bool>() == false)
            {
                await user.AddRoleAsync(role);
                return Task.CompletedTask;
            }
            #endregion

            #region CAPTCHA
            try
            {
                //Get DMs
                IDMChannel userPM = await user.GetOrCreateDMChannelAsync();

                JObject[] CAPTCHAs = jObj["usersCAPTCHA"].ToObject<JObject[]>();
                List<JObject> newList = new List<JObject>(CAPTCHAs);
                var captchaDir = @"captchas";

                //Make list of objects containing old captchas
                List<JObject> toRemove = new List<JObject>();
                foreach (JObject item in newList)
                {
                    CAPTCHAs check = item.ToObject<CAPTCHAs>();
                    if (check.userID == user.Id)
                        toRemove.Add(item);
                }

                string captcha = GetRandomCAPTCHA();
                string imgLocation = captchaDir + $"/{user.Guild.Id}-{user.Id}.png";
                CAPTCHAs userCAPTCHA = new CAPTCHAs() { captchaAnswer = captcha, userID = user.Id };

                newList.Add(JObject.FromObject(userCAPTCHA));

                //Remove old captchas
                foreach (JObject item in toRemove)
                {
                    CAPTCHAs check = item.ToObject<CAPTCHAs>();
                    if (check.userID == user.Id)
                        newList.Remove(item);
                }

                if (!Directory.Exists(captchaDir))
                    Directory.CreateDirectory(captchaDir);

                Bitmap captchaImage = GetCaptchaImage(captcha);
                captchaImage.Save(imgLocation);


                #region Custom Embed 
                var fields = new List<EmbedFieldBuilder>();
                fields.Add(new EmbedFieldBuilder
                {
                    Name = $"What is this?",
                    Value = $"This is an automatic human verification.\n" +
                    $"Please reply in this chat with the word you see on the image.",
                    IsInline = false
                });
                fields.Add(new EmbedFieldBuilder
                {
                    Name = $"Its not working?",
                    Value = $"First please make sure you typed the word as show on the image.\n" +
                    $"If you're sure its correct please contact **{GlobalData.Config.BotOwner}**.",
                    IsInline = false
                });
                #endregion
                await userPM.SendMessageAsync("", embed:
                    await EmbedHandler.CreateCustomEmbed(user.Guild, Discord.Color.Blue, fields, "CAPTCHA", true));
                await userPM.SendFileAsync(imgLocation);

                jObj["usersCAPTCHA"] = JToken.FromObject(newList);
                File.WriteAllText(configFile,
                           JsonConvert.SerializeObject(jObj, Formatting.Indented));
               
            }
            catch { return Task.CompletedTask; }
            #endregion

            return Task.CompletedTask;
            #endregion
        }

        public async Task OnGuildJoin(SocketGuild guild)
        {
            #region Code
            string configLocation = GetService.GetConfigLocation(guild);
            if (!File.Exists(configLocation))
            {
                string json = JsonConvert.SerializeObject(GuildData.GenerateNewConfig(GlobalData.Config.DefaultPrefix), Formatting.Indented);
                var jObj = JsonConvert.DeserializeObject<JObject>(json);

                if (jObj["whitelistedChannels"].Value<JArray>().Count == 0)
                {
                    ulong[] ts = { guild.DefaultChannel.Id };
                    jObj["whitelistedChannels"] = JToken.FromObject(ts);
                }
                File.WriteAllText(configLocation, JsonConvert.SerializeObject(jObj, Formatting.Indented), new UTF8Encoding(false));
            }

            #region Custom Embed 
            var fields = new List<EmbedFieldBuilder>();
            fields.Add(new EmbedFieldBuilder
            {
                Name = "**NOTE**",
                Value = $"By default, {guild.DefaultChannel.Mention} is the default bot channel.\n" +
                $"If you want to change it go to the channel and type {GlobalData.Config.DefaultPrefix}prefix YourPrefixHere",
                IsInline = false
            });

            fields.Add(new EmbedFieldBuilder
            {
                Name = "Experiencing problems?",
                Value = $"If you experience any problems report them to **{GlobalData.Config.BotOwner}**.",
                IsInline = false
            });
            #endregion

            var channel = guild.DefaultChannel as SocketTextChannel;

            await channel.SendMessageAsync(embed: 
                await EmbedHandler.CreateCustomEmbed(guild, Discord.Color.Blue, fields, "I have arrived!", true, $"Thank you for choosing {guild.CurrentUser.Username}")); //Sends the Embed
            #endregion
        }

        private string GetRandomCAPTCHA()
        {
            string alphabet = "abcdefghijklmnopqrstuvwxyz";
            int number = 0;

            string captcha = string.Empty;
            Random random = new Random();
            for (int i = 0; i < 8; i++)
            {
                number = random.Next(0, alphabet.Length);
                if(random.Next(0,100) >= 30)
                    captcha += alphabet[number];
                else
                    captcha += char.ToUpper(alphabet[number]);
            }
            return captcha;
        }

        private Bitmap GetCaptchaImage(string captchaAnswer)
        {
            var image = new Bitmap(165, 35);
            var font = new Font("TimesNewRoman", 28, FontStyle.Bold, GraphicsUnit.Pixel);
            var graphics = Graphics.FromImage(image);

            Random rnd = new Random();
            SolidBrush brushColor = new SolidBrush(System.Drawing.Color.FromArgb(rnd.Next(256), rnd.Next(256), rnd.Next(256)));

            graphics.DrawString(captchaAnswer, font, brushColor, new Point(0,0));

            Pen pen = new Pen(Brushes.Gray) { Width = 1 };

            for (int i = 0; i < 8; i++)
            {
                int x0 = rnd.Next(0, image.Width);
                int y0 = rnd.Next(0, image.Height);
                int x1 = rnd.Next(0, image.Width);
                int y1 = rnd.Next(0, image.Height);
                graphics.DrawLine(pen, x0, y0, x1, x1);
            }

            return image;
        }
    }
}
