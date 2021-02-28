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
        public Dictionary<string, JObject> cryptocurrencies = new Dictionary<string, JObject>();
        public void Initialize()
        {
            Thread webSocketThread = new Thread(async() =>
            {
                //Thread name (used for logging)
                string threadName = Thread.CurrentThread.Name;

                //Create Web Socket
                ClientWebSocket ws = new ClientWebSocket();

                //Convert String to URI
                Uri uri = new Uri("wss://ws.bitstamp.net", UriKind.Absolute);

                //Connecto to WS
                await ws.ConnectAsync(uri, new CancellationToken());

                //Currencies to which we want to subscribe | Documentation: https://www.bitstamp.net/websocket/v2/
                string[] currencies = new string[] { "btcusd", "ethusd" };

                //Subscribe to currencies
                foreach (var currency in currencies)
                {
                    //Subscribe message 
                    string data = "{\"event\": \"bts:subscribe\",\"data\": {\"channel\": \"live_trades_" + currency + "\"}}";

                    //Encode with UTF8
                    var encoded = Encoding.UTF8.GetBytes(data);

                    //Send WS Message
                    await ws.SendAsync(new ArraySegment<Byte>(encoded, 0, encoded.Length), WebSocketMessageType.Text, true, new CancellationToken());
                }
                
                //Start waiting for reponse from WS
                while(true)
                {
                    //Create buffer array
                    ArraySegment<Byte> buffer = new ArraySegment<byte>(new Byte[8192]);

                    WebSocketReceiveResult result = null;

                    using (var ms = new MemoryStream())
                    {
                        do
                        {
                            //Receive Data
                            result = await ws.ReceiveAsync(buffer, CancellationToken.None);

                            //Write to memory
                            ms.Write(buffer.Array, buffer.Offset, result.Count);
                        }
                        while(!result.EndOfMessage);

                        ms.Seek(0, SeekOrigin.Begin);

                        using (var reader = new StreamReader(ms, Encoding.UTF8))
                        {
                            //Convert the string to JSON
                            JObject response = JsonConvert.DeserializeObject<JObject>(reader.ReadToEnd());

                            if(response["event"].ToString() == "bts:subscription_succeeded")
                            {
                                //Log successful subscription
                                LoggingService.Log(threadName, $"Subscribed to {response["channel"]}");
                            }
                            //The documentation says if we recieve "bts:request_reconnect" we need to reconnect | Documentation: https://www.bitstamp.net/websocket/v2/
                            else if(response["event"].ToString() == "bts:request_reconnect")
                            {
                                //Log reconnect request
                                LoggingService.Log(threadName, "Reconnect requested");

                                //Close the Web Socket
                                await ws.CloseAsync(new WebSocketCloseStatus(), "", new CancellationToken());

                                //Wait 5s
                                Thread.Sleep(5000);

                                //Connecto to the Web Socket
                                await ws.ConnectAsync(uri, new CancellationToken());
                            }
                            else
                            {
                                //Display data
                                //LoggingService.Log(threadName, $"{response["channel"].ToString()} => {response["data"]["price"].ToString()}USD");

                                //Process data
                                //If the Dictionary doesn't contain the key - add it
                                if(!cryptocurrencies.ContainsKey((string)response["channel"]))
                                {
                                    cryptocurrencies.Add((string)response["channel"], response);
                                    continue;
                                }

                                //If the Dictionary does contain the key - update it
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

            //Create String Builder
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