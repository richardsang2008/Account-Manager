#region using directives

using Newtonsoft.Json;
using POGOLib.Official;
using POGOLib.Official.Exceptions;
using POGOLib.Official.Logging;
using POGOLib.Official.LoginProviders;
using POGOLib.Official.Net;
using POGOLib.Official.Net.Authentication;
using POGOLib.Official.Net.Authentication.Data;
using POGOLib.Official.Net.Captcha;
using POGOLib.Official.Util.Device;
using POGOLib.Official.Util.Hash;
using POGOProtos.Data;
using POGOProtos.Networking.Requests.Messages;
using POGOProtos.Networking.Responses;
using PokemonGoGUI.Enums;
using PokemonGoGUI.Exceptions;
using PokemonGoGUI.Extensions;
using PokemonGoGUI.GoManager;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using static POGOProtos.Networking.Envelopes.Signature.Types;

#endregion

namespace PokemonGoGUI
{
    public class Client : IDisposable
    {
        public Version VersionStr;
        public uint AppVersion;
        public Session ClientSession;
        public bool LoggedIn = false;
        public GetPlayerMessage.Types.PlayerLocale PlayerLocale;
        private DeviceWrapper ClientDeviceWrapper;
        public Manager ClientManager;
        private string RessourcesFolder;
        private CancellationTokenSource CancellationTokenSource;
        private ILoginProvider LoginProvider;

        public Client()
        {
            VersionStr = new Version("0.91.2");
            AppVersion = 9100;
            RessourcesFolder = $"data/{VersionStr.ToString()}/";
        }

        public void Logout()
        {
            if (!LoggedIn)
                return;

            if (ClientManager.AccountState == AccountState.Conecting)
                ClientManager.AccountState = AccountState.Good;

            LoggedIn = false;
            ClientSession.AssetDigestUpdated -= OnAssetDisgestReceived;
            ClientSession.ItemTemplatesUpdated -= OnItemTemplatesReceived;
            ClientSession.UrlsUpdated -= OnDownloadUrlsReceived;
            ClientSession.RemoteConfigVersionUpdated -= OnLocalConfigVersionReceived;
            ClientSession.AccessTokenUpdated -= SessionAccessTokenUpdated;
            ClientSession.CaptchaReceived -= SessionOnCaptchaReceived;
            ClientSession.InventoryUpdate -= SessionInventoryUpdate;
            ClientSession.MapUpdate -= SessionMapUpdate;
            ClientSession.CheckAwardedBadgesReceived -= OnCheckAwardedBadgesReceived;
            ClientSession.HatchedEggsReceived -= OnHatchedEggsReceived;
            ClientSession.TemporalBanReceived -= TempBanReceived;
            ClientSession.BuddyWalked -= OnBuddyWalked;
            ClientSession.InboxDataReceived -= OnInboxDataReceived;

            if (ClientSession.State != SessionState.Stopped)
                ClientSession.Shutdown();

            ClientSession.Dispose();
            Dispose();
        }

        private void LoggerFucntion(LogLevel logLevel, string message)
        {
            var logtype = LoggerTypes.Debug;
            switch (logLevel)
            {
                case LogLevel.Warn:
                    logtype = LoggerTypes.Warning;
                    break;
                case LogLevel.Error:
                    logtype = LoggerTypes.FatalError;
                    break;
                case LogLevel.Info:
                    logtype = LoggerTypes.Info;
                    break;
                case LogLevel.Notice:
                    logtype = LoggerTypes.Success;
                    break;
            }

            ClientManager.LogCaller(new LoggerEventArgs(message, logtype));
        }

