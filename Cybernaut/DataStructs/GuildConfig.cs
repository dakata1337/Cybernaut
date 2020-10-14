using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using Victoria;

namespace Cybernaut.DataStructs
{
    class GuildConfig
    {
        public string Prefix { get; set; }
        public List<ulong> whitelistedChannels { get; set; }
        public List<JObject> Playlists { get; set; }
        public ulong AuthRole { get; set; }
        public bool AuthEnabled { get; set; }
        public int volume { get; set; }
        public bool islooping { get; set; }
    }
}