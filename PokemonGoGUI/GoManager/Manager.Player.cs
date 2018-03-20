using POGOProtos.Data;
using POGOProtos.Inventory;
using POGOProtos.Networking.Responses;
using POGOProtos.Settings.Master;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PokemonGoGUI.Extensions;
using POGOProtos.Networking.Requests;
using POGOProtos.Networking.Requests.Messages;
using Google.Protobuf;
using PokemonGoGUI.Enums;
using POGOProtos.Enums;
using System.Net.Http;

namespace PokemonGoGUI.GoManager
{
    public partial class Manager
    {

        public async Task<MethodResult> UpdateDetails()
        {
            UpdateInventory(InventoryRefresh.All);

            LogCaller(new LoggerEventArgs("Updating details", LoggerTypes.Debug));

            await Task.Delay(CalculateDelay(UserSettings.GeneralDelay, UserSettings.GeneralDelayRandom));

            return new MethodResult
            {
                Success = true
            };
        }

        public async Task<MethodResult> ExportStats()
        {
            MethodResult result = await UpdateDetails();

            //Prevent API throttling
            await Task.Delay(500);

            if (!result.Success)
            {
                return result;
            }

            //Possible some objects were empty.
            var builder = new StringBuilder();
            builder.AppendLine("=== Trainer Stats ===");

            if (Stats != null && PlayerData != null)
            {
                builder.AppendLine(String.Format("Group: {0}", UserSettings.GroupName));
                builder.AppendLine(String.Format("Username: {0}", UserSettings.Username));
                builder.AppendLine(String.Format("Password: {0}", UserSettings.Password));
                builder.AppendLine(String.Format("Level: {0}", Stats.Level));
                builder.AppendLine(String.Format("Current Trainer Name: {0}", PlayerData.Username));
                builder.AppendLine(String.Format("Team: {0}", PlayerData.Team));
                builder.AppendLine(String.Format("Stardust: {0:N0}", TotalStardust));
                builder.AppendLine(String.Format("Unique Pokedex Entries: {0}", Stats.UniquePokedexEntries));
            }
            else
            {
                builder.AppendLine("Failed to grab stats");
            }

            builder.AppendLine();

            builder.AppendLine("=== Pokemon ===");

            if (Pokemon != null)
            {
                foreach (PokemonData pokemon in Pokemon.OrderByDescending(x => x.Cp))
                {
                    string candy = "Unknown";

                    MethodResult<PokemonSettings> pSettings = GetPokemonSetting(pokemon.PokemonId);

                    if (pSettings.Success)
                    {
                        Candy pCandy = PokemonCandy.FirstOrDefault(x => x.FamilyId == pSettings.Data.FamilyId);

                        if (pCandy != null)
                        {
                            candy = pCandy.Candy_.ToString("N0");
                        }
                    }

                    double perfectResult = CalculateIVPerfection(pokemon);
                    string iv = "Unknown";

                    iv = Math.Round(perfectResult, 2).ToString() + "%";

                    builder.AppendLine(String.Format("Pokemon: {0,-10} CP: {1, -5} IV: {2,-7} Primary: {3, -14} Secondary: {4, -14} Candy: {5}", pokemon.PokemonId, pokemon.Cp, iv, pokemon.Move1.ToString().Replace("Fast", ""), pokemon.Move2, candy));
                }
            }

            //Remove the hardcoded directory later
            try
            {
                string directoryName = "AccountStats";

                if (!Directory.Exists(directoryName))
                {
                    Directory.CreateDirectory(directoryName);
                }

                string fileName = UserSettings.Username.Split('@').First();

                string filePath = Path.Combine(directoryName, fileName) + ".txt";

                File.WriteAllText(filePath, builder.ToString());

                LogCaller(new LoggerEventArgs(String.Format("Finished exporting stats to file {0}", filePath), LoggerTypes.Info));

                return new MethodResult
                {
                    Message = "Success",
                    Success = true
                };
            }
            catch (Exception ex)
            {
                LogCaller(new LoggerEventArgs("Failed to export stats due to exception", LoggerTypes.Warning, ex));

                return new MethodResult();
            }
        }