        public async Task<MethodResult<bool>> DoLogin(Manager manager)
        {
            SetSettings(manager);

            try
            {
                var msgStr = "Session couldn't start up.";
                LoggedIn = false;
                CancellationTokenSource = new CancellationTokenSource();

                return await Task.Run(async () =>
                {
                    try
                    {
                        if (!(Configuration.Hasher is PokeHashHasher))
                        {
                            // By default Configuration.Hasher is LegacyHasher type  (see Configuration.cs in the pogolib source code)
                            // -> So this comparation only will run once.
                            if (ClientManager.UserSettings.UseOnlyOneKey)
                            {
                                Configuration.Hasher = new PokeHashHasher(ClientManager.UserSettings.AuthAPIKey);
                                Configuration.HasherUrl = ClientManager.UserSettings.HashHost;
                                Configuration.HashEndpoint = ClientManager.UserSettings.HashEndpoint;
                            }
                            else
                            {
                                //Need valid keys but this send all
                                Configuration.Hasher = new PokeHashHasher(ClientManager.UserSettings.HashKeys.ToArray());
                            }
                        }

                        // TODO: make this configurable. To avoid bans (may be with a checkbox in hash keys tab).
                        //Configuration.IgnoreHashVersion = true;
                        int humanizeUsed = ClientManager.CalculateDelay(ClientManager.UserSettings.GeneralDelay, ClientManager.UserSettings.GeneralDelayRandom) + ClientManager.CalculateDelay(ClientManager.UserSettings.DelayBetweenPlayerActions, ClientManager.UserSettings.PlayerActionDelayRandom);
                        Configuration.ThrottleDifference = ClientManager.UserSettings.EnableHumanization ? humanizeUsed : ClientManager.UserSettings.APIThrottles;
                        VersionStr = Configuration.Hasher.PokemonVersion;
                        AppVersion = Configuration.Hasher.AppVersion;
                        Configuration.EnableHeartbeat = ClientManager.UserSettings.UsePOGOLibHeartbeat;
                        Configuration.IgnoreHashSemafore = ClientManager.UserSettings.IgnoreHashSemafore;
                        Configuration.IgnoreRPCSemafore = ClientManager.UserSettings.IgnoreRPCSemafore;

                        switch (ClientManager.UserSettings.AuthType)
                        {
                            case AuthType.Google:
                                LoginProvider = new GoogleLoginProvider(ClientManager.UserSettings.Username, ClientManager.UserSettings.Password);
                                break;
                            case AuthType.Ptc:
                                LoginProvider = new PtcLoginProvider(ClientManager.UserSettings.Username, ClientManager.UserSettings.Password, ClientManager.UserSettings.Proxy.AsWebProxy());
                                break;
                            default:
                                throw new ArgumentException("Login provider must be either \"google\" or \"ptc\".");
                        }

                        ClientSession = await GetSession(LoginProvider, ClientManager.UserSettings.Latitude, ClientManager.UserSettings.Longitude, true);

                        ClientSession.AssetDigestUpdated += OnAssetDisgestReceived;
                        ClientSession.ItemTemplatesUpdated += OnItemTemplatesReceived;
                        ClientSession.UrlsUpdated += OnDownloadUrlsReceived;
                        ClientSession.RemoteConfigVersionUpdated += OnLocalConfigVersionReceived;
                        ClientSession.AccessTokenUpdated += SessionAccessTokenUpdated;
                        ClientSession.CaptchaReceived += SessionOnCaptchaReceived;
                        ClientSession.InventoryUpdate += SessionInventoryUpdate;
                        ClientSession.MapUpdate += SessionMapUpdate;
                        ClientSession.CheckAwardedBadgesReceived += OnCheckAwardedBadgesReceived;
                        ClientSession.HatchedEggsReceived += OnHatchedEggsReceived;
                        ClientSession.TemporalBanReceived += TempBanReceived;
                        ClientSession.BuddyWalked += OnBuddyWalked;
                        ClientSession.InboxDataReceived += OnInboxDataReceived;

                        ClientSession.Logger.RegisterLogOutput(LoggerFucntion);

                        ClientSession.ManageRessources = ClientManager.UserSettings.DownloadResources;
                        ClientManager.LogCaller(new LoggerEventArgs("Succefully added all events to the client.", LoggerTypes.Debug));

                        if (await ClientSession.StartupAsync())
                        {
                            LoggedIn = true;
                            msgStr = "Successfully logged into server.";

                            if (ClientSession.Player.Warn)
                            {
                                ClientManager.AccountState = AccountState.Flagged;
                                ClientManager.LogCaller(new LoggerEventArgs("The account is flagged.", LoggerTypes.Warning));
                                msgStr = "The account is flagged.";

                                if (ClientManager.UserSettings.StopAtMinAccountState == AccountState.Flagged)
                                {
                                    ClientManager.Stop();
                                }
                            }

                            if (ClientSession.Player.Banned)
                            {
                                ClientManager.AccountState = AccountState.PermanentBan;
                                ClientManager.LogCaller(new LoggerEventArgs("The account is banned.", LoggerTypes.FatalError));
                                ClientManager.Stop();
                                msgStr = "The account is banned.";
                            }

                            SaveAccessToken(ClientSession.AccessToken);
                        }
                    }
                    catch (HashVersionMismatchException ex)
                    {
                        ClientManager.LogCaller(new LoggerEventArgs(ex.Message, LoggerTypes.Warning));
                        ClientManager.Stop();
                    }
                    catch (APIBadRequestException ex)
                    {
                        if (ClientManager.AccountState != AccountState.CaptchaReceived || ClientManager.AccountState != AccountState.HashIssues)
                            ClientManager.AccountState = AccountState.TemporalBan;
                        ClientManager.LogCaller(new LoggerEventArgs($"API {ex.Message}, this account is maybe banned ...", LoggerTypes.Warning));
                    }
                    catch (InvalidPlatformException)// ex
                    {
                        ClientManager.LogCaller(new LoggerEventArgs("Invalid Platform or token session refresh, continue  ...", LoggerTypes.Warning));
                    }
                    catch (SessionInvalidatedException)// ex
                    {
                        ClientManager.LogCaller(new LoggerEventArgs("Session Invalidated or token session refresh, continue ...", LoggerTypes.Warning));
                    }
                    catch (SessionUnknowException)
                    {
                        ClientManager.AccountState = AccountState.Unknown;
                        msgStr = "Skipping request. Restarting ...";
                    }
                    catch (ArgumentOutOfRangeException ex)
                    {
                        ClientManager.AccountState = AccountState.Unknown;
                        ClientManager.LogCaller(new LoggerEventArgs("Skipping request", LoggerTypes.Exception, ex));
                        msgStr = "Skipping request";
                    }
                    catch (SessionStateException ex)
                    {
                        if (ClientSession.State == SessionState.TemporalBanned)
                        {
                            ClientManager.Stop();
                            msgStr = ex.Message;
                        }
                        else
                            ClientManager.LogCaller(new LoggerEventArgs(ex.Message, LoggerTypes.Warning));
                    }
                    catch (PtcLoginException) // poex
                    {
                        ClientManager.Stop();
                        ClientManager.LogCaller(new LoggerEventArgs("Ptc server offline. Please try again later.", LoggerTypes.Warning));
                        msgStr = "Ptc server offline.";
                    }
                    catch (AccountNotVerifiedException) // anvex
                    {
                        ClientManager.Stop();
                        ClientManager.LogCaller(new LoggerEventArgs("Account not verified. Stopping ...", LoggerTypes.Warning));
                        ClientManager.AccountState = Enums.AccountState.NotVerified;
                        msgStr = "Account not verified.";
                    }
                    catch (WebException wex)
                    {
                        ClientManager.Stop();

                        if (wex.Status == WebExceptionStatus.Timeout)
                        {
                            if (String.IsNullOrEmpty(ClientManager.Proxy))
                            {
                                ClientManager.LogCaller(new LoggerEventArgs("Login request has timed out.", LoggerTypes.Warning));
                            }
                            else
                            {
                                ClientManager._proxyIssue = true;
                                ClientManager.LogCaller(new LoggerEventArgs("Login request has timed out. Possible bad proxy.", LoggerTypes.ProxyIssue));
                            }

                            msgStr = "Request has timed out.";
                        }

                        if (!String.IsNullOrEmpty(ClientManager.Proxy))
                        {
                            if (wex.Status == WebExceptionStatus.ConnectionClosed)
                            {
                                ClientManager._proxyIssue = true;
                                ClientManager.LogCaller(new LoggerEventArgs("Potential http proxy detected. Only https proxies will work.", LoggerTypes.ProxyIssue));

                                msgStr = "Http proxy detected";
                            }
                            else if (wex.Status == WebExceptionStatus.ConnectFailure || wex.Status == WebExceptionStatus.ProtocolError || wex.Status == WebExceptionStatus.ReceiveFailure
                                || wex.Status == WebExceptionStatus.ServerProtocolViolation)
                            {
                                ClientManager._proxyIssue = true;
                                ClientManager.LogCaller(new LoggerEventArgs("Proxy is offline", LoggerTypes.ProxyIssue));

                                msgStr = "Proxy is offline";
                            }
                        }

                        ClientManager._proxyIssue |= !String.IsNullOrEmpty(ClientManager.Proxy);

                        ClientManager.LogCaller(new LoggerEventArgs("Failed to login due to request error", LoggerTypes.Exception, wex.InnerException));

                        msgStr = "Failed to login due to request error";
                    }
                    catch (TaskCanceledException) // tce
                    {
                        ClientManager.AccountState = AccountState.Unknown;
                        ClientManager.Stop();

                        if (String.IsNullOrEmpty(ClientManager.Proxy))
                        {
                            ClientManager.LogCaller(new LoggerEventArgs("Login request has timed out", LoggerTypes.Warning));
                        }
                        else
                        {
                            ClientManager._proxyIssue = true;
                            ClientManager.LogCaller(new LoggerEventArgs("Login request has timed out. Possible bad proxy", LoggerTypes.ProxyIssue));
                        }

                        msgStr = "Login request has timed out";
                    }
                    catch (OperationCanceledException ex)
                    {
                        ClientManager.AccountState = AccountState.Unknown;
                        msgStr = $"OperationCanceledException. {ex}";
                    }
                    catch (InvalidCredentialsException icex)
                    {
                        //Puts stopping log before other log.
                        ClientManager.Stop();
                        ClientManager.LogCaller(new LoggerEventArgs("Invalid credentials or account lockout. Stopping bot...", LoggerTypes.Warning, icex));

                        msgStr = "Username or password incorrect";
                    }
                    catch (IPBannedException) // ipex
                    {
                        if (ClientManager.UserSettings.StopOnIPBan)
                        {
                            ClientManager.Stop();
                        }

                        string message = String.Empty;

                        if (!String.IsNullOrEmpty(ClientManager.Proxy))
                        {
                            if (ClientManager.CurrentProxy != null)
                            {
                                ClientManager.ProxyHandler.MarkProxy(ClientManager.CurrentProxy, true);
                            }

                            message = "Proxy IP is banned.";
                        }
                        else
                        {
                            message = "IP address is banned.";
                        }

                        ClientManager._proxyIssue = true;

                        ClientManager.LogCaller(new LoggerEventArgs(message, LoggerTypes.ProxyIssue));

                        msgStr = message;
                    }
                    catch (GoogleLoginException glex)
                    {
                        ClientManager.Stop();
                        ClientManager.LogCaller(new LoggerEventArgs(glex.Message, LoggerTypes.Warning));

                        msgStr = "Failed to login";
                    }
                    catch (ArgumentNullException ex) // anex
                    {
                        //ClientManager.AccountState = AccountState.TemporalBan;
                        ClientManager.LogCaller(new LoggerEventArgs(ex.Message, LoggerTypes.Warning));
                        ClientManager.Stop();
                        msgStr = "Argument Null Exception.";
                    }
                    catch (PokeHashException ex)
                    {
                        ClientManager.AccountState = AccountState.HashIssues;
                        msgStr = "Hash issues";
                        ClientManager.LogCaller(new LoggerEventArgs("Hash issues", LoggerTypes.Warning, ex));
                    }
                    catch (Exception ex)
                    {
                        ClientManager.Stop();
                        ClientManager.LogCaller(new LoggerEventArgs("Failed to login", LoggerTypes.Exception, ex));
                        msgStr = "Failed to login";
                    }

                    return new MethodResult<bool>()
                    {
                        Success = LoggedIn,
                        Message = msgStr,
                    };
                }, CancellationTokenSource.Token);
            }
            catch (Exception ex)
            {
                ClientManager.AccountState = AccountState.Unknown;
                return new MethodResult<bool>
                {
                    Message = "Unknown error." + ex
                };
            }
        }

