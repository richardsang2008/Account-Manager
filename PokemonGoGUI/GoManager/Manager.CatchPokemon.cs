using System.Globalization;
using Google.Protobuf;
using POGOProtos.Data;
using POGOProtos.Enums;
using POGOProtos.Inventory.Item;
using POGOProtos.Map.Fort;
using POGOProtos.Map.Pokemon;
using POGOProtos.Networking.Requests;
using POGOProtos.Networking.Requests.Messages;
using POGOProtos.Networking.Responses;
using PokemonGoGUI.Extensions;
using PokemonGoGUI.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PokemonGoGUI.Enums;

namespace PokemonGoGUI.GoManager
{
    public partial class Manager
    {
        List<ulong> LastedEncountersIds = new List<ulong>();

        private async Task<MethodResult> CatchInsencePokemon()
        {
            if (!UserSettings.CatchPokemon)
            {
                return new MethodResult
                {
                    Message = "Catching pokemon disabled"
                };
            }

            if (Tracker.PokemonCaught >= UserSettings.CatchPokemonDayLimit)
            {
                LogCaller(new LoggerEventArgs("Catch pokemon limit actived", LoggerTypes.Info));
                return new MethodResult
                {
                    Message = "Limit actived"
                };
            }

            if (FilledPokemonStorage() >= 100)
            {
                return new MethodResult
                {
                    Message = "Pokemon Inventory Full."
                };
            }

            if (!CatchDisabled)
            {
                if (RemainingPokeballs() < 1)
                {
                    LogCaller(new LoggerEventArgs("You don't have any pokeball catching (Lure) pokemon will be disabled during " + UserSettings.DisableCatchDelay.ToString(CultureInfo.InvariantCulture) + " minutes.", LoggerTypes.Info));
                    CatchDisabled = true;
                    TimeAutoCatch = DateTime.Now.AddMinutes(UserSettings.DisableCatchDelay);
                    return new MethodResult();
                }
            }
            else
                return new MethodResult();

            MethodResult<MapPokemon> iResponse = await GetIncensePokemons();

            if (!iResponse.Success || iResponse.Data == null || iResponse.Data.PokemonId == PokemonId.Missingno)
            {
                return new MethodResult();
            }

            if (iResponse.Data.PokemonId == PokemonId.Missingno)
                return new MethodResult();

            if (!PokemonWithinCatchSettings(iResponse.Data.PokemonId))
            {
                return new MethodResult();
            }

            MethodResult<IncenseEncounterResponse> result = await EncounterIncensePokemon(iResponse.Data);

            if (!result.Success)
            {
                await Task.Delay(CalculateDelay(UserSettings.DelayBetweenPlayerActions, UserSettings.PlayerActionDelayRandom));

                return new MethodResult();
            }

            MethodResult catchResult = await CatchPokemon(result.Data, iResponse.Data);

            await Task.Delay(CalculateDelay(UserSettings.DelayBetweenPlayerActions, UserSettings.PlayerActionDelayRandom));

            return new MethodResult
            {
                Success = true
            };
        }

        private async Task<MethodResult> CatchNeabyPokemon()
        {
            if (!UserSettings.CatchPokemon)
            {
                return new MethodResult
                {
                    Message = "Catching pokemon disabled"
                };
            }

            if (Tracker.PokemonCaught >= UserSettings.CatchPokemonDayLimit)
            {
                LogCaller(new LoggerEventArgs("Catch pokemon limit actived", LoggerTypes.Info));
                return new MethodResult
                {
                    Message = "Limit actived"
                };
            }

            if (FilledPokemonStorage() >= 100)
            {
                return new MethodResult
                {
                    Message = "Pokemon Inventory Full."
                };
            }

            MethodResult<List<MapPokemon>> catchableResponse = GetCatchablePokemon();

            if (!catchableResponse.Success || catchableResponse.Data == null || catchableResponse.Data.Count == 0)
            {
                return new MethodResult();
            }

            foreach (MapPokemon pokemon in catchableResponse.Data)
            {
                if (pokemon.PokemonId == PokemonId.Missingno)
                    continue;

                if (!PokemonWithinCatchSettings(pokemon))
                {
                    continue;
                }

                MethodResult<EncounterResponse> result = await EncounterPokemon(pokemon);

                if (!result.Success)
                {
                    await Task.Delay(CalculateDelay(UserSettings.DelayBetweenPlayerActions, UserSettings.PlayerActionDelayRandom));

                    continue;
                }

                MethodResult catchResult = await CatchPokemon(result.Data, pokemon);

                await Task.Delay(CalculateDelay(UserSettings.DelayBetweenPlayerActions, UserSettings.PlayerActionDelayRandom));
            }

            return new MethodResult
            {
                Success = true
            };
        }

