using Newtonsoft.Json;
using POGOProtos.Data;
using POGOProtos.Inventory.Item;
using PokemonGoGUI.Enums;
using System;
using System.Collections.Generic;

namespace PokemonGoGUI.Models
{
    public class AccountExportModel
    {
        [JsonProperty("type")]
        public AuthType Type { get; set; }
        [JsonProperty("username")]
        public string Username { get; set; }
        [JsonProperty("password")]
        public string Password { get; set; }
        [JsonProperty("level")]
        public int Level { get; set; }
        [JsonProperty("pokedex")]
        public List<PokedexEntryExportModel> Pokedex { get; set; }
        [JsonProperty("items")]
        public List<ItemDataExportModel> Items { get; set; }
        [JsonProperty("pokemon")]
        public List<PokemonDataExportModel> Pokemon { get; set; }
        [JsonProperty("eggs")]
        public List<EggDataExportModel> Eggs { get; set; }
        [JsonProperty("exportTime")]
        public DateTime ExportTime { get; set; }

        public AccountExportModel()
        {
            ExportTime = DateTime.Now;
        }
    }

    public class PokedexEntryExportModel
    {
        [JsonProperty("id")]
        public int Id { get; set; }
        [JsonProperty("name")]
        public string Name { get; set; }
        [JsonProperty("timesEncountered")]
        public int TimesEncountered { get; set; }
        [JsonProperty("timesCaught")]
        public int TimesCaught { get; set; }

        public PokedexEntryExportModel(PokedexEntry entry)
        {
            Id = (int)entry.PokemonId;
            Name = entry.PokemonId.ToString();
            TimesEncountered = entry.TimesEncountered;
            TimesCaught = entry.TimesCaptured;
        }
    }

    public class ItemDataExportModel
    {
        [JsonProperty("itemName")]
        public string ItemName { get; set; }
        [JsonProperty("count")]
        public int Count { get; set; }

        public ItemDataExportModel(ItemData itemData)
        {
            ItemName = itemData.ItemId.ToString().Replace("Item", "");
            Count = itemData.Count;
        }
    }

    public class PokemonDataExportModel
    {
        [JsonProperty("pokedexEntry")]
        public int PokedexEntry { get; set; }
        [JsonProperty("pokemonName")]
        public string PokemonName { get; set; }
        [JsonProperty("cp")]
        public int CP { get; set; }
        [JsonProperty("iv")]
        public double IV { get; set; }

        public PokemonDataExportModel(PokemonData pokemon, double iv)
        {
            PokedexEntry = (int)pokemon.PokemonId;
            PokemonName = pokemon.PokemonId.ToString();
            CP = pokemon.Cp;
            IV = iv;
        }
    }

    public class EggDataExportModel
    {
        [JsonProperty("targetDistance")]
        public double TargetDistance { get; set; }
        [JsonProperty("id")]
        public ulong Id { get; set; }

        public EggDataExportModel(PokemonData pokemon)
        {
            TargetDistance = pokemon.EggKmWalkedTarget;
            Id = pokemon.Id;
        }
    }
}
