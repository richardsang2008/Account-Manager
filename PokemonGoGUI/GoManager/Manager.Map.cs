using POGOLib.Official.Extensions;
using POGOProtos.Inventory.Item;
using POGOProtos.Map.Fort;
using POGOProtos.Map.Pokemon;
using PokemonGoGUI.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PokemonGoGUI.GoManager
{
    public partial class Manager
    {
        private async Task<MethodResult<List<MapPokemon>>> GetCatchablePokemonAsync()
        {
            if (!UserSettings.UsePOGOLibHeartbeat)
                await _client.ClientSession.RpcClient.RefreshMapObjectsAsync();

            if (_client.ClientSession.Map.Cells.Count == 0 || _client.ClientSession.Map == null)
            {
               throw new OperationCanceledException("Not cells.");
            }

            //var cells = _client.ClientSession.Map.Cells;

            //         Where(PokemonWithinCatchSettings) <-- Unneeded, will be filtered after.
            List<MapPokemon> newCatchablePokemons = _client.ClientSession.Map.Cells.SelectMany(x => x.CatchablePokemons).ToList();
            List<MapPokemon> realList = new List<MapPokemon>();

            foreach (var pok in newCatchablePokemons)
            {
                if (IsValidLocation(pok.Latitude, pok.Longitude))
                    realList.Add(pok);
            }

            return new MethodResult<List<MapPokemon>>
            {
                Data = realList,
                Success = true,
                Message = "Success"
            };
        }

        private async Task<MethodResult<List<FortData>>> GetAllFortsAsync()
        {
            if (!UserSettings.UsePOGOLibHeartbeat)
                await _client.ClientSession.RpcClient.RefreshMapObjectsAsync();

            if (_client.ClientSession.Map.Cells.Count == 0 || _client.ClientSession.Map == null)
            {
                throw new OperationCanceledException("Not cells.");
            }

            var forts = _client.ClientSession.Map.Cells.SelectMany(p => p.Forts);//.GetFortsSortedByDistance();

            if (!forts.Any())
            {
                return new MethodResult<List<FortData>>
                {
                    Message = "No pokestop data found. Potential temp IP ban or bad location",
                };
            }

            var fortData = new List<FortData>();

            foreach (FortData fort in forts)
            {
                if (!IsValidLocation(fort.Latitude, fort.Longitude))
                {
                    continue;
                }

                if (fort.CooldownCompleteTimestampMs >= DateTime.UtcNow.ToUnixTime())
                {
                    continue;
                }

                var defaultLocation = new GeoCoordinate(_client.ClientSession.Player.Latitude, _client.ClientSession.Player.Longitude);
                var fortLocation = new GeoCoordinate(fort.Latitude, fort.Longitude);

                double distance = CalculateDistanceInMeters(defaultLocation, fortLocation);

                if (distance > UserSettings.MaxTravelDistance)
                {
                    continue;
                }

                fortData.Add(fort);
            }

            if (fortData.Count == 0)
            {
                return new MethodResult<List<FortData>>
                {
                    Message = "No searchable pokestops found within range",
                };
            }

            if (UserSettings.ShufflePokestops)
            {
                var rnd = new Random();
                fortData = fortData.OrderBy(x => rnd.Next()).ToList();
            }
            else
            {
                fortData = fortData.OrderBy(x => CalculateDistanceInMeters(_client.ClientSession.Player.Latitude, _client.ClientSession.Player.Longitude, x.Latitude, x.Longitude)).ToList();
            }

            return new MethodResult<List<FortData>>
            {
                Message = "Success",
                Success = true,
                Data = fortData
            };
        }

        private async Task<MethodResult<MapPokemon>> GetIncensePokemons()
        {
            if (!_client.ClientSession.IncenseUsed)
            {
                if (UserSettings.UseIncense)
                {
                    var incenses = Items.Where(x => x.ItemId == ItemId.ItemIncenseOrdinary
                    || x.ItemId == ItemId.ItemIncenseCool
                    || x.ItemId == ItemId.ItemIncenseFloral
                    || x.ItemId == ItemId.ItemIncenseSpicy
                    );

                    if (incenses.Count() > 0)
                    {
                        await UseIncense(incenses.FirstOrDefault().ItemId);

                        if (_client.ClientSession.Map.IncensePokemon != null)
                        {
                            return new MethodResult<MapPokemon>
                            {
                                Data = _client.ClientSession.Map.IncensePokemon,
                                Success = true,
                                Message = "Succes"
                            };
                        }
                    }
                }
            }

            if (_client.ClientSession.Map.IncensePokemon != null)
            {
                return new MethodResult<MapPokemon>
                {
                    Data = _client.ClientSession.Map.IncensePokemon,
                    Success = true,
                    Message = "Succes"
                };
            }
            return new MethodResult<MapPokemon>();
        }
    }
}