        private async Task<MethodResult> CatchLuredPokemon(FortData fortData)
        {
            if (fortData.LureInfo == null)
            {
                return new MethodResult
                {
                    Message = "No lure on pokestop",
                };
            }

            if (!UserSettings.CatchPokemon)
            {
                return new MethodResult
                {
                    Message = "Catching pokemon disabled"
                };
            }

            if (Tracker.PokemonCaught >= UserSettings.CatchPokemonDayLimit)
            {
                LogCaller(new LoggerEventArgs("Catch pokemon limit actived", LoggerTypes.Info));
                return new MethodResult
                {
                    Message = "Limit actived"
                };
            }

            if (!CatchDisabled)
            {
                if (RemainingPokeballs() < 1)
                {
                    LogCaller(new LoggerEventArgs("You don't have any pokeball catching (Lure) pokemon will be disabled during " + UserSettings.DisableCatchDelay.ToString(CultureInfo.InvariantCulture) + " minutes.", LoggerTypes.Info));
                    CatchDisabled = true;
                    TimeAutoCatch = DateTime.Now.AddMinutes(UserSettings.DisableCatchDelay);
                    return new MethodResult();
                }
            }
            else
                return new MethodResult();

            if (fortData.LureInfo.ActivePokemonId == PokemonId.Missingno)
            {
                return new MethodResult
                {
                    Message = "No lured pokemon",
                };
            }

            if (!PokemonWithinCatchSettings(fortData.LureInfo.ActivePokemonId))
            {
                return new MethodResult();
            }

            MethodResult catchResult = await CatchPokemon(fortData);

            await Task.Delay(CalculateDelay(UserSettings.DelayBetweenPlayerActions, UserSettings.PlayerActionDelayRandom));

            return new MethodResult
            {
                Success = true
            };
        }

