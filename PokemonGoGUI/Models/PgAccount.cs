using System;
using Newtonsoft.Json;

namespace PokemonGoGUI.Models
{
    public class PgAccount
    {
        [JsonProperty("auth_service")]
        public string AuthService { get; set; }
        [JsonProperty("last_modified")]
        public string LastModified { get; set; }
        [JsonProperty("latitude")]
        public object Latitude { get; set; }
        [JsonProperty("longitude")]
        public object Longitude { get; set; }
        [JsonProperty("password")]
        public string Password { get; set; }
        [JsonProperty("rareless_scans")]
        public object RarelessScans { get; set; }
        [JsonProperty("shadowbanned")]
        public object Shadowbanned { get; set; }
        [JsonProperty("username")]
        public string Username { get; set; }
        [JsonProperty("system_id")]
        public string SystemId { get; set; }
        [JsonProperty("level")]
        public int Level { get; set; }
        [JsonProperty("reach_lvl30_datetime")]
        public DateTime? ReachLevel30DateTime{get; set;}
    }
}
