using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord_Bot.DataStrucs;
using Discord_Bot.Handlers;
using Discord_Bot.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Cybernaut.Modules
{
    public class Crypto
    {
        public Crypto(IServiceProvider serviceProvider)
        {

        }
        public Dictionary<string, JObject> cryptocurrencies = new Dictionary<string, JObject>();
        public void Initialize()
        {
            Thread webSocketThread = new Thread(async() =>
            {
                string threadName = Thread.CurrentThread.Name;
                ClientWebSocket ws = new ClientWebSocket();
                Uri uri = new Uri("wss://ws.bitstamp.net", UriKind.Absolute);
                await ws.ConnectAsync(uri, new CancellationToken());

                string[] currencies = new string[] { "btcusd", "ethusd" };
                foreach (var currency in currencies)
                {
                    string data = "{\"event\": \"bts:subscribe\",\"data\": {\"channel\": \"live_trades_" + currency + "\"}}";
                    var encoded = Encoding.UTF8.GetBytes(data);
                    await ws.SendAsync(new ArraySegment<Byte>(encoded, 0, encoded.Length), WebSocketMessageType.Text, true, new CancellationToken());
                }
                

                while(true)
                {
                    ArraySegment<Byte> buffer = new ArraySegment<byte>(new Byte[8192]);

                    WebSocketReceiveResult result = null;

                    using (var ms = new MemoryStream())
                    {
                        do
                        {
                            result = await ws.ReceiveAsync(buffer, CancellationToken.None);
                            ms.Write(buffer.Array, buffer.Offset, result.Count);
                        }
                        while(!result.EndOfMessage);

                        ms.Seek(0, SeekOrigin.Begin);

                        using (var reader = new StreamReader(ms, Encoding.UTF8))
                        {
                            JObject response = JsonConvert.DeserializeObject<JObject>(reader.ReadToEnd());

                            if(response["event"].ToString() == "bts:subscription_succeeded")
                            {
                                LoggingService.Log(threadName, $"Subscribed to {response["channel"]}");
                            }
                            else if(response["event"].ToString() == "bts:request_reconnect")
                            {
                                LoggingService.Log(threadName, "Reconnect requested");
                                await ws.CloseAsync(new WebSocketCloseStatus(), "", new CancellationToken());
                                Thread.Sleep(5000);
                                await ws.ConnectAsync(uri, new CancellationToken());
                            }
                            else
                            {
                                //Display data
                                //LoggingService.Log(threadName, $"{response["channel"].ToString()} => {response["data"]["price"].ToString()}USD");

                                //Process data
                                if(!cryptocurrencies.ContainsKey((string)response["channel"]))
                                {
                                    cryptocurrencies.Add((string)response["channel"], response);
                                    continue;
                                }

                                cryptocurrencies[(string)response["channel"]] = response;
                            }
                        }
                    }
                }
            });
            webSocketThread.Name = "Crypto";
            webSocketThread.IsBackground = true;
            webSocketThread.Start();
        }

        public async Task<Embed> GetPricesAsync(string cryptoName)
        {
            //This prevents "Collection was modified; enumeration operation may not execute."
            var currentCrypto = cryptocurrencies;

            //Check if there is info about any currency
            if(currentCrypto.Count == 0)
                return await EmbedHandler.CreateErrorEmbed("Crypto, Information", $"We are waiting for the information. Please try again later!");

            StringBuilder sb = new StringBuilder();
            int prefixLength = 0;


            sb.Append("**Here is what we've got:\n**");
            prefixLength = prefixLength + sb.Length;

            foreach (var item in currentCrypto)
            {
                //If name is specified
                if(cryptoName != null)
                {
                    if(item.Key.ToLower().Contains(cryptoName.ToLower()))
                        sb.Append($"{item.Key.Replace("live_trades_","").ToUpper()} => ${item.Value["data"]["price"]}\n");
                }
                else
                {
                    sb.Append($"{item.Key.Replace("live_trades_","").ToUpper()} => ${item.Value["data"]["price"]}\n");
                }
            } 

            return await EmbedHandler.CreateBasicEmbed("Crypto, Information", $"{(sb.Length - prefixLength == 0 ? $"Nothing was found!" : sb.ToString())}");
        }
    }
}