        //Catch lured pokemon
        private async Task<MethodResult> CatchPokemon(FortData fortData)
        {
            if (!_client.LoggedIn)
            {
                MethodResult result = await AcLogin();

                if (!result.Success)
                {
                    return result;
                }
            }

            if (!CatchDisabled)
            {
                if (RemainingPokeballs() < 1)
                {
                    LogCaller(new LoggerEventArgs("You don't have any pokeball catching (Lure) pokemon will be disabled during " + UserSettings.DisableCatchDelay.ToString(CultureInfo.InvariantCulture) + " minutes.", LoggerTypes.Info));
                    CatchDisabled = true;
                    TimeAutoCatch = DateTime.Now.AddMinutes(UserSettings.DisableCatchDelay);
                    return new MethodResult();
                }
            }
            else
                return new MethodResult();

            if (fortData.LureInfo == null || fortData.LureInfo.ActivePokemonId == PokemonId.Missingno)
                return new MethodResult();

            if (LastedEncountersIds.Contains(fortData.LureInfo.EncounterId))
                return new MethodResult();

            var response = await _client.ClientSession.RpcClient.SendRemoteProcedureCallAsync(new Request
            {
                RequestType = RequestType.DiskEncounter,
                RequestMessage = new DiskEncounterMessage
                {
                    EncounterId = fortData.LureInfo.EncounterId,
                    FortId = fortData.Id,
                    GymLatDegrees = fortData.Latitude,
                    GymLngDegrees = fortData.Longitude,
                    PlayerLatitude = _client.ClientSession.Player.Latitude,
                    PlayerLongitude = _client.ClientSession.Player.Longitude
                }.ToByteString()
            });

            if (response == null)
                return new MethodResult();

            DiskEncounterResponse eResponse = DiskEncounterResponse.Parser.ParseFrom(response);

            switch (eResponse.Result)
            {
                case DiskEncounterResponse.Types.Result.Success:
                    if (LastedEncountersIds.Count > 30)
                        LastedEncountersIds.Clear();

                    LastedEncountersIds.Add(eResponse.PokemonData.Id);

                    CatchPokemonResponse catchPokemonResponse = null;
                    int attemptCount = 1;
                    var berryUsed = false;

                    if (eResponse.PokemonData == null || eResponse.PokemonData.PokemonId == PokemonId.Missingno)
                        return new MethodResult();

                    do
                    {
                        if (!CatchDisabled)
                        {
                            if (RemainingPokeballs() < 1)
                            {
                                LogCaller(new LoggerEventArgs("You don't have any pokeball catching (Lure) pokemon will be disabled during " + UserSettings.DisableCatchDelay.ToString(CultureInfo.InvariantCulture) + " minutes.", LoggerTypes.Info));
                                CatchDisabled = true;
                                TimeAutoCatch = DateTime.Now.AddMinutes(UserSettings.DisableCatchDelay);
                                return new MethodResult();
                            }
                        }
                        else
                            return new MethodResult();

                        //Uses lowest capture probability
                        float probability = eResponse.CaptureProbability.CaptureProbability_[0];
                        ItemId pokeBall = GetBestBall(eResponse.PokemonData);

                        if (UserSettings.UseBerries)
                        {
                            bool isLowProbability = probability < 0.35;
                            bool isHighCp = eResponse.PokemonData.Cp > 700;
                            bool isHighPerfection = CalculateIVPerfection(eResponse.PokemonData) > 90;

                            if (!berryUsed)
                            {
                                if ((isLowProbability && isHighCp) || isHighPerfection)
                                {
                                    await UseBerry(fortData.LureInfo.EncounterId, fortData.Id, ItemId.ItemRazzBerry);
                                    berryUsed = true;
                                }
                                else
                                {
                                    bool isHighProbability = probability > 0.65;
                                    var catchSettings = UserSettings.CatchSettings.FirstOrDefault(x => x.Id == eResponse.PokemonData.PokemonId);
                                    if (isHighProbability && catchSettings.UsePinap)
                                    {
                                        await UseBerry(fortData.LureInfo.EncounterId, fortData.Id, ItemId.ItemPinapBerry);
                                        berryUsed = true;
                                    }
                                    else if (new Random().Next(0, 100) < 50)
                                    {
                                        // IF we dont use razz neither use pinap, then we will use nanab randomly the 50% of times.
                                        await UseBerry(fortData.LureInfo.EncounterId, fortData.Id, ItemId.ItemNanabBerry);
                                        berryUsed = true;
                                    }
                                }
                            }
                        }

                        double reticuleSize = 1.95;
                        bool hitInsideReticule = true;

                        //Humanization
                        if (UserSettings.EnableHumanization)
                        {
                            reticuleSize = (double)_rand.Next(10, 195) / 100;
                            hitInsideReticule = HitInsideReticle();
                        }

                        //End humanization
                        var arPlusValues = new ARPlusEncounterValues();
                        if (UserSettings.GetArBonus)
                        {
                            LogCaller(new LoggerEventArgs("Using AR Bonus Values", LoggerTypes.Info));
                            arPlusValues.Awareness = (float)UserSettings.ARBonusAwareness;
                            arPlusValues.Proximity = (float)UserSettings.ARBonusProximity;
                            arPlusValues.PokemonFrightened = false;
                        }

                        if (!_client.LoggedIn)
                        {
                            MethodResult result = await AcLogin();

                            if (!result.Success)
                            {
                                return result;
                            }
                        }

                        var catchresponse = await _client.ClientSession.RpcClient.SendRemoteProcedureCallAsync(new Request
                        {
                            RequestType = RequestType.CatchPokemon,
                            RequestMessage = new CatchPokemonMessage
                            {
                                ArPlusValues = arPlusValues,
                                EncounterId = fortData.LureInfo.EncounterId,
                                HitPokemon = hitInsideReticule,
                                NormalizedHitPosition = 1,
                                NormalizedReticleSize = reticuleSize,
                                Pokeball = pokeBall,
                                SpawnPointId = fortData.Id,
                                SpinModifier = 1
                            }.ToByteString()
                        });

                        if (catchresponse == null)
                            return new MethodResult();

                        catchPokemonResponse = CatchPokemonResponse.Parser.ParseFrom(catchresponse);
                        string pokemon = String.Format("Name: {0}, CP: {1}, IV: {2:0.00}%", fortData.LureInfo.ActivePokemonId, eResponse.PokemonData.Cp, CalculateIVPerfection(eResponse.PokemonData));
                        string pokeBallName = pokeBall.ToString().Replace("Item", "");

                        switch (catchPokemonResponse.Status)
                        {
                            case CatchPokemonResponse.Types.CatchStatus.CatchError:
                                LogCaller(new LoggerEventArgs(String.Format("Unknown Error. {0}. Attempt #{1}. Status: {2}", pokemon, attemptCount, catchPokemonResponse.Status), LoggerTypes.Warning));
                                continue;
                            case CatchPokemonResponse.Types.CatchStatus.CatchEscape:
                                //If we get this response, means we're good
                                _fleeingPokemonResponses = 0;
                                _potentialPokemonBan = false;

                                if (AccountState == AccountState.SoftBan || AccountState == AccountState.HashIssues)
                                {
                                    AccountState = AccountState.Good;

                                    LogCaller(new LoggerEventArgs("Pokemon ban was lifted", LoggerTypes.Info));
                                }

                                LogCaller(new LoggerEventArgs(String.Format("Escaped ball. {0}. Attempt #{1}. Ball: {2}", pokemon, attemptCount, pokeBallName), LoggerTypes.PokemonEscape));
                                continue;
                            case CatchPokemonResponse.Types.CatchStatus.CatchFlee:
                                ++_fleeingPokemonResponses;
                                LogCaller(new LoggerEventArgs(String.Format("Pokemon fled. {0}. Attempt #{1}. Ball: {2}", pokemon, attemptCount, pokeBallName), LoggerTypes.PokemonFlee));
                                continue;
                            case CatchPokemonResponse.Types.CatchStatus.CatchMissed:
                                LogCaller(new LoggerEventArgs(String.Format("Missed. {0}. Attempt #{1}. Status: {2}", pokemon, attemptCount, catchPokemonResponse.Status), LoggerTypes.Warning));
                                continue;
                            case CatchPokemonResponse.Types.CatchStatus.CatchSuccess:
                                int expGained = catchPokemonResponse.CaptureAward.Xp.Sum();
                                int candyGained = catchPokemonResponse.CaptureAward.Candy.Sum();

                                Tracker.AddValues(1, 0);

                                ExpIncrease(expGained);

                                //_expGained += expGained;

                                fortData.LureInfo = null;

                                LogCaller(new LoggerEventArgs(String.Format("[Lured] Pokemon Caught. {0}. Exp {1}. Candy {2}. Attempt #{3}. Ball: {4}", pokemon, expGained, candyGained, attemptCount, pokeBallName), LoggerTypes.Success));

                                //Auto favorit shiny
                                if (UserSettings.AutoFavoritShiny && eResponse.PokemonData.PokemonDisplay.Shiny)
                                {
                                    LogCaller(new LoggerEventArgs(String.Format("[{0}] Pokemon shiny. Auto favorit this pokemon.", eResponse.PokemonData.PokemonId.ToString()), LoggerTypes.Info));
                                    await FavoritePokemon(new List<PokemonData> { eResponse.PokemonData }, true);
                                    await Task.Delay(CalculateDelay(UserSettings.DelayBetweenPlayerActions, UserSettings.PlayerActionDelayRandom));
                                }

                                //Pokemon.Add(eResponse.PokemonData);
                                UpdateInventory(InventoryRefresh.Pokemon);

                                return new MethodResult
                                {
                                    Message = "Pokemon caught",
                                    Success = true
                                };
                        }
                        ++attemptCount;

                        await Task.Delay(CalculateDelay(UserSettings.DelayBetweenPlayerActions, UserSettings.PlayerActionDelayRandom));
                    } while (catchPokemonResponse.Status == CatchPokemonResponse.Types.CatchStatus.CatchMissed || catchPokemonResponse.Status == CatchPokemonResponse.Types.CatchStatus.CatchEscape);
                    return new MethodResult();
                case DiskEncounterResponse.Types.Result.EncounterAlreadyFinished:
                    break;
                case DiskEncounterResponse.Types.Result.NotAvailable:
                    break;
                case DiskEncounterResponse.Types.Result.NotInRange:
                    break;
                case DiskEncounterResponse.Types.Result.PokemonInventoryFull:
                    break;
                case DiskEncounterResponse.Types.Result.Unknown:
                    break;
            }

            if (LastedEncountersIds.Count > 30)
                LastedEncountersIds.Clear();

            LastedEncountersIds.Add(fortData.LureInfo.EncounterId);

            LogCaller(new LoggerEventArgs(String.Format("Faill cath lure on pokestop {0}. {1}.",fortData.Id, eResponse.Result), LoggerTypes.Warning));
            return new MethodResult();
        }