        private async Task<MethodResult> ClaimLevelUpRewards(int level)
        {
            if (!UserSettings.ClaimLevelUpRewards || level < 2)
            {
                return new MethodResult();
            }

            if (!_client.LoggedIn)
            {
                MethodResult result = await AcLogin();

                if (!result.Success)
                {
                    return result;
                }
            }

            var response = await _client.ClientSession.RpcClient.SendRemoteProcedureCallAsync(new Request
            {
                RequestType = RequestType.LevelUpRewards,
                RequestMessage = new LevelUpRewardsMessage
                {
                    Level = level
                }.ToByteString()
            });

            if (response == null)
                return new MethodResult();

            LevelUpRewardsResponse levelUpRewardsResponse = null;

            levelUpRewardsResponse = LevelUpRewardsResponse.Parser.ParseFrom(response);
            string rewards = StringUtil.GetSummedFriendlyNameOfItemAwardList(levelUpRewardsResponse.ItemsAwarded);
            LogCaller(new LoggerEventArgs(String.Format("Grabbed rewards for level {0}. Rewards: {1}", level, rewards), LoggerTypes.LevelUp));

            if (level >= 30 && ManagerExportModel.EnablePGPool)
            {
                try
                {
                    using (var client = new HttpClient())
                    {
                        client.BaseAddress = new Uri(ManagerExportModel.PGPoolEndpoint);
                        var content = new StringContent("level=30&condition=good&accounts=ptc," + UserSettings.AccountName + "," + UserSettings.Password, Encoding.UTF8, "application/x-www-form-urlencoded");
                        
                        using(var request = new HttpRequestMessage(HttpMethod.Post, "account/add"))
                        {
                            request.Content = content;

                            await client.SendAsync(request).ContinueWith(async responseTask =>
                            {
                                var res = await responseTask.Result.Content.ReadAsStringAsync();
                                if (!res.Contains("Successfully added"))
                                {
                                    LogCaller(new LoggerEventArgs(String.Format(res), LoggerTypes.Info));
                                    LogCaller(new LoggerEventArgs(String.Format("Error Sending Account To PGPool!"), LoggerTypes.Warning));
                                    LogCaller(new LoggerEventArgs(String.Format("PGPool Response: {0}", responseTask.Result), LoggerTypes.Warning));
                                }
                                else
                                {
                                    LogCaller(new LoggerEventArgs(String.Format(res), LoggerTypes.Info));
                                    LogCaller(new LoggerEventArgs(String.Format("Account successfully sent to PGPool"), LoggerTypes.Info));
                                    Stop();
                                    //_mainForm.AddAccount("AAAA","BBBB");
                                }
                            });
                        }
                        
                    }
                    
                }
                catch (Exception ex)
                {
                    LogCaller(new LoggerEventArgs(String.Format(ex.Message), LoggerTypes.Warning));
                }
                
            }

            return new MethodResult
            {
                Success = true
            };
        }

        private async Task<MethodResult> GetPlayer(bool nobuddy = true, bool noinbox = true)
        {
            if (!_client.LoggedIn)
            {
                MethodResult result = await AcLogin();

                if (!result.Success)
                {
                    return result;
                }
            }

            var response = await _client.ClientSession.RpcClient.SendRemoteProcedureCallAsync(new Request
            {
                RequestType = RequestType.GetPlayer,
                RequestMessage = new GetPlayerMessage
                {
                    PlayerLocale = _client.PlayerLocale
                }.ToByteString()
            }, true, nobuddy, noinbox);

            if (response == null)
                return new MethodResult();

            var parsedResponse = GetPlayerResponse.Parser.ParseFrom(response);

            return new MethodResult
            {
                Success = true
            };
        }

        private async Task<MethodResult> GetPlayerProfile()
        {
            if (!_client.LoggedIn)
            {
                MethodResult result = await AcLogin();

                if (!result.Success)
                {
                    return result;
                }
            }

            var response = await _client.ClientSession.RpcClient.SendRemoteProcedureCallAsync(new Request
            {
                RequestType = RequestType.GetPlayerProfile,
                RequestMessage = new GetPlayerProfileMessage
                {
                    PlayerName = PlayerData.Username
                }.ToByteString()
            }, true, false, true);

            if (response == null)
                return new MethodResult();

            var parsedResponse = GetPlayerProfileResponse.Parser.ParseFrom(response);

            PlayerProfile = parsedResponse;

            return new MethodResult
            {
                Success = true
            };
        }