        private void OnBuddyWalked(object sender, GetBuddyWalkedResponse data)
        {
            ClientManager.LogCaller(new LoggerEventArgs($"Buddy Walked CandyID: {data.FamilyCandyId}, Candy Count: {data.CandyEarnedCount}", LoggerTypes.Buddy));
        }

        private void OnInboxDataReceived(object sender, GetInboxResponse data)
        {
            // Inbox data 
        }

        private void OnAssetDisgestReceived(object sender, List<AssetDigestEntry> data)
        {
            if (!Directory.Exists(RessourcesFolder))
                Directory.CreateDirectory(RessourcesFolder);

            var filename = RessourcesFolder + ClientManager.UserSettings.DeviceId + "_AD.json";

            try
            {
                Task.Run(() => File.WriteAllText(filename, Serializer.ToJson(data)));
            }
            catch (Exception)
            {
                ClientManager.LogCaller(new LoggerEventArgs("AssetDigests could not be saved sucessfully", LoggerTypes.Warning));
            }
        }

        private void OnItemTemplatesReceived(object sender, List<DownloadItemTemplatesResponse.Types.ItemTemplate> data)
        {
            if (!Directory.Exists(RessourcesFolder))
                Directory.CreateDirectory(RessourcesFolder);

            var filename = RessourcesFolder + ClientManager.UserSettings.DeviceId + "_IT.json";

            try
            {
                Task.Run(() => File.WriteAllText(filename, Serializer.ToJson(data)));
            }
            catch (Exception)
            {
                ClientManager.LogCaller(new LoggerEventArgs("ItemTemplates could not be saved sucessfully", LoggerTypes.Warning));
            }
        }