        private async Task<MethodResult<EncounterResponse>> EncounterPokemon(MapPokemon mapPokemon)
        {
            if (!_client.LoggedIn)
            {
                MethodResult result = await AcLogin();

                if (!result.Success)
                {
                    return new MethodResult<EncounterResponse>();
                }
            }

            if (mapPokemon == null || mapPokemon.PokemonId == PokemonId.Missingno)
                return new MethodResult<EncounterResponse>();

            if (AlreadySnipped || mapPokemon.EncounterId == _lastPokeSniperId)
                return new MethodResult<EncounterResponse>();

            if (LastedEncountersIds.Contains(mapPokemon.EncounterId))
                return new MethodResult<EncounterResponse>();

            if (!CatchDisabled)
            {
                if (RemainingPokeballs() < 1)
                {
                    LogCaller(new LoggerEventArgs("You don't have any pokeball catching pokemon will be disabled during " + UserSettings.DisableCatchDelay.ToString(CultureInfo.InvariantCulture) + " minutes.", LoggerTypes.Info));
                    CatchDisabled = true;
                    TimeAutoCatch = DateTime.Now.AddMinutes(UserSettings.DisableCatchDelay);
                    return new MethodResult<EncounterResponse>();
                }
            }
            else
                return new MethodResult<EncounterResponse>();

            var response = await _client.ClientSession.RpcClient.SendRemoteProcedureCallAsync(new Request
            {
                RequestType = RequestType.Encounter,
                RequestMessage = new EncounterMessage
                {
                    EncounterId = mapPokemon.EncounterId,
                    PlayerLatitude = _client.ClientSession.Player.Latitude,
                    PlayerLongitude = _client.ClientSession.Player.Longitude,
                    SpawnPointId = mapPokemon.SpawnPointId
                }.ToByteString()
            });

            if (response == null)
                return new MethodResult<EncounterResponse>();

            EncounterResponse eResponse = EncounterResponse.Parser.ParseFrom(response);

            switch (eResponse.Status)
            {
                case EncounterResponse.Types.Status.EncounterAlreadyHappened:
                    break;
                case EncounterResponse.Types.Status.EncounterClosed:
                    break;
                case EncounterResponse.Types.Status.EncounterError:
                    break;
                case EncounterResponse.Types.Status.EncounterNotFound:
                    break;
                case EncounterResponse.Types.Status.EncounterNotInRange:
                    break;
                case EncounterResponse.Types.Status.EncounterPokemonFled:
                    break;
                case EncounterResponse.Types.Status.EncounterSuccess:
                    if (LastedEncountersIds.Count > 30)
                        LastedEncountersIds.Clear();

                    LastedEncountersIds.Add(eResponse.WildPokemon.EncounterId);

                    return new MethodResult<EncounterResponse>
                    {
                        Data = eResponse,
                        Success = true,
                        Message = "Success"
                    };
                case EncounterResponse.Types.Status.PokemonInventoryFull:
                    break;
            }

            if (LastedEncountersIds.Count > 30)
                LastedEncountersIds.Clear();

            LastedEncountersIds.Add(mapPokemon.EncounterId);

            LogCaller(new LoggerEventArgs(String.Format("Faill encounter pokemon. {0}.", eResponse.Status), LoggerTypes.Warning));
            return new MethodResult<EncounterResponse> { Message = eResponse.Status.ToString() };
        }