        private async Task<MethodResult> SetPlayerTeam(TeamColor team)
        {
            if (!_client.LoggedIn)
            {
                MethodResult result = await AcLogin();

                if (!result.Success)
                {
                    return result;
                }
            }

            var response = await _client.ClientSession.RpcClient.SendRemoteProcedureCallAsync(new Request
            {
                RequestType = RequestType.SetPlayerTeam,
                RequestMessage = new SetPlayerTeamMessage
                {
                    Team = team
                }.ToByteString()
            }, true);

            if (response == null)
                return new MethodResult();

            SetPlayerTeamResponse setPlayerTeamResponse = SetPlayerTeamResponse.Parser.ParseFrom(response);

            LogCaller(new LoggerEventArgs($"Set player Team completion request wasn't successful. Team: {team.ToString()}", LoggerTypes.Success));

            // not nedded pogolib set this auto
            //_client.ClientSession.Player.Data = setPlayerTeamResponse.PlayerData;

            return new MethodResult
            {
                Success = true
            };
        }

        public async Task<MethodResult> SetBuddyPokemon(PokemonData pokemon)
        {
            if (!_client.LoggedIn)
            {
                MethodResult result = await AcLogin();

                if (!result.Success)
                {
                    return result;
                }
            }

            var response = await _client.ClientSession.RpcClient.SendRemoteProcedureCallAsync(new Request
            {
                RequestType = RequestType.SetBuddyPokemon,
                RequestMessage = new SetBuddyPokemonMessage
                {
                    PokemonId = pokemon.Id
                }.ToByteString()
            }, true);

            if (response == null)
                return new MethodResult();

            SetBuddyPokemonResponse setBuddyPokemonResponse = SetBuddyPokemonResponse.Parser.ParseFrom(response);

            switch (setBuddyPokemonResponse.Result)
            {
                case SetBuddyPokemonResponse.Types.Result.ErrorInvalidPokemon:
                    LogCaller(new LoggerEventArgs($"Faill to set buddy pokemon, reason: {setBuddyPokemonResponse.Result.ToString()}", LoggerTypes.Info));
                    break;
                case SetBuddyPokemonResponse.Types.Result.ErrorPokemonDeployed:
                    LogCaller(new LoggerEventArgs($"Faill to set buddy pokemon, reason: {setBuddyPokemonResponse.Result.ToString()}", LoggerTypes.Info));
                    break;
                case SetBuddyPokemonResponse.Types.Result.ErrorPokemonIsEgg:
                    LogCaller(new LoggerEventArgs($"Faill to set buddy pokemon, reason: {setBuddyPokemonResponse.Result.ToString()}", LoggerTypes.Info));
                    break;
                case SetBuddyPokemonResponse.Types.Result.ErrorPokemonNotOwned:
                    LogCaller(new LoggerEventArgs($"Faill to set buddy pokemon, reason: {setBuddyPokemonResponse.Result.ToString()}", LoggerTypes.Info));
                    break;
                case SetBuddyPokemonResponse.Types.Result.Success:
                    PlayerData.BuddyPokemon = new BuddyPokemon
                    {
                        Id = pokemon.Id,
                        //LastKmAwarded = PokeSettings[pokemon.PokemonId].KmBuddyDistance,
                        //StartKmWalked = PokeSettings[pokemon.PokemonId].KmDistanceToHatch
                    };

                    setBuddyPokemonResponse.UpdatedBuddy = PlayerData.BuddyPokemon;

                    LogCaller(new LoggerEventArgs($"Set buddy pokemon completion request wasn't successful. pokemon buddy: {pokemon.PokemonId.ToString()}", LoggerTypes.Buddy));

                    UpdateInventory(InventoryRefresh.Pokemon);

                    return new MethodResult
                    {
                        Success = true
                    };
                case SetBuddyPokemonResponse.Types.Result.Unest:
                    LogCaller(new LoggerEventArgs($"Faill to set buddy pokemon, reason: {setBuddyPokemonResponse.Result.ToString()}", LoggerTypes.Info));
                    break;
            }
            return new MethodResult();
        }
    }
}
