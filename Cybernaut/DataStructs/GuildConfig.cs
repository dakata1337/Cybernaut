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
        public bool GiveRoleOnJoin { get; set; }
        public ulong RoleOnJoin { get; set; }
        public bool RequireCAPTCHA { get; set; }
        public int volume { get; set; }
        public bool islooping { get; set; }
        public JObject[] mutedUsers { get; set; }
    }
}