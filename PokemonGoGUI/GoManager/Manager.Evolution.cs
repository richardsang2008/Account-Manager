using Google.Protobuf;
using POGOProtos.Data;
using POGOProtos.Enums;
using POGOProtos.Inventory;
using POGOProtos.Inventory.Item;
using POGOProtos.Networking.Requests;
using POGOProtos.Networking.Requests.Messages;
using POGOProtos.Networking.Responses;
using POGOProtos.Settings.Master;
using PokemonGoGUI.Enums;
using PokemonGoGUI.Extensions;
using PokemonGoGUI.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PokemonGoGUI.GoManager
{
    public partial class Manager
    {
        private async Task<MethodResult> EvolveFilteredPokemon()
        {
            MethodResult<List<PokemonData>> response = GetPokemonToEvolve();

            if (response.Data.Count == 0)
            {
                return new MethodResult();
            }

            LogCaller(new LoggerEventArgs(String.Format("{0} pokemon to evolve", response.Data.Count), LoggerTypes.Info));

            if (FilledPokemonInventorySpace() <= UserSettings.ForceEvolveAbovePercent)
            {
                LogCaller(new LoggerEventArgs(String.Format("Not enough pokemon inventory space {0:0.00}% of {1:0.00}% force evolve above percent.", FilledPokemonInventorySpace(), UserSettings.ForceEvolveAbovePercent), LoggerTypes.Info));

                return new MethodResult();
            }

            if (response.Data.Count < UserSettings.MinPokemonBeforeEvolve)
            {
                LogCaller(new LoggerEventArgs(String.Format("Not enough pokemon to evolve. {0} of {1} evolvable pokemon.", response.Data.Count, UserSettings.MinPokemonBeforeEvolve), LoggerTypes.Info));

                return new MethodResult();
            }

            if (UserSettings.UseLuckyEgg && !UserSettings.UseLuckEggConst)
            {
                MethodResult result = await UseLuckyEgg();

                if (!result.Success)
                {
                    LogCaller(new LoggerEventArgs("Failed to use lucky egg. Possibly already active. Continuing evolving", LoggerTypes.Info));
                }
            }

            MethodResult evole = await EvolvePokemon(response.Data);
            if (evole.Success)
            {
                return new MethodResult
                {
                    Success = true,
                    Message = "Success"
                };
            }
            return new MethodResult();
        }

        public async Task<MethodResult> EvolvePokemon(IEnumerable<PokemonData> pokemonToEvolve)
        {
            //Shouldn't happen
            if (pokemonToEvolve.Count() < 1)
            {
                LogCaller(new LoggerEventArgs("Null value sent to evolve pokemon", LoggerTypes.Debug));

                return new MethodResult();
            }

            foreach (PokemonData pokemon in pokemonToEvolve)
            {
                if (pokemon == null)
                {
                    LogCaller(new LoggerEventArgs("Null pokemon data in IEnumerable", LoggerTypes.Debug));

                    continue;
                }

                if (pokemon.IsBad)
                {
                    LogCaller(new LoggerEventArgs(String.Format("Pokemon {0} is slashed.", pokemon.PokemonId), LoggerTypes.Warning));
                    //await TransferPokemon(new List<PokemonData> { pokemon });
                    continue;
                }

                if (!CanEvolvePokemon(pokemon))
                {
                    LogCaller(new LoggerEventArgs(String.Format("Skipped {0}, this pokemon cant not be upgrated maybe is deployed pokemon or you not have needed resources.", pokemon.PokemonId), LoggerTypes.Info));
                    continue;
                }

                PokemonSettings pokemonSettings = GetPokemonSetting((pokemon).PokemonId).Data;
                ItemId itemNeeded = pokemonSettings.EvolutionBranch.Select(x => x.EvolutionItemRequirement).FirstOrDefault();

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
                    RequestType = RequestType.EvolvePokemon,
                    RequestMessage = new EvolvePokemonMessage
                    {
                        PokemonId = pokemon.Id,
                        EvolutionItemRequirement = itemNeeded
                    }.ToByteString()
                });

                if (response == null)
                    return new MethodResult();

                EvolvePokemonResponse evolvePokemonResponse = EvolvePokemonResponse.Parser.ParseFrom(response);
                switch (evolvePokemonResponse.Result)
                {
                    case EvolvePokemonResponse.Types.Result.Success:
                        ExpIncrease(evolvePokemonResponse.ExperienceAwarded);
                        //_expGained += evolvePokemonResponse.ExperienceAwarded;

                        LogCaller(new LoggerEventArgs(
                                String.Format("Successully evolved {0} to {1}. Experience: {2}. Cp: {3} -> {4}. IV: {5:0.00}%",
                                            pokemon.PokemonId,
                                            pokemonSettings.EvolutionBranch.Select(x => x.Evolution).FirstOrDefault(),
                                            evolvePokemonResponse.ExperienceAwarded,
                                            pokemon.Cp,
                                            evolvePokemonResponse.EvolvedPokemonData.Cp,
                                            CalculateIVPerfection(evolvePokemonResponse.EvolvedPokemonData)),
                                            LoggerTypes.Evolve));

                        UpdateInventory(InventoryRefresh.Pokemon);
                        UpdateInventory(InventoryRefresh.PokemonCandy);

                        await Task.Delay(CalculateDelay(UserSettings.DelayBetweenPlayerActions, UserSettings.PlayerActionDelayRandom));

                        continue;
                    case EvolvePokemonResponse.Types.Result.FailedInsufficientResources:
                        LogCaller(new LoggerEventArgs("Evolve request failed: Failed Insufficient Resources", LoggerTypes.Warning));
                        continue;
                    case EvolvePokemonResponse.Types.Result.FailedInvalidItemRequirement:
                        LogCaller(new LoggerEventArgs("Evolve request failed: Failed Invalid Item Requirement", LoggerTypes.Warning));
                        continue;
                    case EvolvePokemonResponse.Types.Result.FailedPokemonCannotEvolve:
                        LogCaller(new LoggerEventArgs("Evolve request failed: Failed Pokemon Cannot Evolve", LoggerTypes.Warning));
                        continue;
                    case EvolvePokemonResponse.Types.Result.FailedPokemonIsDeployed:
                        LogCaller(new LoggerEventArgs("Evolve request failed: Failed Pokemon IsDeployed", LoggerTypes.Warning));
                        continue;
                    case EvolvePokemonResponse.Types.Result.FailedPokemonMissing:
                        LogCaller(new LoggerEventArgs("Evolve request failed: Failed Pokemon Missing", LoggerTypes.Warning));
                        continue;
                    case EvolvePokemonResponse.Types.Result.Unset:
                        LogCaller(new LoggerEventArgs("Evolve request failed", LoggerTypes.Warning));
                        continue;
                }
            }

            return new MethodResult
            {
                Success = true
            };
        }

        private async Task<MethodResult<int>> GetEvolutionCandy(PokemonId pokemonId)
        {
            if (PokeSettings == null)
            {
                MethodResult result = await GetItemTemplates();

                if (!result.Success)
                {
                    return (MethodResult<int>)result;
                }
            }

            MethodResult<PokemonSettings> settingsResult = GetPokemonSetting(pokemonId);

            if (!settingsResult.Success)
            {
                return new MethodResult<int>
                {
                    Message = settingsResult.Message
                };
            }

            return new MethodResult<int>
            {
                Data = settingsResult.Data.EvolutionBranch.Select(x => x.CandyCost).FirstOrDefault(),
                Message = "Success",
                Success = true
            };
        }

        private MethodResult<List<PokemonData>> GetPokemonToEvolve()
        {
            if (!UserSettings.EvolvePokemon)
            {
                LogCaller(new LoggerEventArgs("Evolving disabled", LoggerTypes.Info));

                return new MethodResult<List<PokemonData>>
                {
                    Data = new List<PokemonData>(),
                    Message = "Evolving disabled",
                    Success = true
                };
            }

            var pokemonToEvolve = new List<PokemonData>();

            IEnumerable<IGrouping<PokemonId, PokemonData>> groupedPokemon = Pokemon.OrderByDescending(x => x.PokemonId).GroupBy(x => x.PokemonId);

            foreach (IGrouping<PokemonId, PokemonData> group in groupedPokemon)
            {
                EvolveSetting evolveSetting = UserSettings.EvolveSettings.FirstOrDefault(x => x.Id == group.Key);

                if (evolveSetting == null)
                {
                    LogCaller(new LoggerEventArgs(String.Format("Failed to find evolve settings for pokemon {0}", group.Key), LoggerTypes.Info));

                    continue;
                }

                if (!evolveSetting.Evolve)
                {
                    //Don't evolve
                    continue;
                }
                PokemonSettings setting;
                if (!PokeSettings.TryGetValue(group.Key, out setting))
                {
                    LogCaller(new LoggerEventArgs(String.Format("Failed to find settings for pokemon {0}", group.Key), LoggerTypes.Info));

                    continue;
                }

                Candy pokemonCandy = PokemonCandy.FirstOrDefault(x => x.FamilyId == setting.FamilyId);
                List<PokemonData> pokemonGroupToEvolve = group.Where(x => x.Cp >= evolveSetting.MinCP).OrderByDescending(x => CalculateIVPerfection(x)).ToList();

                if (pokemonCandy == null)
                {
                    LogCaller(new LoggerEventArgs(String.Format("No candy found for pokemon {0}", group.Key), LoggerTypes.Info));

                    continue;
                }

                int candyToEvolve = setting.EvolutionBranch.Select(x => x.CandyCost).FirstOrDefault();

                if (candyToEvolve == 0)
                {
                    LogCaller(new LoggerEventArgs(String.Format("No evolution for pokemon {0}", group.Key), LoggerTypes.Info));

                    continue;
                }

                int totalPokemon = pokemonGroupToEvolve.Count;
                int totalCandy = pokemonCandy.Candy_;

                int maxPokemon = totalCandy / candyToEvolve;

                foreach (PokemonData pData in pokemonGroupToEvolve.Take(maxPokemon))
                {
                    if (!CanTransferOrEvolePokemon(pData, true))
                        LogCaller(new LoggerEventArgs(String.Format("Skipped {0}, this pokemon cant not be transfered maybe is a favorit, is deployed or is a buddy pokemon.", pData.PokemonId), LoggerTypes.Info));
                    else
                        pokemonToEvolve.Add(pData);
                }
            }

            return new MethodResult<List<PokemonData>>
            {
                Data = pokemonToEvolve,
                Message = "Success",
                Success = true
            };
        }

        private async Task<MethodResult> UseLuckyEgg()
        {
            if (_client.ClientSession.LuckyEggsUsed)
            {
                return new MethodResult
                {
                    Message = "Lucky egg already active"
                };
            }

            ItemData data = Items.FirstOrDefault(x => x.ItemId == ItemId.ItemLuckyEgg);

            if (data == null || data.Count == 0)
            {
                LogCaller(new LoggerEventArgs("No lucky eggs left", LoggerTypes.Info));

                return new MethodResult
                {
                    Message = "No lucky eggs"
                };
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
                RequestType = RequestType.UseItemXpBoost,
                RequestMessage = new UseItemXpBoostMessage
                {
                    ItemId = ItemId.ItemLuckyEgg
                }.ToByteString()
            });

            if (response == null)
                return new MethodResult();

            UseItemXpBoostResponse useItemXpBoostResponse = null;

            useItemXpBoostResponse = UseItemXpBoostResponse.Parser.ParseFrom(response);

            LogCaller(new LoggerEventArgs(String.Format("Lucky egg used. Remaining: {0}", data.Count - 1), LoggerTypes.Success));

            return new MethodResult
            {
                Success = true
            };
        }

        public double FilledPokemonInventorySpace()
        {
            if (Pokemon == null || PlayerData == null)
            {
                return 0;
            }

            return (double)(Pokemon.Count + Eggs.Count) / PlayerData.MaxPokemonStorage * 100;
        }

        private bool CanEvolvePokemon(PokemonData pokemon)
        {
            // Can't evolve pokemon in gyms.
            if (!string.IsNullOrEmpty(pokemon.DeployedFortId))
                return false;

            var settings = PokeSettings.SingleOrDefault(x => x.Value.PokemonId == pokemon.PokemonId);

            // Can't evolve pokemon that are not evolvable.
            if (settings.Value.EvolutionIds.Count == 0 && settings.Value.EvolutionBranch.Count == 0)
                return false;

            int familyCandy = PokemonCandy.FirstOrDefault(x => x.FamilyId == settings.Value.FamilyId).Candy_;

            bool canEvolve = false;
            // Check requirements for all branches, if we meet the requirements for any of them then we return true.
            foreach (var branch in settings.Value.EvolutionBranch)
            {
                var itemCount = Items.Count(x => x.ItemId == branch.EvolutionItemRequirement);
                var Candies2Evolve = branch.CandyCost; // GetCandyToEvolve(settings);
                var Evolutions = familyCandy / Candies2Evolve;

                if (branch.EvolutionItemRequirement != ItemId.ItemUnknown)
                {
                    if (itemCount == 0)
                        continue;  // Cannot evolve so check next branch
                }

                if (familyCandy < branch.CandyCost)
                    continue;  // Cannot evolve so check next branch

                // If we got here, then we can evolve so break out of loop.
                canEvolve = true;
            }
            return canEvolve;
        }
    }
}