        private void OnDownloadUrlsReceived(object sender, List<DownloadUrlEntry> data)
        {
            if (!Directory.Exists(RessourcesFolder))
                Directory.CreateDirectory(RessourcesFolder);

            var filename = RessourcesFolder + ClientManager.UserSettings.DeviceId + "_UR.json";

            try
            {
                Task.Run(() => File.WriteAllText(filename, Serializer.ToJson(data)));
            }
            catch (Exception)
            {
                ClientManager.LogCaller(new LoggerEventArgs("Urls could not be saved sucessfully", LoggerTypes.Warning));
            }
        }

        private void OnLocalConfigVersionReceived(object sender, DownloadRemoteConfigVersionResponse data)
        {
            if (!Directory.Exists(RessourcesFolder))
                Directory.CreateDirectory(RessourcesFolder);

            var filename = RessourcesFolder + ClientManager.UserSettings.DeviceId + "_LCV.json";

            try
            {
                Task.Run(() => File.WriteAllText(filename, Serializer.ToJson(data)));
            }
            catch (Exception)
            {
                ClientManager.LogCaller(new LoggerEventArgs("LocalConfigVersion could not be saved sucessfully", LoggerTypes.Warning));
            }
        }

        private void SessionMapUpdate(object sender, EventArgs e)
        {
            //Map Update
        }

