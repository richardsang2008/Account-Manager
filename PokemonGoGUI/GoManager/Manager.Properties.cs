using Newtonsoft.Json;
using POGOProtos.Data;
using POGOProtos.Data.Player;
using POGOProtos.Enums;
using POGOProtos.Inventory;
using POGOProtos.Inventory.Item;
using POGOProtos.Networking.Responses;
using POGOProtos.Settings.Master;
using PokemonGoGUI.AccountScheduler;
using PokemonGoGUI.Enums;
using PokemonGoGUI.Extensions;
using PokemonGoGUI.GoManager.Models;
using PokemonGoGUI.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace PokemonGoGUI.GoManager
{
    public partial class Manager
    {
        public byte[] LogHeaderSettings { get; set; }
        public AccountState AccountState { get; set; }
        public Settings UserSettings { get; set; }
        public Tracker Tracker { get; set; }
        public Scheduler AccountScheduler { get; set; }
        public PlayerStats Stats { get; set; }

        [JsonIgnore]
        public Dictionary<PokemonMove, MoveSettings> MoveSettings { get; private set; }
        [JsonIgnore]
        public Dictionary<BadgeType, BadgeSettings> BadgeSettings { get; private set; }
        [JsonIgnore]
        public Dictionary<ItemId, ItemSettings> ItemSettings { get; private set; }
        [JsonIgnore]
        public GymBattleSettings BattleSettings { get; private set; }
        [JsonIgnore]
        public PokemonUpgradeSettings UpgradeSettings { get; private set; }
        [JsonIgnore]
        public MoveSequenceSettings GetMoveSequenceSettings { get; private set; }
        [JsonIgnore]
        public EncounterSettings GetEncounterSettings { get; private set; }
        [JsonIgnore]
        public IapItemDisplay GetIapItemDisplay { get; private set; }
        [JsonIgnore]
        public IapSettings GetIapSettings { get; private set; }
        [JsonIgnore]
        public EquippedBadgeSettings GetEquippedBadgeSettings { get; private set; }
        [JsonIgnore]
        public QuestSettings GetQuestSettings { get; private set; }
        [JsonIgnore]
        public AvatarCustomizationSettings GetAvatarCustomizationSettings { get; private set; }
        [JsonIgnore]
        public FormSettings GetFormSettings { get; private set; }
        [JsonIgnore]
        public GenderSettings GetGenderSettings { get; private set; }
        [JsonIgnore]
        public GymBadgeGmtSettings GetGymBadgeGmtSettings { get; private set; }
        [JsonIgnore]
        public WeatherAffinity GetWeatherAffinity { get; private set; }
        [JsonIgnore]
        public WeatherBonus GetWeatherBonus { get; private set; }
        [JsonIgnore]
        public PokemonScaleSetting GetPokemonScaleSetting { get; private set; }
        [JsonIgnore]
        public TypeEffectiveSettings GetTypeEffectiveSettings { get; private set; }
        [JsonIgnore]
        public CameraSettings GetCameraSettings { get; private set; }
        [JsonIgnore]
        public GymLevelSettings GetGymLevelSettings { get; private set; }
        [JsonIgnore]
        public GetPlayerProfileResponse PlayerProfile { get; private set; }

        [JsonIgnore]
        public string SchedulerName
        {
            get
            {
                return AccountScheduler == null ? String.Empty : AccountScheduler.Name;
            }
        }

        [JsonIgnore]
        public int PokemonCaught
        {
            get
            {
                return Tracker == null ? 0 : Tracker.PokemonCaught;
            }
            set
            {
                Tracker.PokemonCaught = value;
            }
        }

        [JsonIgnore]
        public int PokestopsFarmed
        {
            get
            {
                return Tracker == null ? 0 : Tracker.PokestopsFarmed;
            }
            set
            {
                Tracker.PokestopsFarmed = value;
            }
        }

        [JsonIgnore]
        public string GroupName
        {
            get
            {
                return String.IsNullOrEmpty(UserSettings.GroupName) ? String.Empty : UserSettings.GroupName;
            }
        }

        [JsonIgnore]
        public string Proxy
        {
            get
            {
                var proxyEx = new ProxyEx
                {
                    Address = UserSettings.ProxyIP,
                    Port = UserSettings.ProxyPort,
                    Username = UserSettings.ProxyUsername,
                    Password = UserSettings.ProxyPassword
                };

                return proxyEx.ToString();
            }
        }

        [JsonIgnore]
        public BotState State { get; set; }

        [JsonIgnore]
        public PlayerData PlayerData { get { return _client?.ClientSession?.Player?.Data; } }

        [JsonIgnore]
        public List<Log> Logs { get; private set; }

        [JsonIgnore]
        public List<ItemData> Items { get; private set; } = new List<ItemData>();

        [JsonIgnore]
        public List<PokemonData> Pokemon { get; private set; } = new List<PokemonData>();

        [JsonIgnore]
        public List<PokedexEntry> Pokedex { get; private set; }  = new List<PokedexEntry>();

        [JsonIgnore]
        public List<Candy> PokemonCandy { get; private set; } = new List<Candy>();

        [JsonIgnore]
        public List<EggIncubator> Incubators { get; private set; } = new List<EggIncubator>();

        [JsonIgnore]
        public List<PokemonData> Eggs { get; private set; } = new List<PokemonData>();

        [JsonIgnore]
        public Dictionary<PokemonId, PokemonSettings> PokeSettings { get; private set; }

        [JsonIgnore]
        public PlayerLevelSettings LevelSettings { get; private set; }

        [JsonIgnore]
        public List<FarmLocation> FarmLocations { get; private set; }

        [JsonIgnore]
        public bool IsRunning { get; private set; }

        [JsonIgnore]
        public bool StartingUp { get; private set; }

        [JsonIgnore]
        public int TotalLogs
        {
            get
            {
                return Logs == null ? 0 : Logs.Count;
            }
        }

        [JsonIgnore]
        public string LastLogMessage
        {
            get
            {
                if (Logs == null || Logs.Count == 0)
                {
                    return String.Empty;
                }

                lock (Logs)
                {
                    string message = Logs.Last().Message;

                    return String.IsNullOrEmpty(message) ? String.Empty : message;
                }
            }
        }

        [JsonIgnore]
        public string AccountName
        {
            get
            {
                return UserSettings == null ? "???" : UserSettings.AccountName;
            }
        }

        [JsonIgnore]
        public int Level
        {
            get
            {
                return Stats == null ? 0 : Stats.Level;
            }
            set
            {
                Stats.Level = value;
            }
        }

        [JsonIgnore]
        public string Team
        {
            get
            {
                return Stats == null ? TeamColor.Neutral.ToString() : UserSettings.DefaultTeam;
            }
        }

        [JsonIgnore]
        public int MaxLevel
        {
            get
            {
                return UserSettings == null ? 0 : UserSettings.MaxLevel;
            }
        }

        [JsonIgnore]
        public string TillLevelUp
        {
            get
            {
                if(ExpPerHour == 0)
                {
                    return "Unknown";
                }

                long currentExp = Stats.Experience - Stats.PrevLevelXp;
                long required = Stats.NextLevelXp - Stats.PrevLevelXp;
                long needed = required - currentExp;

                int expPerHour = ExpPerHour;

                double totalHours = (double)needed / expPerHour;

                if(totalHours <= 0)
                {
                    return "Now";
                }

                TimeSpan time = TimeSpan.FromHours(totalHours);

                if(time.TotalHours < 1)
                {
                    return String.Format("{0:0}m {1:00}s", time.Minutes, time.Seconds);
                }

                return time.TotalHours >= 24 ? String.Format("{0:0}d {1:0}h {2:00}m", time.Days, time.Hours, time.Seconds) : String.Format("{0:0}h {1:00}m {2:00}s", time.Hours, time.Minutes, time.Seconds);
            }
        }

        [JsonIgnore]
        public string RemainingRunningTime
        {
            get
            {
                if (Math.Abs(MaxRuntime) < 0.0001) {
                    return "Unlimited";
                }

                double remainingHours = MaxRuntime - _runningStopwatch.Elapsed.TotalHours;
                TimeSpan time = TimeSpan.FromHours(remainingHours);


                if (time.TotalHours < 1)
                {
                    return String.Format("{0:0}m {1:00}s", time.Minutes, time.Seconds);
                }

                return time.TotalHours >= 24 ? String.Format("{0:0}d {1:0}h {2:00}m", time.Days, time.Hours, time.Seconds) : String.Format("{0:0}h {1:00}m {2:00}s", time.Hours, time.Minutes, time.Seconds);
            }
        }

        [JsonIgnore]
        public string ExpRatio
        {
            get
            {
                if (Stats == null)
                {
                    return "??/??";
                }

                long currentExp = Stats.Experience - Stats.PrevLevelXp;
                long required = Stats.NextLevelXp - Stats.PrevLevelXp;

                double ratio = 0;

                if (required != 0)
                {
                    ratio = (double)currentExp / required * 100;
                }

                return String.Format("{0}/{1} ({2:0.00}%)", currentExp, required, ratio);
            }
        }

        [JsonIgnore]
        public int MaxItemStorage
        {
            get
            {
                return PlayerData == null ? 350 : PlayerData.MaxItemStorage;
            }
        }

        [JsonIgnore]
        public int MaxPokemonStorage
        {
            get
            {
                return PlayerData == null ? 250 : PlayerData.MaxPokemonStorage;
            }
        }

        [JsonIgnore]
        public int TotalStardust
        {
            get
            {
                if(PlayerData == null || PlayerData.Currencies.Count == 0)
                {
                    return 0;
                }

                Currency stardust = PlayerData.Currencies.FirstOrDefault(x => x.Name == "STARDUST");
                return stardust.Amount == 0 ? 0 : stardust.Amount;
            }
        }

        [JsonIgnore]
        public int TotalPokeCoins
        {
            get
            {
                if (PlayerData == null || PlayerData.Currencies.Count == 0)
                {
                    return 0;
                }

                Currency pokecoins = PlayerData.Currencies.FirstOrDefault(x => x.Name == "POKECOIN");
                return pokecoins.Amount == 0 ? 0 : pokecoins.Amount;
            }
        }

        [JsonIgnore]
        public int ExpPerHour
        {
            get
            {
                double totalHours = _runningStopwatch.Elapsed.TotalHours;

                if (Math.Abs(totalHours) < 0.0001) {
                    return 0;
                }

                double expPerHour = ExpGained / totalHours;

                return (int)expPerHour;
            }
        }

        [JsonIgnore]
        public string RunningTime
        {
            get
            {
                //return String.Format("{0:c}", _runningStopwatch.Elapsed);
                return _runningStopwatch.Elapsed.ToString(@"dd\.hh\:mm\:ss");
            }
        }

        [JsonIgnore]
        public int ExpGained { get; set; } = 0;

        [JsonIgnore]
        public int TotalPokeStopExp { get; set; }

        [JsonIgnore]
        public double MaxRuntime
        {
            get
            {
                return UserSettings == null ? 0 : UserSettings.RunForHours;
            }
        }

        [JsonIgnore]
        public bool LuckyEggActive
        {
            get
            {
                if (_client.LoggedIn)
                    return _client.ClientSession.LuckyEggsUsed;
                else
                    return false;
            }
        }

        private Stopwatch _runningStopwatch = new Stopwatch();

        private void ExpIncrease(int amount)
        {
            ExpGained += amount;
            Stats.Experience += amount;
        }

        private int RemainingPokeballs()
        {
            int total = 0;

            ItemData data = Items.FirstOrDefault(x => x.ItemId == ItemId.ItemPokeBall);

            if(data != null)
            {
                total += data.Count;
            }

            data = Items.FirstOrDefault(x => x.ItemId == ItemId.ItemGreatBall);

            if (data != null)
            {
                total += data.Count;
            }
            data = Items.FirstOrDefault(x => x.ItemId == ItemId.ItemUltraBall);

            if (data != null)
            {
                total += data.Count;
            }

            data = Items.FirstOrDefault(x => x.ItemId == ItemId.ItemMasterBall);

            if (data != null)
            {
                total += data.Count;
            }

            data = Items.FirstOrDefault(x => x.ItemId == ItemId.ItemPremierBall);

            if (data != null)
            {
                total += data.Count;
            }

            return total;
        }

        private bool HasPokeballsLeft()
        {
            return RemainingPokeballs() > 0;
        }
    }
}
