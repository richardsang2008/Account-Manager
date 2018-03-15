using POGOProtos.Enums;
using PokemonGoGUI.Enums;
using System;

namespace PokemonGoGUI.Models
{
    [Serializable]
    public class TransferSetting
    {
        public PokemonId Id { get; set; }
        public bool Transfer { get; set; }
        public TransferType Type { get; set; }
        public int KeepMax { get; set; }
        public int MinCP { get; set; }
        public int IVPercent { get; set; }
        public TransferSetting()
        {
            Id = PokemonId.Missingno;
            Type = TransferType.KeepStrongestX;
            KeepMax = 1;
            MinCP = 0;
            IVPercent = 80;
        }

        public string Name
        {
            get
            {
                return Id.ToString();
            }
        }
    }
}
