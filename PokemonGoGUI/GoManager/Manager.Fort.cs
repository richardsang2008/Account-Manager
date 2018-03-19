using Google.Protobuf;
using POGOProtos.Inventory.Item;
using POGOProtos.Map.Fort;
using POGOProtos.Networking.Requests;
using POGOProtos.Networking.Requests.Messages;
using POGOProtos.Networking.Responses;
using PokemonGoGUI.Enums;
using PokemonGoGUI.Extensions;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace PokemonGoGUI.GoManager
{
    public partial class Manager
    {
        private async Task<MethodResult> SearchPokestop(FortData pokestop)
        {
            if (pokestop == null)
                return new MethodResult();

            FortSearchResponse fortResponse = null;
            const int maxFortAttempts = 5;

            string fort = pokestop.Type == FortType.Checkpoint ? "Fort" : "Gym";

            for (int i = 0; i < maxFortAttempts; i++)
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
                    RequestType = RequestType.FortSearch,
                    RequestMessage = new FortSearchMessage
                    {
                        FortId = pokestop.Id,
                        FortLatitude = pokestop.Latitude,
                        FortLongitude = pokestop.Longitude,
                        PlayerLatitude = _client.ClientSession.Player.Latitude,
                        PlayerLongitude = _client.ClientSession.Player.Longitude
                    }.ToByteString()
                });

                if (response == null)
                    return new MethodResult();

                fortResponse = FortSearchResponse.Parser.ParseFrom(response);

                switch (fortResponse.Result)
                {
                    case FortSearchResponse.Types.Result.ExceededDailyLimit:
                        AccountState = AccountState.SoftBan;
                        LogCaller(new LoggerEventArgs(String.Format("Failed to search {0}. Response: {1}. Stoping ...", fort, fortResponse.Result), LoggerTypes.Warning));
                        Stop();
                        break;
                    case FortSearchResponse.Types.Result.InCooldownPeriod:
                        LogCaller(new LoggerEventArgs(String.Format("Failed to search {0}. Response: {1}", fort, fortResponse.Result), LoggerTypes.Warning));
                        return new MethodResult();//break;
                    case FortSearchResponse.Types.Result.InventoryFull:
                        LogCaller(new LoggerEventArgs(String.Format("Failed to search {0}. Response: {1}", fort, fortResponse.Result), LoggerTypes.Warning));
                        //Recycle if inventory full
                        await RecycleFilteredItems();
                        await Task.Delay(CalculateDelay(UserSettings.GeneralDelay, UserSettings.GeneralDelayRandom));
                        return new MethodResult();
                    case FortSearchResponse.Types.Result.NoResultSet:
                        LogCaller(new LoggerEventArgs(String.Format("Failed to search {0}. Response: {1}", fort, fortResponse.Result), LoggerTypes.Warning));
                        break;
                    case FortSearchResponse.Types.Result.OutOfRange:
                        if (_potentialPokeStopBan)
                        {
                            if (AccountState != AccountState.SoftBan)
                            {
                                LogCaller(new LoggerEventArgs("Pokestop ban detected. Marking state", LoggerTypes.Warning));
                            }

                            AccountState = AccountState.SoftBan;

                            if (fortResponse.ExperienceAwarded != 0)
                            {
                                if (!_potentialPokemonBan && _fleeingPokemonResponses >= _fleeingPokemonUntilBan)
                                {
                                    LogCaller(new LoggerEventArgs("Potential pokemon ban detected. Setting flee count to 0 avoid false positives", LoggerTypes.Warning));

                                    _potentialPokemonBan = true;
                                    _fleeingPokemonResponses = 0;
                                }
                                else if (_fleeingPokemonResponses >= _fleeingPokemonUntilBan)
                                {
                                    //Already pokestop banned
                                    if (AccountState == AccountState.SoftBan)
                                    {
                                        _potentialPokemonBan = true;
                                        _potentialPokeStopBan = true;
                                    }

                                    if (AccountState != AccountState.SoftBan)
                                    {
                                        //Only occurs when out of range is found
                                        if (fortResponse.ExperienceAwarded == 0)
                                        {
                                            LogCaller(new LoggerEventArgs("Pokemon fleeing and failing to grab stops. Potential pokemon & pokestop ban or daily limit reached.", LoggerTypes.Warning));
                                        }
                                        else
                                        {
                                            LogCaller(new LoggerEventArgs("Pokemon fleeing, yet grabbing stops. Potential pokemon ban or daily limit reached.", LoggerTypes.Warning));
                                        }
                                    }

                                    if (UserSettings.StopAtMinAccountState == AccountState.SoftBan)
                                    {
                                        LogCaller(new LoggerEventArgs("Auto stopping bot ...", LoggerTypes.Info));

                                        Stop();
                                    }

                                    return new MethodResult
                                    {
                                        Message = "Bans detected",
                                    };
                                }
                            }
                        }
                        else //This error should never happen normally, so assume temp ban
                        {
                            if (!UserSettings.UseSoftBanBypass)
                            {
                                _failedPokestopResponse++;
                                LogCaller(new LoggerEventArgs($"Pokestop softban baypass disabled go to next...", LoggerTypes.Info));
                                return new MethodResult();
                            }

                            //by pass softban 
                            int bypass = UserSettings.SoftBanBypassTimes;

                            //Go to location again
                            //LogCaller(new LoggerEventArgs($"Pokestop potential softban baypass enabled go to location again {pokestop.Latitude}, {pokestop.Longitude}.", LoggerTypes.Debug));
                            //MethodResult move = UpdateLocation(new GeoCoordinate(pokestop.Latitude, pokestop.Longitude));

                            while (bypass > 0)
                            {
                                LogCaller(new LoggerEventArgs($"Pokestop potential softban baypass enabled #{bypass.ToString()}.", LoggerTypes.Info));
                                try
                                {
                                    if (!_client.LoggedIn)
                                    {
                                        MethodResult result = await AcLogin();

                                        if (!result.Success)
                                        {
                                            return result;
                                        }
                                    }

                                    var _response = await _client.ClientSession.RpcClient.SendRemoteProcedureCallAsync(new Request
                                    {
                                        RequestType = RequestType.FortSearch,
                                        RequestMessage = new FortSearchMessage
                                        {
                                            FortId = pokestop.Id,
                                            FortLatitude = pokestop.Latitude,
                                            FortLongitude = pokestop.Longitude,
                                            PlayerLatitude = _client.ClientSession.Player.Latitude,
                                            PlayerLongitude = _client.ClientSession.Player.Longitude
                                        }.ToByteString()
                                    });

                                    if (_response == null)
                                        return new MethodResult();

                                    fortResponse = FortSearchResponse.Parser.ParseFrom(_response);

                                    switch (fortResponse.Result)
                                    {
                                        case FortSearchResponse.Types.Result.ExceededDailyLimit:
                                            LogCaller(new LoggerEventArgs(String.Format("Failed to search {0}. Response: {1}", fort, fortResponse.Result), LoggerTypes.Warning));
                                            return new MethodResult();
                                        case FortSearchResponse.Types.Result.InventoryFull:
                                            LogCaller(new LoggerEventArgs(String.Format("Failed to search {0}. Response: {1}", fort, fortResponse.Result), LoggerTypes.Warning));
                                            //Recycle if inventory full
                                            await RecycleFilteredItems();
                                            return new MethodResult();
                                        case FortSearchResponse.Types.Result.Success:
                                            string _message = String.Format("Searched {0}. Exp: {1}. Items: {2}.", // Badge: {3}. BonusLoot: {4}. Gems: {5}. Loot: {6}, Eggs: {7:0.0}. RaidTickets: {8}. TeamBonusLoot: {9}",
                                                            fort,
                                                            fortResponse.ExperienceAwarded,
                                                            StringUtil.GetSummedFriendlyNameOfItemAwardList(fortResponse.ItemsAwarded.ToList())
                                                            /*,
                                                            fortResponse.AwardedGymBadge.ToString(),
                                                            fortResponse.BonusLoot.LootItem.ToString(),
                                                            fortResponse.GemsAwarded.ToString(),
                                                            fortResponse.Loot.LootItem.ToString(),
                                                            fortResponse.PokemonDataEgg.EggKmWalkedStart,
                                                            fortResponse.RaidTickets.ToString(),
                                                            fortResponse.TeamBonusLoot.LootItem.ToString()*/);

                                            //Successfully grabbed stop
                                            if (AccountState == AccountState.SoftBan)// || AccountState == Enums.AccountState.HashIssues)
                                            {
                                                AccountState = AccountState.Good;

                                                LogCaller(new LoggerEventArgs("Soft ban was removed", LoggerTypes.Info));
                                            }

                                            ExpIncrease(fortResponse.ExperienceAwarded);
                                            TotalPokeStopExp += fortResponse.ExperienceAwarded;

                                            Tracker.AddValues(0, 1);

                                            if (fortResponse.ExperienceAwarded == 0)
                                            {
                                                //Softban on the fleeing pokemon. Reset.
                                                _fleeingPokemonResponses = 0;
                                                _potentialPokemonBan = false;

                                                ++_totalZeroExpStops;
                                                _message += String.Format(" No exp gained. Attempt {0} of {1}", i + 1, maxFortAttempts);
                                                LogCaller(new LoggerEventArgs(_message, LoggerTypes.Success));
                                                continue;
                                            }

                                            LogCaller(new LoggerEventArgs(_message, LoggerTypes.Success));

                                            _totalZeroExpStops = 0;
                                            _potentialPokeStopBan = false;

                                            return new MethodResult
                                            {
                                                Success = true,
                                                Message = "Success"
                                            };
                                    }
                                }
                                catch
                                {
                                    break;
                                }
                                finally
                                {
                                    bypass--;
                                    await Task.Delay(CalculateDelay(UserSettings.DelayBetweenPlayerActions, UserSettings.PlayerActionDelayRandom));
                                }
                            }

                            _potentialPokeStopBan = true;
                            _proxyIssue = true;
                            //Display error only on first notice
                            LogCaller(new LoggerEventArgs("Pokestop out of range. Potential temp pokestop ban or IP ban or daily limit reached.", LoggerTypes.Warning));
                        }

                        _failedPokestopResponse++;
                        //Let it continue down
                        continue;
                    case FortSearchResponse.Types.Result.PoiInaccessible:
                        LogCaller(new LoggerEventArgs(String.Format("Failed to search {0}. Response: {1}", fort, fortResponse.Result), LoggerTypes.Warning));
                        break;
                    case FortSearchResponse.Types.Result.Success:
                        string message = String.Format("Searched {0}. Exp: {1}. Items: {2}.", // Badge: {3}. BonusLoot: {4}. Gems: {5}. Loot: {6}, Eggs: {7:0.0}. RaidTickets: {8}. TeamBonusLoot: {9}",
                            fort,
                            fortResponse.ExperienceAwarded,
                            StringUtil.GetSummedFriendlyNameOfItemAwardList(fortResponse.ItemsAwarded.ToList())
                            /*,
                            fortResponse.AwardedGymBadge.ToString(),
                            fortResponse.BonusLoot.LootItem.ToString(),
                            fortResponse.GemsAwarded.ToString(),
                            fortResponse.Loot.LootItem.ToString(),
                            fortResponse.PokemonDataEgg.EggKmWalkedStart,
                            fortResponse.RaidTickets.ToString(),
                            fortResponse.TeamBonusLoot.LootItem.ToString()*/);

                        //Successfully grabbed stop
                        if (AccountState == AccountState.SoftBan)// || AccountState == Enums.AccountState.HashIssues)
                        {
                            AccountState = AccountState.Good;

                            LogCaller(new LoggerEventArgs("Soft ban was removed", LoggerTypes.Info));
                        }

                        ExpIncrease(fortResponse.ExperienceAwarded);
                        TotalPokeStopExp += fortResponse.ExperienceAwarded;

                        Tracker.AddValues(0, 1);

                        if (fortResponse.ExperienceAwarded == 0)
                        {
                            //Softban on the fleeing pokemon. Reset.
                            _fleeingPokemonResponses = 0;
                            _potentialPokemonBan = false;

                            ++_totalZeroExpStops;
                            message += String.Format(" No exp gained. Attempt {0} of {1}", i + 1, maxFortAttempts);
                            LogCaller(new LoggerEventArgs(message, LoggerTypes.Success));
                            continue;
                        }

                        LogCaller(new LoggerEventArgs(message, LoggerTypes.Success));

                        _totalZeroExpStops = 0;
                        _potentialPokeStopBan = false;

                        return new MethodResult
                        {
                            Success = true,
                            Message = "Success"
                        };
                }
            }

            return new MethodResult();
        }

        private async Task<MethodResult<FortDetailsResponse>> FortDetails(FortData pokestop)
        {
            if (!_client.LoggedIn)
            {
                MethodResult result = await AcLogin();

                if (!result.Success)
                {
                    return new MethodResult<FortDetailsResponse>();
                }
            }

            if (Tracker.PokestopsFarmed >= UserSettings.SpinPokestopsDayLimit)
            {
                LogCaller(new LoggerEventArgs("Pokestops limit actived", LoggerTypes.Info));
                return new MethodResult<FortDetailsResponse>
                {
                    Message = "Limit actived"
                };
            }

            var response = await _client.ClientSession.RpcClient.SendRemoteProcedureCallAsync(new Request
            {
                RequestType = RequestType.FortDetails,
                RequestMessage = new FortDetailsMessage
                {
                    FortId = pokestop.Id,
                    Latitude = pokestop.Latitude,
                    Longitude = pokestop.Longitude,
                }.ToByteString()
            });

            if (response == null)
                return new MethodResult<FortDetailsResponse>();

            var fortDetailsResponse = FortDetailsResponse.Parser.ParseFrom(response);

            if (fortDetailsResponse != null)
            {
                LogCaller(new LoggerEventArgs("Fort details success.", LoggerTypes.Success));
                return new MethodResult<FortDetailsResponse>
                {
                    Data = fortDetailsResponse,
                    Success = true
                };
            }
            else
                return new MethodResult<FortDetailsResponse>();
        }

        private async Task<MethodResult<AddFortModifierResponse>> AddFortModifier(string fortId, ItemId modifierType = ItemId.ItemTroyDisk)
        {
            if (!_client.LoggedIn)
            {
                MethodResult result = await AcLogin();

                if (!result.Success)
                {
                    return new MethodResult<AddFortModifierResponse>();
                }
            }

            var response = await _client.ClientSession.RpcClient.SendRemoteProcedureCallAsync(new Request
            {
                RequestType = RequestType.AddFortModifier,
                RequestMessage = new AddFortModifierMessage
                {
                    FortId = fortId,
                    ModifierType = modifierType,
                    PlayerLatitude = _client.ClientSession.Player.Latitude,
                    PlayerLongitude = _client.ClientSession.Player.Longitude
                }.ToByteString()
            });

            if (response == null)
                return new MethodResult<AddFortModifierResponse>();

            var addFortModifierResponse = AddFortModifierResponse.Parser.ParseFrom(response);

            switch (addFortModifierResponse.Result)
            {
                case AddFortModifierResponse.Types.Result.Success:
                    LogCaller(new LoggerEventArgs(String.Format("Add fort modifier {0} success.", modifierType.ToString().Replace("Item","")), LoggerTypes.Success));
                    return new MethodResult<AddFortModifierResponse>
                    {
                        Success = true,
                        Message = "Success",
                        Data = addFortModifierResponse
                    };
                case AddFortModifierResponse.Types.Result.FortAlreadyHasModifier:
                    LogCaller(new LoggerEventArgs(String.Format("Failed to search Fort. Response: {0}", addFortModifierResponse.Result), LoggerTypes.Warning));
                    break;
                case AddFortModifierResponse.Types.Result.NoItemInInventory:
                    LogCaller(new LoggerEventArgs(String.Format("Failed to search Fort. Response: {0}", addFortModifierResponse.Result), LoggerTypes.Warning));
                    break;
                case AddFortModifierResponse.Types.Result.PoiInaccessible:
                    LogCaller(new LoggerEventArgs(String.Format("Failed to search Fort. Response: {0}", addFortModifierResponse.Result), LoggerTypes.Warning));
                    break;
                case AddFortModifierResponse.Types.Result.TooFarAway:
                    LogCaller(new LoggerEventArgs(String.Format("Failed to search Fort. Response: {0}", addFortModifierResponse.Result), LoggerTypes.Warning));
                    break;
           }
            return new MethodResult<AddFortModifierResponse>();
        }
    }
}
