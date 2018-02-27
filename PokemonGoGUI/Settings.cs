﻿using Newtonsoft.Json;
using POGOLib.Official.Util.Device;
using POGOProtos.Enums;
using POGOProtos.Inventory.Item;
using PokemonGoGUI.Enums;
using PokemonGoGUI.Extensions;
using PokemonGoGUI.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;

namespace PokemonGoGUI
{
    public class Settings
    {
        public List<string> HashKeys { get; set; }
        public bool UseOnlyOneKey { get; set; }
        public string AuthAPIKey { get; set; }
        public Uri HashHost { get; set; }
        public string HashEndpoint { get; set; }

        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double Altitude { get; set; }
        public string DeviceId { get; set; }
        public string DeviceBrand { get; set; }
        public string DeviceModel { get; set; }
        public string DeviceModelBoot { get; set; }
        public string HardwareManufacturer { get; set; }
        public string HardwareModel { get; set; }
        public string FirmwareBrand { get; set; }
        public string FirmwareType { get; set; }
        public string Country { get; set; }
        public string Language { get; set; }
        public string TimeZone { get; set; }
        public string POSIX { get; set; }

        public bool AllowManualCaptchaResolve { get; set; }
        public int ManualCaptchaTimeout { get; set; }
        public bool PlaySoundOnCaptcha { get; set; }
        public bool DisplayOnTop { get; set; }
        public bool Enable2Captcha { get; set; }
        public bool EnableAntiCaptcha { get; set; }
        public string AntiCaptchaAPIKey { get; set; }
        public string ProxyHostCaptcha { get; set; }
        public int ProxyPortCaptcha { get; set; }
        public bool EnableCaptchaSolutions { get; set; }
        public string CaptchaSolutionAPIKey { get; set; }
        public string CaptchaSolutionsSecretKey { get; set; }
        public int AutoCaptchaTimeout { get; set; }
        public int AutoCaptchaRetries { get; set; }
        public string TwoCaptchaAPIKey { get; set; }