        public void SessionOnCaptchaReceived(object sender, CaptchaEventArgs e)
        {
            AccountState accountState = ClientManager.AccountState;

            ClientManager.AccountState = AccountState.CaptchaReceived;

            ClientManager.LogCaller(new LoggerEventArgs("Captcha received.", LoggerTypes.Warning));

            ClientManager.LogCaller(new LoggerEventArgs("Bot paused VerifyChallenge...", LoggerTypes.Captcha));

            bool solved = ClientManager.CaptchaSolver.SolveCaptcha(this, e.CaptchaUrl).Result;

            if (solved)
            {
                ClientManager.LogCaller(new LoggerEventArgs("Unpausing bot Challenge finished...", LoggerTypes.Captcha));
                ClientManager.AccountState = accountState;
                ClientManager.Restart();

                return;
            }

            ClientManager.Stop();
        }

        private void SessionInventoryUpdate(object sender, EventArgs e)
        {
            //TODO: review needed here            
            //ClientManager.UpdateInventory(InventoryRefresh.All); // <- this line should be the unique line updating the inventory
        }

        private void OnHatchedEggsReceived(object sender, GetHatchedEggsResponse hatchedEggResponse)
        {
            ClientManager.LogCaller(new LoggerEventArgs(String.Format("Egg hatched: {0}, Candy awarded: {1}, XP: {2}, Egg km walked: {3:0.00}, Stardust awarded: {4} Eggs: {5}.", hatchedEggResponse.PokemonId, hatchedEggResponse.CandyAwarded, hatchedEggResponse.ExperienceAwarded, hatchedEggResponse.EggKmWalked, hatchedEggResponse.StardustAwarded, hatchedEggResponse.HatchedPokemon.Count), LoggerTypes.HatchedEggs));
        }

