using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace Cybernaut.DataStructs
{ 
    class CAPTCHAs
    {
        public string captchaAnswer { get; set; }
        public ulong userID { get; set; }
    }

    class UserInfo
    {
        public ulong Id { get; set; }
        public ulong GuildId { get; set; }
        public DateTime ExpiresOn { get; set; }
    }

    class Playlists
    {
        public JObject playlists { get; set; }
    }

    class Playlist
    {
        public string name { get; set; }
        public JObject[] songs { get; set; }
    }

    class Songs
    {
        public JObject song { get; set; }
    }

    class Song
    {
        public string name { get; set; }
        public string url { get; set; }
    }
}