        //Catch encountered pokemon
        private async Task<MethodResult> CatchPokemon(dynamic eResponse, MapPokemon mapPokemon, bool snipped = false)
        {
            PokemonData _encounteredPokemon = null;
            long _unixTimeStamp = 0;
            ulong _encounterId = 0;
            string _spawnPointId = null;
            string _pokemonType = null;
            //Default catch success
            LoggerTypes _loggerType = LoggerTypes.Success;

            // Calling from CatchNormalPokemon
            if (eResponse is EncounterResponse &&
                    (eResponse?.Status == EncounterResponse.Types.Status.EncounterSuccess))
            {
                _encounteredPokemon = eResponse.WildPokemon?.PokemonData;
                _unixTimeStamp = eResponse.WildPokemon?.LastModifiedTimestampMs
                                + eResponse.WildPokemon?.TimeTillHiddenMs;
                _spawnPointId = eResponse.WildPokemon?.SpawnPointId;
                _encounterId = eResponse.WildPokemon?.EncounterId;
                _pokemonType = "Normal";
            }
            // Calling from CatchIncensePokemon
            else if (eResponse is IncenseEncounterResponse &&
                         (eResponse?.Result == IncenseEncounterResponse.Types.Result.IncenseEncounterSuccess))
            {
                _encounteredPokemon = eResponse?.PokemonData;
                _unixTimeStamp = mapPokemon.ExpirationTimestampMs;
                _spawnPointId = mapPokemon.SpawnPointId;
                _encounterId = mapPokemon.EncounterId;
                _pokemonType = "Incense";
            }

            if (_encounterId == _lastPokeSniperId || snipped)
            {
                _pokemonType = "Local Snipe: " + _pokemonType;
                _loggerType = LoggerTypes.Snipe;
                AlreadySnipped = true;
            }

            CatchPokemonResponse catchPokemonResponse = null;
            int attemptCount = 1;
            bool berryUsed = false;

            if (_encounteredPokemon == null || _encounteredPokemon.PokemonId == PokemonId.Missingno)
                return new MethodResult();

            do
            {
                if (!CatchDisabled)
                {
                    if (RemainingPokeballs() < 1)
                    {
                        LogCaller(new LoggerEventArgs("You don't have any pokeball catching pokemon will be disabled during " + UserSettings.DisableCatchDelay.ToString(CultureInfo.InvariantCulture) + " minutes.", LoggerTypes.Info));
                        CatchDisabled = true;
                        TimeAutoCatch = DateTime.Now.AddMinutes(UserSettings.DisableCatchDelay);
                        return new MethodResult();
                    }
                }
                else
                    return new MethodResult();

                //Uses lowest capture probability
                float probability = eResponse.CaptureProbability.CaptureProbability_[0];
                ItemId pokeBall = GetBestBall(_encounteredPokemon);

                if (UserSettings.UseBerries)
                {
                    bool isLowProbability = probability < 0.40;
                    bool isHighCp = _encounteredPokemon.Cp > 800;
                    bool isHighPerfection = CalculateIVPerfection(_encounteredPokemon) > 95;

                    if (!berryUsed)
                    {
                        if ((isLowProbability && isHighCp) || isHighPerfection)
                        {
                            await UseBerry(mapPokemon, ItemId.ItemRazzBerry);
                            berryUsed = true;
                        }
                        else
                        {
                            bool isHighProbability = probability > 0.65;
                            var catchSettings = UserSettings.CatchSettings.FirstOrDefault(x => x.Id == _encounteredPokemon.PokemonId);
                            if (isHighProbability && catchSettings.UsePinap)
                            {
                                await UseBerry(mapPokemon, ItemId.ItemPinapBerry);
                                berryUsed = true;
                            }
                            else if (new Random().Next(0, 100) < 50)
                            {
                                // IF we dont use razz neither use pinap, then we will use nanab randomly the 50% of times.
                                await UseBerry(mapPokemon, ItemId.ItemNanabBerry);
                                berryUsed = true;
                            }
                        }
                        await Task.Delay(CalculateDelay(UserSettings.DelayBetweenPlayerActions, UserSettings.PlayerActionDelayRandom));
                    }
                }

                await Task.Delay(CalculateDelay(UserSettings.DelayBetweenPlayerActions, UserSettings.PlayerActionDelayRandom));

                double reticuleSize = 1.95;
                bool hitInsideReticule = true;

                //Humanization
                if (UserSettings.EnableHumanization)
                {
                    reticuleSize = (double)_rand.Next(10, 195) / 100;
                    hitInsideReticule = HitInsideReticle();
                }
                var arPlusValues = new ARPlusEncounterValues();
                if (UserSettings.GetArBonus)
                {
                    LogCaller(new LoggerEventArgs("Using AR Bonus Values", LoggerTypes.Info));
                    arPlusValues.Awareness = (float)UserSettings.ARBonusAwareness;
                    arPlusValues.Proximity = (float)UserSettings.ARBonusProximity;
                    arPlusValues.PokemonFrightened = false;
                }

                if (!_client.LoggedIn)
                {
                    MethodResult result = await AcLogin();

                    if (!result.Success)
                    {
                        return result;
                    }
                }

                var catchresponse = await _client.ClientSession.RpcClient.SendRemoteProcedureCallAsync(new Request
                {
                    RequestType = RequestType.CatchPokemon,
                    RequestMessage = new CatchPokemonMessage
                    {
                        ArPlusValues = arPlusValues,
                        EncounterId = _encounterId,
                        HitPokemon = hitInsideReticule,
                        NormalizedHitPosition = 1,
                        NormalizedReticleSize = reticuleSize,
                        Pokeball = pokeBall,
                        SpawnPointId = _spawnPointId,
                        SpinModifier = 1
                    }.ToByteString()
                });

                if (catchresponse == null)
                    return new MethodResult();

                catchPokemonResponse = CatchPokemonResponse.Parser.ParseFrom(catchresponse);

                string pokemon = String.Format("Name: {0}, CP: {1}, IV: {2:0.00}%", _encounteredPokemon.PokemonId.ToString(), _encounteredPokemon.Cp, CalculateIVPerfection(_encounteredPokemon));
                string pokeBallName = pokeBall.ToString().Replace("Item", "");

                switch (catchPokemonResponse.Status)
                {
                    case CatchPokemonResponse.Types.CatchStatus.CatchError:
                        LogCaller(new LoggerEventArgs(String.Format("Unknown Error. {0}. Attempt #{1}. Status: {2}", pokemon, attemptCount, catchPokemonResponse.Status), LoggerTypes.Warning));
                        ++attemptCount;
                        continue;
                    case CatchPokemonResponse.Types.CatchStatus.CatchEscape:
                        //If we get this response, means we're good
                        _fleeingPokemonResponses = 0;
                        _potentialPokemonBan = false;

                        if (AccountState == Enums.AccountState.SoftBan)
                        {
                            AccountState = Enums.AccountState.Good;

                            LogCaller(new LoggerEventArgs("Pokemon ban was lifted", LoggerTypes.Info));
                        }

                        LogCaller(new LoggerEventArgs(String.Format("Escaped ball. {0}. Attempt #{1}. Ball: {2}", pokemon, attemptCount, pokeBallName), LoggerTypes.PokemonEscape));
                        ++attemptCount;
                        continue;
                    case CatchPokemonResponse.Types.CatchStatus.CatchFlee:
                        ++_fleeingPokemonResponses;
                        LogCaller(new LoggerEventArgs(String.Format("Pokemon fled. {0}. Attempt #{1}. Ball: {2}", pokemon, attemptCount, pokeBallName), LoggerTypes.PokemonFlee));
                        ++attemptCount;
                        continue;
                    case CatchPokemonResponse.Types.CatchStatus.CatchMissed:
                        LogCaller(new LoggerEventArgs(String.Format("Missed. {0}. Attempt #{1}. Status: {2}", pokemon, attemptCount, catchPokemonResponse.Status), LoggerTypes.Warning));
                        ++attemptCount;
                        continue;
                    case CatchPokemonResponse.Types.CatchStatus.CatchSuccess:
                        //Reset data
                        _fleeingPokemonResponses = 0;
                        Tracker.AddValues(1, 0);
                        _potentialPokemonBan = false;

                        int expGained = catchPokemonResponse.CaptureAward.Xp.Sum();
                        int candyGained = catchPokemonResponse.CaptureAward.Candy.Sum();

                        ExpIncrease(expGained);

                        //_expGained += expGained;

                        LogCaller(new LoggerEventArgs(String.Format("[{0}] Pokemon Caught. {1}. Exp {2}. Candy: {3}. Attempt #{4}. Ball: {5}", _pokemonType, pokemon, expGained, candyGained, attemptCount, pokeBallName), _loggerType));

                        //Auto favorit shiny
                        if (UserSettings.AutoFavoritShiny && _encounteredPokemon.PokemonDisplay.Shiny)
                        {
                            LogCaller(new LoggerEventArgs(String.Format("[{0}] Pokemon shiny. Auto favorit this pokemon.", _encounteredPokemon.PokemonId.ToString()), LoggerTypes.Info));
                            await FavoritePokemon(new List<PokemonData> { _encounteredPokemon }, true);
                            await Task.Delay(CalculateDelay(UserSettings.DelayBetweenPlayerActions, UserSettings.PlayerActionDelayRandom));
                        }

                        //Pokemon.Add(_encounteredPokemon);
                        UpdateInventory(InventoryRefresh.Pokemon);
                        UpdateInventory(InventoryRefresh.PokemonCandy);

                        return new MethodResult
                        {
                            Message = "Pokemon caught",
                            Success = true
                        };
                }
                
                await Task.Delay(CalculateDelay(UserSettings.DelayBetweenPlayerActions, UserSettings.PlayerActionDelayRandom));
            } while (catchPokemonResponse.Status == CatchPokemonResponse.Types.CatchStatus.CatchMissed || catchPokemonResponse.Status == CatchPokemonResponse.Types.CatchStatus.CatchEscape);
            return new MethodResult();
        }