        private void OnCheckAwardedBadgesReceived(object sender, CheckAwardedBadgesResponse e)
        {
            ClientManager.LogCaller(new LoggerEventArgs(String.Format("Avatar template ids: {0}, Awarded badge levels: {1}, Awarded badges: {2}.", e.AvatarTemplateIds.Count, e.AwardedBadgeLevels.Count, e.AwardedBadges.Count), LoggerTypes.AwardedBadges));
        }

        private void SessionAccessTokenUpdated(object sender, EventArgs e)
        {
            SaveAccessToken(ClientSession.AccessToken);
        }

        private void TempBanReceived(object sender, EventArgs e)
        {
            if (ClientManager.AccountState != AccountState.CaptchaReceived || ClientManager.AccountState != AccountState.HashIssues)
                ClientManager.AccountState = AccountState.TemporalBan;
            ClientManager.Stop();
        }

        public void SetSettings(Manager manager)
        {
            ClientManager = manager;

            Dictionary<string, string> Header = new Dictionary<string, string>()
            {
                {"11.1.0", "CFNetwork/889.3 Darwin/17.2.0"},
                {"11.2.0", "CFNetwork/893.10 Darwin/17.3.0"},
                {"11.2.5", "CFNetwork/893.14.2 Darwin/17.4.0"},
                {"11.3.0", "CFNetwork/897.1 Darwin/17.5.0"}
            };

            ClientDeviceWrapper = new DeviceWrapper
            {
                UserAgent = $"pokemongo/1 {Header[ClientManager.UserSettings.FirmwareType]}",
                DeviceInfo = new DeviceInfo
                {
                    DeviceId = ClientManager.UserSettings.DeviceId,
                    DeviceBrand = ClientManager.UserSettings.DeviceBrand,
                    DeviceModel = ClientManager.UserSettings.DeviceModel,
                    DeviceModelBoot = ClientManager.UserSettings.DeviceModelBoot,
                    HardwareManufacturer = ClientManager.UserSettings.HardwareManufacturer,
                    HardwareModel = ClientManager.UserSettings.HardwareModel,
                    FirmwareBrand = ClientManager.UserSettings.FirmwareBrand,
                    FirmwareType = ClientManager.UserSettings.FirmwareType
                },
                Proxy = ClientManager.UserSettings.Proxy.AsWebProxy()
            };

            PlayerLocale = new GetPlayerMessage.Types.PlayerLocale
            {
                Country = ClientManager.UserSettings.Country,
                Language = ClientManager.UserSettings.Language,
                Timezone = ClientManager.UserSettings.TimeZone
            };
        }

        private void SaveAccessToken(AccessToken accessToken)
        {
            if (accessToken == null || accessToken.Token == null || accessToken.IsExpired)
                return;
            var fileName = Path.Combine(Directory.GetCurrentDirectory(), "Cache", $"{accessToken.Uid}.json");
            File.WriteAllText(fileName, JsonConvert.SerializeObject(accessToken, Formatting.Indented));
        }

