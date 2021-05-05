using Discord;
using Discord_Bot.DataStrucs;
using Discord_Bot.Handlers;
using Discord_Bot.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Discord_Bot.Modules
{
    public class CryptoModule
    {
        public Dictionary<string, Crypto> cryptoCurrencies = new Dictionary<string, Crypto>();
        public async Task<Task> Initialize()
        {
            Thread cryptoThread = new Thread(async() => 
            {
                // Get thread name for logging
                string threadName = Thread.CurrentThread.Name;
                while (true)
                {
                    try
                    {
                        // Create CWS
                        ClientWebSocket ws = new ClientWebSocket();

                        // Get URI
                        Uri uri = new Uri("wss://ws.bitstamp.net", UriKind.Absolute);

                        // Connect to WS
                        await ws.ConnectAsync(uri, new CancellationToken());

                        // Currencies to which we want to subscribe | More info in the documentation: https://www.bitstamp.net/websocket/v2/
                        string[] currencies = new string[] { "btcusd", "ethusd" };

                        foreach (var currency in currencies)
                        {
                            // Encode with UTF8
                            var encoded = Encoding.UTF8.GetBytes("{\"event\": \"bts:subscribe\",\"data\": {\"channel\": \"live_trades_" + currency + "\"}}");

                            // Send WS Message
                            await ws.SendAsync(new ArraySegment<byte>(encoded, 0, encoded.Length), WebSocketMessageType.Text, true, new CancellationToken());
                        }

                        // Start waiting for replies
                        while (true)
                        {
                            ArraySegment<Byte> buffer = new ArraySegment<byte>(new byte[8192]);
                            WebSocketReceiveResult result = null;
                            using (var ms = new MemoryStream())
                            {
                                do
                                {
                                    // Receive replie
                                    result = await ws.ReceiveAsync(buffer, CancellationToken.None);

                                    // Save to Memory Stream
                                    ms.Write(buffer.Array, buffer.Offset, buffer.Count);
                                }
                                while (!result.EndOfMessage);

                                // Set index 0 as MS position
                                ms.Seek(0, SeekOrigin.Begin);
                                using (var reader = new StreamReader(ms, Encoding.UTF8))
                                {
                                    // Parse data to JObject
                                    JObject json = JsonConvert.DeserializeObject<JObject>(reader.ReadToEnd());

                                    // More info about Events in the documentation: https://www.bitstamp.net/websocket/v2/
                                    if (json["event"].ToString() == "bts:subscription_succeeded")
                                    {
                                        LoggingService.Log(threadName, $"Subscribed to {json["channel"]}");
                                    }
                                    else if (json["event"].ToString() == "bts:request_reconnect")
                                    {
                                        LoggingService.Log(threadName, "Reconnect requested");

                                        // Close WS Connection
                                        await ws.CloseAsync(new WebSocketCloseStatus(), "", new CancellationToken());

                                        // Wait 5s
                                        Thread.Sleep(5000);

                                        // Connect to WS
                                        await ws.ConnectAsync(uri, new CancellationToken());
                                    }
                                    else
                                    {
                                        // Get Crypto Price as double
                                        var price = json["data"]["price"].ToObject<double>();
                                        // Get Crypto Name as string
                                        var key = json["channel"].ToString().Replace("live_trades_", "");

                                        // If cryptoCurrencies doesn't contains Crypto Name
                                        if (!cryptoCurrencies.ContainsKey(key))
                                        {
                                            // Add Crypto to Dictionary
                                            cryptoCurrencies.Add(key, new Crypto() { cryptoName = key, price = price });
                                            continue;
                                        }

                                        // Update Crypto
                                        cryptoCurrencies[key] = new Crypto() { cryptoName = key, price = price };
                                    }
                                }
                            }
                        }
                    }
                    catch
                    {
                        // If an error occurred - start all over
                        continue;
                    }
                }
            });
            cryptoThread.Name = "Crypto";
            cryptoThread.Start();
            LoggingService.Log(cryptoThread.Name, "Crypto module initialized");

            return Task.CompletedTask;
        }

        public async Task<Embed> GetPriceAsync(string cryptoName = null)
        {
            // Get Instance of cryptoCurrencies
            // (so it doesn't change while we run this function)
            var currentCrypto = cryptoCurrencies.OrderBy(x => x.Value.price).Reverse().ToDictionary(x => x.Key, x => x.Value);

            // If no Crypto info is found
            if (currentCrypto.Count == 0)
                return await EmbedHandler.CreateErrorEmbed("Crypto, Information", $"We are waiting for the information. Please try again later!");

            // Create StringBuilder
            StringBuilder sb = new StringBuilder();
            sb.Append("**Here is what we found:**\n==================\n");

            // Count of currencies we found
            int count = 0;
            foreach (var crypto in currentCrypto)
            {
                // Get Crypto Data
                var cryptoData = crypto.Value;

                // If Crypto name is specified and it doesn't match - continue
                if (cryptoName != null && cryptoName.ToLower() != cryptoData.cryptoName.ToLower())
                    continue;

                // Give Crypto name/price
                sb.Append($"{crypto.Value.cryptoName.ToUpper()}: ${crypto.Value.price}\n");
                // Increment crypto count
                count++;
            }
            sb.Append($"==================\nHaven't seen the coin you are looking for? Please contact the developers to add it.");

            // If no currencies were found
            if(count == 0)
                return await EmbedHandler.CreateErrorEmbed("Crypto, Information", $"Nothing was found!");

            return await EmbedHandler.CreateBasicEmbed("Crypto, Information", $"{sb.ToString()}", "https://external-content.duckduckgo.com/iu/?u=https%3A%2F%2Fi.ytimg.com%2Fvi%2FV1j5lzT2pVU%2Fmaxresdefault.jpg");
        }
    }
}