        private bool PokemonWithinCatchSettings(PokemonId pokemonId)
        {
            if (pokemonId == PokemonId.Missingno)
                return false;

            CatchSetting catchSettings = UserSettings.CatchSettings.FirstOrDefault(x => x.Id == pokemonId);

            if (catchSettings == null)
            {
                LogCaller(new LoggerEventArgs(String.Format("Failed to find catch setting for {0}. Attempting to catch", pokemonId), LoggerTypes.Warning));

                return false;
            }

            if (catchSettings.Catch)
            {
                return true;
            }

            LogCaller(new LoggerEventArgs(String.Format("Skipping catching {0}", pokemonId), LoggerTypes.Info));
            return false;
        }

        private bool PokemonWithinCatchSettings(MapPokemon pokemon)
        {
            if (pokemon == null || pokemon.PokemonId == PokemonId.Missingno)
                return false;

            CatchSetting catchSettings = UserSettings?.CatchSettings.FirstOrDefault(x => x.Id == pokemon.PokemonId);

            if (catchSettings == null)
            {
                LogCaller(new LoggerEventArgs(String.Format("Failed to find catch setting for {0}. Attempting to catch", pokemon.PokemonId), LoggerTypes.Warning));
                return false;
            }

            if (catchSettings.Catch)
            {
                return true;
            }

            LogCaller(new LoggerEventArgs(String.Format("Skipping catching {0}", pokemon.PokemonId.ToString()), LoggerTypes.Info));
            return false;
        }