        /// <summary>
        /// Login to PokémonGo and return an authenticated <see cref="ClientSession" />.
        /// </summary>
        /// <param name="loginProvider">Provider must be PTC or Google.</param>
        /// <param name="initLat">The initial latitude.</param>
        /// <param name="initLong">The initial longitude.</param>
        /// <param name="mayCache">Can we cache the <see cref="AccessToken" /> to a local file?</param>
        private async Task<Session> GetSession(ILoginProvider loginProvider, double initLat, double initLong, bool mayCache = false)
        {
            LoginProvider = loginProvider;

            Session session = null;
            if (mayCache)
            {
                var cacheDir = Path.Combine(Directory.GetCurrentDirectory(), "Cache");
                var fileName = Path.Combine(cacheDir, $"{loginProvider.UserId}-{loginProvider.ProviderId}.json");

                if (!Directory.Exists(cacheDir))
                    Directory.CreateDirectory(cacheDir);

                if (File.Exists(fileName))
                {
                    var accessToken = JsonConvert.DeserializeObject<AccessToken>(File.ReadAllText(fileName));

                    if (accessToken != null || !string.IsNullOrEmpty(accessToken.Token) || !accessToken.IsExpired)
                    {
                        session = await Login.GetSession(loginProvider, accessToken, initLat, initLong, ClientDeviceWrapper, PlayerLocale);
                        LoadResources(session);
                        return session;
                    }
                }
            }

            session = await Login.GetSession(loginProvider, initLat, initLong, ClientDeviceWrapper, PlayerLocale);
            LoadResources(session);
            return session;
        }

        private void LoadResources(Session session)
        {
            //My files resources here  
            var filename = RessourcesFolder + ClientManager.UserSettings.DeviceId + "_IT.json";
            if (File.Exists(filename))
                session.Templates.ItemTemplates = Serializer.FromJson<List<DownloadItemTemplatesResponse.Types.ItemTemplate>>(File.ReadAllText(filename));
            filename = RessourcesFolder + ClientManager.UserSettings.DeviceId + "_UR.json";
            if (File.Exists(filename))
                session.Templates.DownloadUrls = Serializer.FromJson<List<DownloadUrlEntry>>(File.ReadAllText(filename));
            filename = RessourcesFolder + ClientManager.UserSettings.DeviceId + "_AD.json";
            if (File.Exists(filename))
                session.Templates.AssetDigests = Serializer.FromJson<List<AssetDigestEntry>>(File.ReadAllText(filename));
            filename = RessourcesFolder + ClientManager.UserSettings.DeviceId + "_LCV.json";
            if (File.Exists(filename))
                session.Templates.LocalConfigVersion = Serializer.FromJson<DownloadRemoteConfigVersionResponse>(File.ReadAllText(filename));
        }

        public void CleanLocalAccesToken()
        {
            var cacheDir = Path.Combine(Directory.GetCurrentDirectory(), "Cache");
            var fileName = Path.Combine(cacheDir, $"{LoginProvider.UserId}-{LoginProvider.ProviderId}.json");
            if (File.Exists(fileName))
                File.Delete(fileName);
        }

        #region IDisposable Support
        private bool disposedValue = false; // Pour détecter les appels redondants

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: supprimer l'état managé (objets managés).
                    if (CancellationTokenSource != null)
                    {
                        CancellationTokenSource.Dispose();
                        return;
                    }
                    ClientManager.LogCaller(new LoggerEventArgs("Session closed!", LoggerTypes.FatalError));
                }

                // TODO: libérer les ressources non managées (objets non managés) et remplacer un finaliseur ci-dessous.
                // TODO: définir les champs de grande taille avec la valeur Null.

                disposedValue = true;
            }
        }

        // TODO: remplacer un finaliseur seulement si la fonction Dispose(bool disposing) ci-dessus a du code pour libérer les ressources non managées.
        // ~Client() {
        //   // Ne modifiez pas ce code. Placez le code de nettoyage dans Dispose(bool disposing) ci-dessus.
        //   Dispose(false);
        // }

        // Ce code est ajouté pour implémenter correctement le modèle supprimable.
        public void Dispose()
        {
            // Ne modifiez pas ce code. Placez le code de nettoyage dans Dispose(bool disposing) ci-dessus.
            Dispose(true);
            // TODO: supprimer les marques de commentaire pour la ligne suivante si le finaliseur est remplacé ci-dessus.
            // GC.SuppressFinalize(this);
        }
        #endregion
    }
}
