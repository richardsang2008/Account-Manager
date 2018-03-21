using PokemonGoGUI.AccountScheduler;
using PokemonGoGUI.GoManager;
using PokemonGoGUI.ProxyManager;
using System.Collections.Generic;

namespace PokemonGoGUI.Models
{
    public class ProgramExportModel
    {
        public List<Manager> Managers { get; set; }
        public ProxyHandler ProxyHandler { get; set; }
        public List<Scheduler> Schedulers { get; set; }
        public List<HashKey> HashKeys { get; set; }
        public bool SPF { get; set; }
        public bool ShowWelcomeMessage { get; set; }
        public bool AutoUpdate { get; set; }
    }

    public class HashKey {
        public string Key;
        public string KeyInfo;
        public bool IsValide;
    }
}
