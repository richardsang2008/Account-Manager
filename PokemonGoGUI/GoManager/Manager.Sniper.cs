using POGOLib.Official.Extensions;
using POGOProtos.Enums;
using POGOProtos.Map.Pokemon;
using POGOProtos.Networking.Responses;
using PokemonGoGUI.Enums;
using PokemonGoGUI.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PokemonGoGUI.GoManager
{
    public partial class Manager
    {
        private ulong _lastPokeSniperId  = 0;

        private bool AlreadySnipped { get; set; } = false;

        private MethodResult<List<NearbyPokemon>> RequestPokeSniperRares()
        {
            if (_client.ClientSession.Map.Cells.Count == 0 || _client.ClientSession.Map == null)
            {
                return new MethodResult<List<NearbyPokemon>>();
            }

            var cells = _client.ClientSession.Map.Cells;
            List<NearbyPokemon> newNearbyPokemons = cells.SelectMany(x => x.NearbyPokemons).ToList();

            if (newNearbyPokemons.Count == 0)
            {
                return new MethodResult<List<NearbyPokemon>>
                {
                    Message = "No pokemon found"
                };
            }

            return new MethodResult<List<NearbyPokemon>>
            {
                Data = newNearbyPokemons,
                Success = true
            };
        }

        public async Task<MethodResult> SnipeAllNearyPokemon()
        {
            if (!UserSettings.CatchPokemon)
            {
                return new MethodResult
                {
                    Message = "Catching pokemon disabled"
                };
            }

            MethodResult<List<NearbyPokemon>> pokeSniperResult = RequestPokeSniperRares();

            if (!pokeSniperResult.Success)
            {
                return new MethodResult
                {
                    Message = pokeSniperResult.Message
                };
            }

            //Priorise snipe...
            /*if (Tracker.PokemonCaught >= UserSettings.CatchPokemonDayLimit)
            {
                LogCaller(new LoggerEventArgs("Catch pokemon limit actived", LoggerTypes.Info));
                return new MethodResult
                {
                    Message = "Limit actived"
                };
            }*/

            List<NearbyPokemon> pokemonToSnipe = pokeSniperResult.Data.Where(x => x.EncounterId != _lastPokeSniperId && UserSettings.CatchSettings.FirstOrDefault(p => p.Id == x.PokemonId).Snipe && x.DistanceInMeters < UserSettings.MaxTravelDistance && !LastedEncountersIds.Contains(x.EncounterId)).OrderBy(x => x.DistanceInMeters).ToList();

            if (UserSettings.SnipeAllPokemonsNoInPokedex)
            {
                LogCaller(new LoggerEventArgs("Search pokemons no into pokedex ...", LoggerTypes.Info));
                var ids = Pokedex.Select(x => x.PokemonId);
                pokemonToSnipe = pokeSniperResult.Data.Where(x => x.EncounterId != _lastPokeSniperId && !ids.Contains(x.PokemonId) && x.DistanceInMeters < UserSettings.MaxTravelDistance && !LastedEncountersIds.Contains(x.EncounterId)).OrderBy(x => x.DistanceInMeters).ToList();
            }

            if (pokemonToSnipe.Count == 0)
            {
                return new MethodResult
                {
                    Message = "No catchable pokemon found"
                };
            }

            LogCaller(new LoggerEventArgs(String.Format("Sniping {0} pokemon", pokemonToSnipe.Count), LoggerTypes.Snipe));

            await Task.Delay(CalculateDelay(UserSettings.GeneralDelay, UserSettings.GeneralDelayRandom));

            //Long running, so can't let this continue
            try
            {
                while (pokemonToSnipe.Any() && IsRunning && !AlreadySnipped)
                {
                    AlreadySnipped = false;

                    NearbyPokemon nearbyPokemon = pokemonToSnipe.First();
                    pokemonToSnipe.Remove(nearbyPokemon);

                    var forts = _client.ClientSession.Map.Cells.SelectMany(x => x.Forts);
                    var fortNearby = forts.Where(x => x.Id == nearbyPokemon.FortId).FirstOrDefault();

                    if (fortNearby == null || nearbyPokemon == null || nearbyPokemon.PokemonId == PokemonId.Missingno)
                    {
                        continue;
                    }

                    GeoCoordinate coords = new GeoCoordinate
                    {
                        Latitude = fortNearby.Latitude,
                        Longitude = fortNearby.Longitude
                    };

                    await CaptureSnipePokemon(coords.Latitude, coords.Longitude, nearbyPokemon.PokemonId);

                    await Task.Delay(CalculateDelay(UserSettings.GeneralDelay, UserSettings.GeneralDelayRandom));

                    pokemonToSnipe = pokemonToSnipe.Where(x => UserSettings.CatchSettings.FirstOrDefault(p => p.Id == x.PokemonId).Snipe && fortNearby.CooldownCompleteTimestampMs >= DateTime.Now.AddSeconds(30).ToUnixTime() && !LastedEncountersIds.Contains(x.EncounterId) && IsValidLocation(fortNearby.Latitude, fortNearby.Longitude)).OrderBy(x => x.DistanceInMeters).ToList();

                    if (UserSettings.SnipeAllPokemonsNoInPokedex)
                    {
                        LogCaller(new LoggerEventArgs("Search pokemons no into pokedex ...", LoggerTypes.Debug));

                        var ids = Pokedex.Select(x => x.PokemonId);
                        pokemonToSnipe = pokemonToSnipe.Where(x => x.EncounterId != _lastPokeSniperId && !ids.Contains(x.PokemonId)).OrderBy(x => x.DistanceInMeters).ToList();

                        if (pokemonToSnipe.Count > 0)
                            LogCaller(new LoggerEventArgs("Found pokemons no into pokedex, go to sniping ...", LoggerTypes.Debug));
                    }
                }

                return new MethodResult
                {
                    Success = true
                };
            }
            catch (Exception ex)
            {
                LogCaller(new LoggerEventArgs("Error, sniping ...", LoggerTypes.Warning, ex));
            }
            finally
            {
                AlreadySnipped = false;
            }

            return new MethodResult();
       }

        private async Task<MethodResult> CaptureSnipePokemon(double latitude, double longitude, PokemonId pokemon)
        {
            var currentLocation = new GeoCoordinate(_client.ClientSession.Player.Latitude, _client.ClientSession.Player.Longitude);
            var fortLocation = new GeoCoordinate(latitude, longitude);

            double distance = CalculateDistanceInMeters(currentLocation, fortLocation);
            LogCaller(new LoggerEventArgs(String.Format("Going to sniping {0} at location {1}, {2}. Distance {3:0.00}m", pokemon, latitude, longitude, distance), LoggerTypes.Snipe));

            // Not nedded this runs on local pos.../
            //GeoCoordinate originalLocation = new GeoCoordinate(_client.ClientSession.Player.Latitude, _client.ClientSession.Player.Longitude, _client.ClientSession.Player.Altitude);

            //Update location           
            //MethodResult result = await UpdateLocation(new GeoCoordinate(latitude, longitude));

            //Update location           
            MethodResult result = await GoToLocation(new GeoCoordinate(latitude, longitude));           

            if(!result.Success)
            {
                return result;
            }

            if (UserSettings.UsePOGOLibHeartbeat)
                await Task.Delay(10000); //wait for pogolib refreshmapobjects

            //Get catchable pokemon

            MethodResult<List<MapPokemon>> pokemonResult = await GetCatchablePokemonAsync();

            if(!pokemonResult.Success)
            {
                return new MethodResult
                {
                    Message = pokemonResult.Message
                };
            }

            if (pokemonResult.Data == null || pokemonResult.Data.Count == 0)
                return new MethodResult();

            MapPokemon pokemonToSnipe = pokemonResult.Data.FirstOrDefault(x => x.PokemonId == pokemon);

            //Encounter
            MethodResult<EncounterResponse> eResponseResult = await EncounterPokemon(pokemonToSnipe);

            if (!eResponseResult.Success)
            {
                //LogCaller(new LoggerEventArgs(String.Format("Snipe failed to encounter pokemon {0}. Going back to original location, or already catched", pokemon), LoggerTypes.Info));
                LogCaller(new LoggerEventArgs(String.Format("Snipe failed to encounter pokemon {0}, or already catched", pokemon), LoggerTypes.Info));

                //Failed, update location back
                //await UpdateLocation(originalLocation);

                // Not nedded this runs on local pos.../
                //await GoToLocation(originalLocation);

                return new MethodResult
                {
                    Message = eResponseResult.Message
                };
            }

            if (eResponseResult.Data == null || eResponseResult.Data.WildPokemon.PokemonData.PokemonId == PokemonId.Missingno)
                return new MethodResult();

            //Update location back
            //MethodResult locationResult = await RepeatAction(() => UpdateLocation(originalLocation), 2);

            // Not nedded this runs on local pos.../
            /*MethodResult locationResult = await RepeatAction(() => GoToLocation(originalLocation), 2);

            if (!locationResult.Success)
            {
                return locationResult;
            }
            */

            _lastPokeSniperId = pokemonToSnipe.EncounterId;

            //Catch pokemon
            MethodResult catchResult = await CatchPokemon(eResponseResult.Data, pokemonToSnipe, true); //Handles logging

            if (catchResult.Success)
            {
                AlreadySnipped = true;                
            }

            return catchResult;
        }

        private async Task<MethodResult> RepeatAction(Func<Task<MethodResult>> action, int tries)
        {
            MethodResult result = new MethodResult();

            for (int i = 0; i < tries; i++)
            {
                result = await action();

                if (result.Success)
                {
                    return result;
                }

                await Task.Delay(1000);
            }

            return result;
        }

        private async Task<MethodResult<T>> RepeatAction<T>(Func<Task<MethodResult<T>>> action, int tries)
        {
            MethodResult<T> result = new MethodResult<T>();

            for (int i = 0; i < tries; i++)
            {
                result = await action();

                if (result.Success)
                {
                    return result;
                }

                await Task.Delay(1000);
            }

            return result;
        }
    }
}