        public bool AutoFavoritShiny { get; set; }
        public bool UseIncense { get; set; }
        public string DefaultTeam { get; set; }
        public double DisableCatchDelay { get; set; }
        public bool SpinGyms { get; set; }
        public bool GoOnlyToGyms { get; set; }
        public bool DeployPokemon { get; set; }
        public string GroupName { get; set; }
        public string AccountName { get; set; }
        public AuthType AuthType { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public bool MimicWalking { get; set; }
        public int WalkingSpeed { get; set; }
        public bool EncounterWhileWalking { get; set; }
        public int MaxTravelDistance { get; set; }
        public bool UseLuckyEgg { get; set; }
        public bool ClaimLevelUpRewards { get; set; }
        public int MinPokemonBeforeEvolve { get; set; }
        public bool RecycleItems { get; set; }
        public bool TransferPokemon { get; set; }
        public bool EvolvePokemon { get; set; }
        public bool UpgradePokemon { get; set; }
        public bool CatchPokemon { get; set; }
        public bool IncubateEggs { get; set; }
        public int MaxLevel { get; set; }
        public bool SPF { get; set; }

        public double SearchFortBelowPercent { get; set; }
        public int CatchPokemonDayLimit { get; set; }
        public int SpinPokestopsDayLimit { get; set; }
        public bool SnipeAllPokemonsNoInPokedex { get; set; }
        public double ForceEvolveAbovePercent { get; set; }
        public bool StopOnAPIUpdate { get; set; }

        public int MaxLogs { get; set; }
        public double RunForHours { get; set; }

        //Humanization
        public bool EnableHumanization { get; set; }
        public int InsideReticuleChance { get; set; }

        public int DelayBetweenPlayerActions { get; set; }
        public int PlayerActionDelayRandom { get; set; }

        public int DelayBetweenLocationUpdates { get; set; }
        public int LocationupdateDelayRandom { get; set; }

        public int GeneralDelay { get; set; }
        public int GeneralDelayRandom { get; set; }

        public double WalkingSpeedOffset { get; set; }
        //End Humanization

        public string ProxyIP { get; set; }
        public int ProxyPort { get; set; }
        public string ProxyUsername { get; set; }
        public string ProxyPassword { get; set; }
        public bool AutoRotateProxies { get; set; }
        public bool AutoRemoveOnStop { get; set; }

        public bool StopOnIPBan { get; set; }
        public int MaxFailBeforeReset { get; set; }
        public bool UseBerries { get; set; }
        public bool OnlyUnlimitedIncubator { get; set; }
        public bool TransferSlashPokemons { get; set; }
        public bool ShufflePokestops { get; set; }
        public bool GetArBonus { get; set; }
        public decimal ARBonusProximity { get; set; }
        public decimal ARBonusAwareness { get; set; }
        public bool CompleteTutorial { get; set; }
        public bool TransferAtOnce { get; set; }
        public bool ShowDebugLogs { get; set; }
        public bool DownloadResources { get; set; }

        public AccountState StopAtMinAccountState { get; set; }

        public ProxyEx Proxy
        {
            get
            {
                return new ProxyEx
                {
                    Address = ProxyIP,
                    Port = ProxyPort,
                    Username = ProxyUsername,
                    Password = ProxyPassword
                };
            }
        }

        public List<InventoryItemSetting> ItemSettings { get; set; }
        public List<TransferSetting> TransferSettings { get; set; }
        public List<CatchSetting> CatchSettings { get; set; }
        public List<EvolveSetting> EvolveSettings { get; set; }
        public List<UpgradeSetting> UpgradeSettings { get; set; }

        [JsonConstructor]
        public Settings(bool jsonConstructor = true)
        {
            LoadDefaults();
        }

        public Settings()
        {
            //Defaults
            LoadDefaults();
            RandomizeDevice();
            LoadInventorySettings();
            LoadCatchSettings();
            LoadEvolveSettings();
            LoadTransferSettings();
            LoadUpgradeSettings();
        }

        public void LoadDefaults()
        {
            GroupName = "Default";
            AuthType = AuthType.Ptc;
            MimicWalking = true;
            CatchPokemon = true;
            WalkingSpeed = 7;
            MaxTravelDistance = 1000;
            EncounterWhileWalking = false;
            EnableHumanization = false;
            InsideReticuleChance = 100;
            MinPokemonBeforeEvolve = 0;
            StopAtMinAccountState = AccountState.Unknown;
            DelayBetweenPlayerActions = 500;
            DelayBetweenLocationUpdates = 1000;
            GeneralDelay = 800;
            MaxLogs = 400;
            MaxFailBeforeReset = 3;
            StopOnIPBan = true;
            SearchFortBelowPercent = 1000;
            CatchPokemonDayLimit = 500;
            SpinPokestopsDayLimit = 700;
            ForceEvolveAbovePercent = 1000;
            StopOnAPIUpdate = true;
            SpinGyms = true;
            HashHost = new Uri("https://pokehash.buddyauth.com/");
            HashEndpoint = "api/v159_1/hash";
            AuthAPIKey = "XXXXXXXXXXXXXXXXXXXX";
            Latitude = 40.764665;
            Longitude = -73.973184;
            Country = "US";
            Language = "en";
            TimeZone = "America/New_York";
            POSIX = "en-us";
            DisableCatchDelay = 8;
            DownloadResources = true;
            AllowManualCaptchaResolve = true;
            ManualCaptchaTimeout = 120;
            PlaySoundOnCaptcha = true;
            DisplayOnTop = true;
            Enable2Captcha = false;
            EnableAntiCaptcha = false;
            ProxyPortCaptcha = 3128;
            EnableCaptchaSolutions = false;
            AutoCaptchaTimeout = 120;
            AutoCaptchaRetries = 3;
            DefaultTeam = "Neutral";
            ShowDebugLogs = false;
            GoOnlyToGyms = false;
            AutoFavoritShiny = true;
            SnipeAllPokemonsNoInPokedex = true;
            EncounterWhileWalking = true;
        }

        public void LoadCatchSettings()
        {
            CatchSettings = new List<CatchSetting>();

            foreach (PokemonId pokemon in Enum.GetValues(typeof(PokemonId)))
            {
                if (pokemon == PokemonId.Missingno)
                {
                    continue;
                }

                var cSettings = new CatchSetting
                {
                    Id = pokemon
                };

                CatchSettings.Add(cSettings);
            }
        }

        public void LoadInventorySettings()
        {
            ItemSettings = new List<InventoryItemSetting>();

            foreach (ItemId item in Enum.GetValues(typeof(ItemId)))
            {
                if (item == ItemId.ItemUnknown)
                {
                    continue;
                }

                var itemSetting = new InventoryItemSetting
                {
                    Id = item
                };

                ItemSettings.Add(itemSetting);
            }
        }

        public void LoadEvolveSettings()
        {
            EvolveSettings = new List<EvolveSetting>();

            foreach (PokemonId pokemon in Enum.GetValues(typeof(PokemonId)))
            {
                if (pokemon == PokemonId.Missingno)
                {
                    continue;
                }

                var setting = new EvolveSetting
                {
                    Id = pokemon
                };

                EvolveSettings.Add(setting);
            }
        }

        public void LoadTransferSettings()
        {
            TransferSettings = new List<TransferSetting>();

            foreach (PokemonId pokemon in Enum.GetValues(typeof(PokemonId)))
            {
                if (pokemon == PokemonId.Missingno)
                {
                    continue;
                }

                var setting = new TransferSetting
                {
                    Id = pokemon,
                    Transfer = true
                };

                TransferSettings.Add(setting);
            }
        }

        public void LoadUpgradeSettings()
        {
            UpgradeSettings = new List<UpgradeSetting>();

            foreach (PokemonId pokemon in Enum.GetValues(typeof(PokemonId)))
            {
                if (pokemon == PokemonId.Missingno)
                {
                    continue;
                }

                var setting = new UpgradeSetting
                {
                    Id = pokemon,
                    //Upgrade = true
                };

                UpgradeSettings.Add(setting);
            }
        }

        public void RandomizeDeviceId()
        {
            var device = DeviceInfoUtil.GetRandomDevice();
            DeviceId = device.DeviceInfo.DeviceId;
        }

        public void RandomizeDevice()
        {
            var device = DeviceInfoUtil.GetRandomDevice();
            DeviceId = device.DeviceInfo.DeviceId;
            DeviceBrand = device.DeviceInfo.DeviceBrand;
            DeviceModel = device.DeviceInfo.DeviceModel;
            DeviceModelBoot = device.DeviceInfo.DeviceModelBoot;
            HardwareManufacturer = device.DeviceInfo.HardwareManufacturer;
            HardwareModel = device.DeviceInfo.HardwareModel;
            FirmwareBrand = device.DeviceInfo.FirmwareBrand;
            FirmwareType = device.DeviceInfo.FirmwareType;
        }

        private byte RandomByte()
        {
            using (var randomizationProvider = new RNGCryptoServiceProvider())
            {
                var randomBytes = new byte[1];
                randomizationProvider.GetBytes(randomBytes);
                return randomBytes.Single();
            }
        }
    }
}
