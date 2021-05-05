using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using System.Net.Cache;
using System.Globalization;
using System.Linq;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Discord_Bot.Handlers;
using Discord_Bot.Services;
using Discord.WebSocket;
using HtmlAgilityPack;
using Discord;

namespace Discord_Bot.Modules
{
    class OLXModule
    {
        private DiscordSocketClient _clinet;
        private static string Source = "OLX_Module";

        public OLXModule(IServiceProvider serviceProvider)
        {
            _clinet = serviceProvider.GetRequiredService<DiscordSocketClient>();
        }

        public async Task InitializeAsync()
        {
            Thread checkThread = new Thread(async () =>
            {
                #region Check For Directory/Files
                string categoriesPath = @"categories";
                string filePath = $@"{categoriesPath}/categories.json";
                string guildPath = $@"{categoriesPath}/guilds.json";

                if (!Directory.Exists(categoriesPath))
                    Directory.CreateDirectory(categoriesPath);

                if (!File.Exists(guildPath))
                    File.WriteAllText(guildPath, JsonConvert.SerializeObject(new JArray()));

                if (!File.Exists(filePath))
                    File.WriteAllText(filePath, JsonConvert.SerializeObject(new List<Category>() { new Category()
                    {
                        Url = "https://www.olx.bg/elektronika/kompyutrni-aksesoari-chasti/aksesoari-chasti/",
                        Latest = "new"
                    }}));
                #endregion

                Thread.Sleep(5000);
                while (true)
                {
                    List<Category> categoriesToWatch = JsonConvert.DeserializeObject<List<Category>>(File.ReadAllText(filePath));

                    for (int i = 0; i < categoriesToWatch.Count; i++)
                    {
                        var category = categoriesToWatch[i];
                        var url = category.Url;
                        var latest = category.Latest;

                        #region Get Ads & update file
                        //Get Ads
                        var adSearchResult = await GetLatestAds(url + "?search%5Border%5D=created_at%3Adesc" + $"&random={DateTime.UtcNow.Ticks}", latest);

                        if (adSearchResult.Ads == null)
                            continue;

                        var ads = adSearchResult.Ads;
                        string token = url.Replace("https://www.olx.bg/", "");

                        // Update categoriesToWatch
                        categoriesToWatch[i] = new Category()
                        {
                            Token = token,
                            Url = url,
                            Latest = ads.Count == 0 ? latest : ads[0].Url
                        };
                        #endregion

                        // Reverse list so first in comes last out
                        ads.Reverse();

                        #region Send To Guilds
                        var guildsConfig = JsonConvert.DeserializeObject<List<OlxGuildConfig>>(File.ReadAllText(guildPath));
                        foreach (var ad in ads.ToList())
                        {
                            foreach (var guild in _clinet.Guilds)
                            {
                                var guildConfig = guildsConfig.Where(x => x.GuildId == guild.Id).FirstOrDefault();

                                if (guildConfig == null)
                                    continue;

                                if (!guildConfig.Categories.Contains(category.Token))
                                    continue;

                                var textChannel = guild.TextChannels.Where(x => x.Id == guildConfig.OlxChannel).FirstOrDefault();

                                await textChannel.SendMessageAsync(embed: await CreateAdEmbed($"New Ad Found!",
                                    $"**Category:** {(category.Token.Length > 50 ? "\n" : "")}[{category.Token}]({category.Url})\n" +
                                    $"**Title: {(ad.Title.Length > 50 ? "\n" : "")}[{ad.Title.Replace("*", "").Replace("&quot;", "\"")}]({ad.Url})\n" +
                                    $"Price: {ad.Price}\n" +
                                    $"Location: {ad.Location}\n" +
                                    $"Description:**\n" +
                                    $"{ad.Description.Replace("*", "")}",
                                    imageUrl: ad.Image,
                                    timestamp: ad.CreationTime
                                ));
                            }
                        }
                        #endregion

                        // Save categoriesToWatch to file
                        File.WriteAllText(filePath, JsonConvert.SerializeObject(categoriesToWatch, Formatting.Indented));
                        Thread.Sleep(100);
                    }

                    // Wait 10 seconds
                    Thread.Sleep(10 * 1000);
                }
            });
            checkThread.IsBackground = true;
            checkThread.Start();

            LoggingService.Log(Source, $"{Source} initialized");
        }

        #region GetLatestAds
        private async Task<AdSearchResult> GetLatestAds(string url, string latestAd)
        {
            try
            {
                string cleanUrl = url.Substring(0, url.IndexOf('?'));
                // LoggingService.Log(Source, $"Starting to look for new ads ({cleanUrl})", ConsoleColor.Cyan);

                // Ads location
                string listSales = "//tr[@class='wrap']/td/div/table/tbody";
                // Ad Url location
                string adURL = "tr/td[2]/div/h3/a";
                // Ad Price location
                string adPrice = "tr/td[3]/div/p/strong";
                // Ad Location location
                string location = "tr[2]/td/div/p/small/span";
                // Ad Creation Time
                string adTime = "tr[2]/td/div/p/small[2]/span";

                AdInfo backupAd = new AdInfo();
                List<AdInfo> ads = new List<AdInfo>();
                int adCount = 0;
                int page = 1;

                while (page <= 3)
                {
                    #region Get Ads
                    // await LoggingService.Log(Source, $"Going on page {page}");

                    // Get HTML Document of current page
                    var htmlDocument = ReturnHtmlDocumentFromUrl(url + $"&page={page}").Result;

                    // Get Ads from current Page
                    var nodeSales = htmlDocument.DocumentNode.SelectNodes(listSales);
                    #endregion

                    #region Go through each Ad
                    bool adFound = false;
                    foreach (var item in nodeSales)
                    {
                        // Get Ad Url
                        string _url = item.SelectSingleNode(adURL).GetAttributeValue("href", string.Empty);

                        if (_url.EndsWith(";promoted"))
                            continue;

                        // Removes everything after the last #  
                        int index = _url.LastIndexOf("#");
                        if (index > 0)
                            _url = _url.Substring(0, index);


                        // LAST AD FOUND
                        if (_url == latestAd)
                        {
                            adFound = true;
                            break;
                        }


                        // Get Ad Title
                        string _title = item.SelectSingleNode(adURL)?.InnerText;

                        // Get Ad Price
                        string _price = item.SelectSingleNode(adPrice)?.InnerText;

                        // Get Ad Location
                        string _location = item.SelectSingleNode(location)?.InnerText;

                        // Get Ad Creation Time
                        string _time = item.SelectSingleNode(adTime)?.InnerText;

                        // 12:30 = 5 Chars
                        int lenght = 5;
                        _time = _time.Substring(_time.Length - lenght, lenght);

                        // Get DateTime from string
                        var ci = new CultureInfo("bg-BG");
                        if (!DateTime.TryParseExact(_time, "HH:mm", ci, DateTimeStyles.None, out DateTime dateTime))
                            dateTime = DateTime.MinValue;

                        string _description = string.Empty;
                        string _image = string.Empty;

                        // Opens the Ad to get Image & Description
                        var adInfo = ReturnHtmlDocumentFromUrl(_url).Result;

                        // Get Ad Description
                        _description = adInfo.DocumentNode.SelectSingleNode("//div[@class='content']/div/div/div[2]/div/div[2]/div[2]")?.InnerText;
                        try
                        {
                            // Get Ad Image
                            _image = adInfo.DocumentNode.SelectSingleNode("//div[@class='content']/div/div/div/div/img").GetAttributeValue("src", string.Empty);
                        }
                        catch { _image = "https://www.thermaxglobal.com/wp-content/uploads/2020/05/image-not-found.jpg"; }

                        // Adds Ad to List
                        var newAd = new AdInfo
                        {
                            Title = _title.Trim(),
                            Price = _price == null ? "not set" : _price.Trim(),
                            Location = _location == null ? "not set" : _location.Trim(),
                            Description = _description != null ? TruncateAtWord(_description.Trim(), 300, _url).Result : "No descrition!",
                            Image = _image,
                            Url = _url,
                            CreationTime = dateTime
                        };

                        // If this is the first add - backup it
                        if (adCount == 0 && page == 1)
                            backupAd = newAd;

                        // If this is the first add in new category
                        // save it
                        if (latestAd == "new")
                        {
                            return new AdSearchResult()
                            {
                                Url = cleanUrl,
                                Ads = new List<AdInfo>() { newAd },
                                LatestAdUrl = _url
                            };
                        }

                        ads.Add(newAd);

                        // Increment Ad count
                        adCount++;
                    }
                    #endregion

                    #region Return ads
                    // If last know Ad was found return all ads
                    if (adFound)
                        return new AdSearchResult()
                        {
                            Url = cleanUrl,
                            Ads = ads,
                            LatestAdUrl = ads.Count == 0 ? latestAd : ads[0].Url
                        };

                    // Reset Ad Count
                    adCount = 0;

                    // Increment page 
                    page++;
                    #endregion
                }
                LoggingService.Log(Source, $"Ad wasnt found! Defaulting to latest ad.", ConsoleColor.Red);
                return new AdSearchResult() { Url = cleanUrl, LatestAdUrl = backupAd.Url, Ads = new List<AdInfo>() { backupAd } };
            }
            catch (Exception e)
            {
                LoggingService.Log(Source, e.ToString());
                return new AdSearchResult();
            }
        }
        #endregion

        #region Get Page
        public async Task<HtmlDocument> ReturnHtmlDocumentFromUrl(string url)
        {
            while (true)
            {
                try
                {
                    var webClient = new WebClient();
                    webClient.Headers.Add("Cache-Control", "no-cache");
                    webClient.CachePolicy = new RequestCachePolicy(RequestCacheLevel.BypassCache);

                    webClient.Encoding = Encoding.UTF8;
                    var htmlDocument = new HtmlDocument();
                    var htmlPage = webClient.DownloadString(url);
                    htmlDocument.LoadHtml(htmlPage);
                    return htmlDocument;
                }
                catch (Exception e)
                {
                    LoggingService.Log(Source, $"Error: {e.ToString()}\n" +
                        $"occurred while trying to load url: {url}\n");
                    continue;
                }
            }
        }
        #endregion

        #region CreateAdEmbed
        private async Task<Embed> CreateAdEmbed(string title, string description, string? imageUrl = null, Color? color = null, DateTime? timestamp = null)
        {
            var embed = new EmbedBuilder()
            {
                Title = title,
                Description = description,
                Color = color == null ? Color.DarkTeal : (Color)color,
                Timestamp = timestamp == null ? DateTime.Now : timestamp,
                ThumbnailUrl = imageUrl
            };
            return embed.Build();
        }
        #endregion#

        #region TruncateAtWord
        public async Task<string> TruncateAtWord(string input, int length, string url)
        {
            try
            {
                if (input == null || input.Length < length)
                    return input;

                // Backslash '\'
                char backslash = '\u005c';

                // Replace forbidden text
                input = input
                    .Replace("\r\n", "_NEWLINE_")
                    .Replace("/", "")
                    .Replace(backslash.ToString()
                    .Replace("*", "")
                    .Replace("{", "")
                    .Replace("}", "")
                    , "");

                input = input.Replace("_NEWLINE_", "\r\n");

                int iNextSpace = input.LastIndexOf(" ", length, StringComparison.Ordinal);
                return string.Format($"{input.Substring(0, (iNextSpace > 0) ? iNextSpace : length).Trim()}…");
            }
            catch (Exception e)
            {
                LoggingService.Log(Source, $"Error: {e.ToString()}\noccurred while trying to truncate description of: {url}");
                return "Error!!";
            }
        }
        #endregion

        #region Classes
        private class Category
        {
            public string Token { get; set; }
            public string Url { get; set; }
            public string Latest { get; set; }
        }

        private class OlxGuildConfig
        {
            public ulong GuildId;
            public ulong OlxChannel;
            public List<string> Categories;
        }

        private class AdInfo
        {
            public string Title { get; set; }
            public string Price { get; set; }
            public string Location { get; set; }
            public string Description { get; set; }
            public string Image { get; set; }
            public string Url { get; set; }
            public DateTime? CreationTime { get; set; }
        }

        private class AdSearchResult
        {
            public string Url;
            public string LatestAdUrl;
            public List<AdInfo> Ads;
        }
        #endregion
    }
}