        private ItemId GetBestBall(PokemonData pokemonData)
        {
            if (Items == null || pokemonData == null || pokemonData.PokemonId == PokemonId.Missingno)
            {
                return ItemId.ItemUnknown;
            }

            int pokemonCp = pokemonData.Cp;
            //double ivPercent = CalculateIVPerfection(encounter.WildPokemon.PokemonData).Data;

            ItemData pokeBalls = Items.FirstOrDefault(x => x.ItemId == ItemId.ItemPokeBall);
            ItemData greatBalls = Items.FirstOrDefault(x => x.ItemId == ItemId.ItemGreatBall);
            ItemData ultraBalls = Items.FirstOrDefault(x => x.ItemId == ItemId.ItemUltraBall);
            ItemData masterBalls = Items.FirstOrDefault(x => x.ItemId == ItemId.ItemMasterBall);
            ItemData premierBalls = Items.FirstOrDefault(x => x.ItemId == ItemId.ItemPremierBall);

            if (masterBalls != null && masterBalls.Count > 0 && pokemonCp >= 1200)
            {
                masterBalls.Count--;

                return ItemId.ItemMasterBall;
            }

            if (ultraBalls != null && ultraBalls.Count > 0 && pokemonCp >= 750)
            {
                ultraBalls.Count--;

                return ItemId.ItemUltraBall;
            }

            if (greatBalls != null && greatBalls.Count > 0 && pokemonCp >= 1000)
            {
                greatBalls.Count--;

                return ItemId.ItemGreatBall;
            }

            if (pokeBalls != null && pokeBalls.Count > 0)
            {
                pokeBalls.Count--;

                return ItemId.ItemPokeBall;
            }

            if (greatBalls != null && greatBalls.Count > 0)
            {
                greatBalls.Count--;

                return ItemId.ItemGreatBall;
            }

            if (ultraBalls != null && ultraBalls.Count > 0)
            {
                ultraBalls.Count--;

                return ItemId.ItemUltraBall;
            }

            if (masterBalls != null && masterBalls.Count > 0)
            {
                masterBalls.Count--;

                return ItemId.ItemMasterBall;
            }

            if (premierBalls != null && premierBalls.Count > 0)
            {
                premierBalls.Count--;

                return ItemId.ItemPremierBall;
            }

            return ItemId.ItemUnknown;
        }

