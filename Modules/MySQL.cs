using Discord;
using Discord.WebSocket;
using Discord_Bot.DataStrucs;
using Discord_Bot.Handlers;
using Discord_Bot.Services;
using Microsoft.Extensions.DependencyInjection;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Discord_Bot.Modules
{
    public class MySQL
    {
        public MySqlConnection connection;
        private CommandHandler _commandHandler;
        private DiscordSocketClient _client;
        public MySQL(IServiceProvider _services)
        {
            _commandHandler = _services.GetRequiredService<CommandHandler>();
            _client = _services.GetRequiredService<DiscordSocketClient>();

            Initialize();
        }

        public void Initialize()
        {
            var config = GlobalData.Config;     
            try
            {
                //Create MySQL Connection String
                var connStr = new MySqlConnectionStringBuilder()
                {
                    Server = config.DB_Server,
                    Port = uint.Parse(config.DB_Port),
                    UserID = config.DB_User,
                    Password = config.DB_Password,
                    Database = config.DB_Database
                };

                connection = new MySqlConnection(connStr.GetConnectionString(true));
                connection.StateChange += Connection_StateChange;

                //Connect to the Database
                connection.Open();

                //Create thread for updating the cache
                Thread guildConfigUpdate = new Thread(new ThreadStart(UpdateGuildConfigs));
                guildConfigUpdate.Start();
            }
            catch (Exception ex)
            {
                LoggingService.Log("MySQL", "An exception was caught! Error message:\n" + ex.Message, ConsoleColor.Red);
                Thread.Sleep(-1);
            }
        }

        private void Connection_StateChange(object sender, System.Data.StateChangeEventArgs e)
        {
            LoggingService.Log("MySQL", $"MySQL Connection State: {e.CurrentState}");
        }

        private void UpdateGuildConfigs()
        {
            while (true)
            {
                Dictionary<ulong, GuildConfig> Configs = new Dictionary<ulong, GuildConfig>();

                //Go thru all guilds the Bot is in
                foreach (var guild in _client.Guilds)
                {
                    try
                    {
                        //If the Guild has a config in the Database
                        if (GuildHasConfig(guild))
                        {
                            //Get the config for the Guild
                            var config = GetGuildConfig(guild);

                            //Add the config to the dictionary
                            Configs.Add(guild.Id, config);
                        }
                        else
                        {
                            //Create new config for the Guild
                            CreateNewGuildConfig(guild);

                            //Create a custom Embed
                            List<EmbedFieldBuilder> fields = new List<EmbedFieldBuilder>();
                            fields.Add(new EmbedFieldBuilder
                            {
                                Name = "**I'm sorry for being late**",
                                Value = $"We had some technical difficulties.\n" +
                                $"Everything should be normal by now.",
                                IsInline = false
                            });

                            fields.Add(new EmbedFieldBuilder
                            {
                                Name = "**Please Note**",
                                Value = $"By default, {guild.DefaultChannel.Mention} is the default bot channel.\n" +
                                $"If you want to change it, type {GlobalData.Config.defaultPrefix}whitelist add #YourTextChannel",
                                IsInline = false
                            });

                            //Send the Embed in the Guild
                            Task.Run(async () =>
                            {
                                await guild.DefaultChannel.SendMessageAsync(embed: await EmbedHandler.CreateCustomEmbed(
                                    guild: guild,
                                    embedTitle: "Oh oh..",
                                    fields: fields,
                                    color: Color.DarkTeal,
                                    footer: $"Thank you for choosing {guild.CurrentUser.Username}"
                                ));
                            });
                            
                        }
                    }
                    catch (Exception e)
                    {
                        //If an error is caught log it
                        LoggingService.Log("UGC", e.Message, ConsoleColor.Red);
                    }
                }

                //LoggingService.Log("UGC", $"Updated all guild configs ({Configs.Count})");
                Console.Title = $"{DateTime.Now} - Updated all guild configs ({Configs.Count})";
                GlobalData.GuildConfigs = Configs;

                Thread.Sleep(GlobalData.Config.cacheUpdateTime);
            }
        }

        #region Config Functions
        /// <summary>
        /// Creates a config for the specified Guild
        /// </summary>
        public void CreateNewGuildConfig(IGuild guild)
        {
            var sqlCommands = new string[]
            {
                $"INSERT INTO Guilds VALUES('{guild.Id}')",
                $"INSERT INTO GuildConfigurable VALUES ('{guild.Id}', '{GlobalData.Config.defaultPrefix}', '{guild.DefaultChannelId}', false, 100, null)"
            };
            foreach (var sql in sqlCommands)
            {
                using (var cmd = new MySqlCommand(sql, connection))
                {
                    cmd.ExecuteNonQuery();
                }
            }
        }

        /// <summary>
        /// Deletes the Guild config
        /// </summary>
        public void RemoveGuildConfig(IGuild guild)
        {
            var sqlCommands = new string[]
            {
                $"DELETE FROM Guilds WHERE guildId = '{guild.Id}'",
                $"DELETE FROM GuildConfigurable WHERE guildId = '{guild.Id}'"
            };

            foreach (var sql in sqlCommands)
            {
                using (var cmd = new MySqlCommand(sql, connection))
                {
                    cmd.ExecuteNonQuery();
                }
            }
        }

        /// <summary>
        /// Gets the config for the specified Guild
        /// </summary>
        /// <returns>The config as GuildConfig</returns>
        private GuildConfig GetGuildConfig(IGuild guild)
        {
            var guildConfig = new GuildConfig();
            var command = $"SELECT * FROM GuildConfigurable WHERE guildId = '{guild.Id}'";
            using (var cmd = new MySqlCommand(command, connection))
            {
                using (var data_reader = cmd.ExecuteReader())
                {
                    while (data_reader.Read())
                    {
                        guildConfig.prefix = data_reader.GetValue(1).ToString();
                        guildConfig.whitelistedChannels = Array.ConvertAll(data_reader.GetValue(2).ToString().Split(';'), ulong.Parse).ToList();
                        guildConfig.isLooping = (bool)data_reader.GetValue(3);
                        guildConfig.volume = (int)data_reader.GetValue(4);
                        var playlistData = data_reader.GetValue(5);
                        if (!playlistData.Equals(DBNull.Value))
                            guildConfig.playlists = (JArray)JsonConvert.DeserializeObject((string)playlistData);
                    }
                }
            }
            return guildConfig;
        }

        /// <summary>
        /// Update Guild config
        /// </summary>
        /// <param name="guild">The Guild you want to update</param>
        /// <param name="whatToUpdate">The name of the colum in the Database</param>
        /// <param name="value">The new value</param>
        public void UpdateGuildConfig(IGuild guild, string whatToUpdate, string value)
        {
            string command = $"UPDATE GuildConfigurable SET {whatToUpdate} = '{value}' WHERE guildId = '{guild.Id}'";
            using(MySqlCommand cmd = new MySqlCommand(command, connection))
            {
                cmd.ExecuteNonQuery();
                cmd.Dispose();
            }
        }

        /// <summary>
        /// Checks if the Guild has a config
        /// </summary>
        /// <returns>Boolen (true or false)</returns>
        public bool GuildHasConfig(IGuild guild)
        {
            var command = $"SELECT EXISTS(SELECT * FROM GuildConfigurable WHERE guildId = '{guild.Id}')";
            using (var cmd = new MySqlCommand(command, connection))
            {
                using (var data_reader = cmd.ExecuteReader())
                {
                    while (data_reader.Read())
                    {
                        if (data_reader.GetValue(0).ToString() == "1")
                            return true;
                    }
                }
            }
            return false;
        }
        #endregion
    }
}
