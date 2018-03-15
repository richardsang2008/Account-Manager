using PokemonGoGUI.Extensions;
using System;

namespace PokemonGoGUI.Models
{
    [Serializable]
    public class GoProxy : ProxyEx
    {
        public int MaxConcurrentFails { get; set; }
        public int CurrentConcurrentFails { get; set; }
        public bool IsBanned { get; set; }
        public int MaxAccounts { get; set; }
        public int CurrentAccounts { get; set; }

        public GoProxy()
        {
            MaxConcurrentFails = 3;
            MaxAccounts = 1;
        }
    }
}