        private async Task UseBerry(ulong encounterId, string spawnId, ItemId berry)
        {
            ItemData berryData = Items.FirstOrDefault(x => x.ItemId == berry);

            if (berryData == null || berryData.Count <= 0)
            {
                return;
            }

            if (!_client.LoggedIn)
            {
                MethodResult result = await AcLogin();

                if (!result.Success)
                {
                    return;
                }
            }

            var response = await _client.ClientSession.RpcClient.SendRemoteProcedureCallAsync(new Request
            {
                RequestType = RequestType.UseItemEncounter,
                RequestMessage = new UseItemEncounterMessage
                {
                    EncounterId = encounterId,
                    Item = berryData.ItemId,
                    SpawnPointGuid = spawnId
                }.ToByteString()
            });

            if (response == null)
                return;

            UseItemEncounterResponse useItemEncounterResponse = UseItemEncounterResponse.Parser.ParseFrom(response);

            switch (useItemEncounterResponse.Status)
            {
                case UseItemEncounterResponse.Types.Status.ActiveItemExists:
                    LogCaller(new LoggerEventArgs("Faill: " + useItemEncounterResponse.Status, LoggerTypes.Debug));
                    break;
                case UseItemEncounterResponse.Types.Status.AlreadyCompleted:
                    LogCaller(new LoggerEventArgs("Faill: " + useItemEncounterResponse.Status, LoggerTypes.Debug));
                    break;
                case UseItemEncounterResponse.Types.Status.InvalidItemCategory:
                    LogCaller(new LoggerEventArgs("Faill: " + useItemEncounterResponse.Status, LoggerTypes.Debug));
                    break;
                case UseItemEncounterResponse.Types.Status.NoItemInInventory:
                    LogCaller(new LoggerEventArgs("Faill: " + useItemEncounterResponse.Status, LoggerTypes.Debug));
                    break;
                case UseItemEncounterResponse.Types.Status.Success:
                    int remaining = berryData.Count - 1;
                    berryData.Count = remaining;
                    LogCaller(new LoggerEventArgs(String.Format("Successfully used {0}. Remaining: {1}", berryData.ItemId.ToString().Replace("Item", ""), remaining), LoggerTypes.Success));

                    await Task.Delay(CalculateDelay(UserSettings.DelayBetweenPlayerActions, UserSettings.PlayerActionDelayRandom));
                    break;
            }
        }

        private async Task UseBerry(MapPokemon pokemon, ItemId berry)
        {
            await UseBerry(pokemon.EncounterId, pokemon.SpawnPointId, berry);
        }

        private bool HitInsideReticle()
        {
            lock (_rand)
            {
                return _rand.Next(1, 101) <= UserSettings.InsideReticuleChance;

            }
        }

        //Encounter Incense
        private async Task<MethodResult<IncenseEncounterResponse>> EncounterIncensePokemon(MapPokemon mapPokemon)
        {
            if (!_client.LoggedIn)
            {
                MethodResult result = await AcLogin();

                if (!result.Success)
                {
                    return new MethodResult<IncenseEncounterResponse>();
                }
            }

            if (mapPokemon == null || mapPokemon.PokemonId == PokemonId.Missingno)
                return new MethodResult<IncenseEncounterResponse>();

            if (LastedEncountersIds.Contains(mapPokemon.EncounterId))
                return new MethodResult<IncenseEncounterResponse>();

            if (!CatchDisabled)
            {
                if (RemainingPokeballs() < 1)
                {
                    LogCaller(new LoggerEventArgs("You don't have any pokeball catching pokemon will be disabled during " + UserSettings.DisableCatchDelay.ToString(CultureInfo.InvariantCulture) + " minutes.", LoggerTypes.Info));
                    CatchDisabled = true;
                    TimeAutoCatch = DateTime.Now.AddMinutes(UserSettings.DisableCatchDelay);
                    return new MethodResult<IncenseEncounterResponse>();
                }
            }

            var response = await _client.ClientSession.RpcClient.SendRemoteProcedureCallAsync(new Request
            {
                RequestType = RequestType.IncenseEncounter,
                RequestMessage = new IncenseEncounterMessage
                {
                    EncounterId = mapPokemon.EncounterId,
                    EncounterLocation = mapPokemon.SpawnPointId
                }.ToByteString()
            });

            if (response == null)
                return new MethodResult<IncenseEncounterResponse>();

            IncenseEncounterResponse eResponse = IncenseEncounterResponse.Parser.ParseFrom(response);

            switch (eResponse.Result)
            {
                case IncenseEncounterResponse.Types.Result.IncenseEncounterNotAvailable:
                    break;
                case IncenseEncounterResponse.Types.Result.IncenseEncounterUnknown:
                    break;
                case IncenseEncounterResponse.Types.Result.IncenseEncounterSuccess:
                    if (LastedEncountersIds.Count > 30)
                        LastedEncountersIds.Clear();

                    LastedEncountersIds.Add(eResponse.PokemonData.Id);

                    return new MethodResult<IncenseEncounterResponse>
                    {
                        Data = eResponse,
                        Success = true,
                        Message = "Success"
                    };
                case IncenseEncounterResponse.Types.Result.PokemonInventoryFull:
                    break;
            }

            if (LastedEncountersIds.Count > 30)
                LastedEncountersIds.Clear();

            LastedEncountersIds.Add(mapPokemon.EncounterId);

            LogCaller(new LoggerEventArgs(String.Format("Encounter incense failed with response {0}", eResponse.Result), LoggerTypes.Warning));
            return new MethodResult<IncenseEncounterResponse>();
        }
    }
}
