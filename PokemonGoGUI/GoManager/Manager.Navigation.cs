using PokemonGoGUI.Enums;
using System;
using System.Threading.Tasks;
using PokemonGoGUI.Extensions;
using POGOLib.Official.Extensions;
using POGOLib.Official.Exceptions;

namespace PokemonGoGUI.GoManager
{
    public partial class Manager
    {
        public async Task<MethodResult> GoToLocation(GeoCoordinate location)
        {
            if (!UserSettings.MimicWalking)
            {
                MethodResult result = await UpdateLocation(location);

                await Task.Delay(CalculateDelay(UserSettings.DelayBetweenLocationUpdates, UserSettings.LocationupdateDelayRandom));

                return result;
            }

            const int maxTries = 3;
            int currentTries = 0;

            while (currentTries < maxTries)
            {
                try
                {
                    Func<Task<MethodResult>> walkingFunction = null;
                    Func<Task<MethodResult>> walkingIncenceFunction = null; 

                    if (UserSettings.EncounterWhileWalking && UserSettings.CatchPokemon)
                    {
                        walkingFunction = CatchNeabyPokemon;
                        walkingIncenceFunction = CatchInsencePokemon;
                    }                    

                    MethodResult walkResponse = await WalkToLocation(location, walkingFunction, walkingIncenceFunction);

                    if (walkResponse.Success)
                    {
                        return new MethodResult
                        {
                            Success = true,
                            Message = "Successfully walked to location"
                        };
                    }

                    LogCaller(new LoggerEventArgs(String.Format("Failed to walk to location. Retry #{0}", currentTries + 1), LoggerTypes.Warning));

                    await Task.Delay(CalculateDelay(UserSettings.DelayBetweenLocationUpdates, UserSettings.LocationupdateDelayRandom));
                }
                catch (SessionStateException ex)
                {
                    throw ex;
                }
                catch (PokeHashException ex)
                {
                    throw ex;
                }
                catch (SessionInvalidatedException ex)
                {
                    throw ex;
                }
                catch (InvalidPlatformException ex)
                {
                    throw ex;
                }
                catch (TaskCanceledException ex)
                {
                    throw ex;
                }
                catch (OperationCanceledException ex)
                {
                    throw ex;
                }
                catch (APIBadRequestException ex)
                {
                    throw ex;
                }
                catch (Exception ex)
                {
                    throw new SessionStateException($"Failed to walk to location. {ex}");
                }
                finally
                {
                    ++currentTries;
                }
                //return new MethodResult();
            }

            return new MethodResult();
        }

        private double WalkOffset()
        {
            lock (_rand)
            {
                double maxOffset = UserSettings.WalkingSpeedOffset * 2;

                double offset = _rand.NextDouble() * maxOffset - UserSettings.WalkingSpeedOffset;

                return offset;
            }
        }

        private async Task<MethodResult> WalkToLocation(GeoCoordinate location, Func<Task<MethodResult>> functionExecutedWhileWalking, Func<Task<MethodResult>> functionExecutedWhileIncenseWalking)
        {
            double speedInMetersPerSecond = (UserSettings.WalkingSpeed + WalkOffset()) / 3.6;

            if(speedInMetersPerSecond <= 0)
            {
                speedInMetersPerSecond = 0;
            }

            var sourceLocation = new GeoCoordinate(_client.ClientSession.Player.Latitude, _client.ClientSession.Player.Longitude);
            double distanceToTarget = CalculateDistanceInMeters(sourceLocation, location);

            double nextWaypointBearing = DegreeBearing(sourceLocation, location);
            double nextWaypointDistance = speedInMetersPerSecond;

            if(nextWaypointDistance >= distanceToTarget)
            {
                nextWaypointDistance = distanceToTarget;
            }

            GeoCoordinate waypoint = await CreateWaypoint(sourceLocation, nextWaypointDistance, nextWaypointBearing);

            //Initial walking
            DateTime requestSendDateTime = DateTime.Now;
            MethodResult result = await UpdateLocation(waypoint);

            if (!result.Success)
            {
                return new MethodResult();
            }

            sourceLocation = new GeoCoordinate(_client.ClientSession.Player.Latitude, _client.ClientSession.Player.Longitude);

            while (CalculateDistanceInMeters(sourceLocation, location) >= 25)
            {
                await Task.Delay(CalculateDelay(UserSettings.DelayBetweenLocationUpdates, UserSettings.LocationupdateDelayRandom));

                speedInMetersPerSecond = (UserSettings.WalkingSpeed + WalkOffset()) / 3.6;

                if (speedInMetersPerSecond <= 0)
                {
                    speedInMetersPerSecond = 0;
                }

                double millisecondsUntilGetUpdatePlayerLocationResponse = (DateTime.Now - requestSendDateTime).TotalMilliseconds;

                sourceLocation = new GeoCoordinate(_client.ClientSession.Player.Latitude, _client.ClientSession.Player.Longitude);
                var currentDistanceToTarget = CalculateDistanceInMeters(sourceLocation, location);

                nextWaypointDistance = Math.Min(currentDistanceToTarget,
                    millisecondsUntilGetUpdatePlayerLocationResponse / 1000 * speedInMetersPerSecond);
                nextWaypointBearing = DegreeBearing(sourceLocation, location);
                waypoint = await CreateWaypoint(sourceLocation, nextWaypointDistance, nextWaypointBearing);

                requestSendDateTime = DateTime.Now;
                result = await UpdateLocation(waypoint);

                if(!result.Success)
                {
                    return new MethodResult();
                }

                if (functionExecutedWhileWalking != null && functionExecutedWhileIncenseWalking != null)
                {
                    MethodResult walkFunctionResult = await functionExecutedWhileWalking(); // look for pokemon
                    MethodResult walkFunctionIncenseResult = await functionExecutedWhileIncenseWalking(); // look for incence pokemon

                    if (walkFunctionResult.Success && walkFunctionIncenseResult.Success)
                    {
                        return new MethodResult
                        {
                            Success = true,
                            Message = "Success"
                        };
                    }
                }

                return new MethodResult
                {
                    Success = true,
                    Message = "Success"
                };
            }

            return new MethodResult
            {
                Success = true
            };
        }

        private async Task<MethodResult> UpdateLocation(GeoCoordinate location)
        {
            try
            {
                var previousLocation = new GeoCoordinate(_client.ClientSession.Player.Latitude, _client.ClientSession.Player.Longitude);

                double distance = CalculateDistanceInMeters(previousLocation, location);

                //Prevent less than 1 meter hops
                if(distance < 1)
                {
                    return new MethodResult
                    {
                        Success = true
                    };
                }

                var moveTo = new GeoCoordinate(location.Latitude, location.Longitude);

                await Task.Run(() => _client.ClientSession.Player.SetCoordinates(moveTo));
                
                UserSettings.Latitude = _client.ClientSession.Player.Latitude;
                UserSettings.Longitude = _client.ClientSession.Player.Longitude;
                UserSettings.Altitude = _client.ClientSession.Player.Altitude;

                //string message = String.Format("Location updated to {0}, {1}. Distance: {2:0.00}m", location.Latitude, location.Longitude, distance);
                string message = String.Format("Location distance: {0:0.00}m",  distance);

                LogCaller(new LoggerEventArgs(message, LoggerTypes.LocationUpdate));

                return new MethodResult
                {
                    Message = message,
                    Success = true
                };
            }
            catch(Exception ex)
            {
                LogCaller(new LoggerEventArgs("Failed to update location", LoggerTypes.Exception, ex));

                return new MethodResult();
            }
        }

        public bool IsValidLocation(double latitude, double longitude)
        {
            return latitude <= 90 && latitude >= -90 && longitude >= -180 && longitude <= 180;
        }

        public double CalculateDistanceInMeters(double sourceLat, double sourceLng,
                double destLat, double destLng)
        // from http://stackoverflow.com/questions/6366408/calculating-distance-between-two-latitude-and-longitude-geocoordinates
        {
            try
            {
                var sourceLocation = new GeoCoordinate(sourceLat, sourceLng);
                var targetLocation = new GeoCoordinate(destLat, destLng);
                return sourceLocation.GetDistanceTo(targetLocation);
            }
            catch (ArgumentOutOfRangeException)
            {
                return double.MaxValue;
            }
        }

        public double CalculateDistanceInMeters(GeoCoordinate sourceLocation, GeoCoordinate destinationLocation)
        {
            return CalculateDistanceInMeters(sourceLocation.Latitude, sourceLocation.Longitude,
                destinationLocation.Latitude, destinationLocation.Longitude);
        }

        public async Task<GeoCoordinate> CreateWaypoint(GeoCoordinate sourceLocation,
                double distanceInMeters, double bearingDegrees)
        //from http://stackoverflow.com/a/17545955
        {
            var distanceKm = distanceInMeters / 1000.0;
            var distanceRadians = distanceKm / 6371; //6371 = Earth's radius in km

            var bearingRadians = ToRad(bearingDegrees);
            var sourceLatitudeRadians = ToRad(sourceLocation.Latitude);
            var sourceLongitudeRadians = ToRad(sourceLocation.Longitude);

            var targetLatitudeRadians = Math.Asin(Math.Sin(sourceLatitudeRadians) * Math.Cos(distanceRadians)
                                                  +
                                                  Math.Cos(sourceLatitudeRadians) * Math.Sin(distanceRadians) *
                                                  Math.Cos(bearingRadians));

            var targetLongitudeRadians = sourceLongitudeRadians + Math.Atan2(Math.Sin(bearingRadians)
                                                                             * Math.Sin(distanceRadians) *
                                                                             Math.Cos(sourceLatitudeRadians),
                                             Math.Cos(distanceRadians)
                                             - Math.Sin(sourceLatitudeRadians) * Math.Sin(targetLatitudeRadians));

            // adjust toLonRadians to be in the range -180 to +180...
            targetLongitudeRadians = (targetLongitudeRadians + 3 * Math.PI) % (2 * Math.PI) - Math.PI;

            return new GeoCoordinate(
                ToDegrees(targetLatitudeRadians),
                ToDegrees(targetLongitudeRadians)
            );
        }

        public GeoCoordinate CreateWaypoint(GeoCoordinate sourceLocation, double distanceInMeters,
                double bearingDegrees, double altitude)
        //from http://stackoverflow.com/a/17545955
        {
            var distanceKm = distanceInMeters / 1000.0;
            var distanceRadians = distanceKm / 6371; //6371 = Earth's radius in km

            var bearingRadians = ToRad(bearingDegrees);
            var sourceLatitudeRadians = ToRad(sourceLocation.Latitude);
            var sourceLongitudeRadians = ToRad(sourceLocation.Longitude);

            var targetLatitudeRadians = Math.Asin(Math.Sin(sourceLatitudeRadians) * Math.Cos(distanceRadians)
                                                  +
                                                  Math.Cos(sourceLatitudeRadians) * Math.Sin(distanceRadians) *
                                                  Math.Cos(bearingRadians));

            var targetLongitudeRadians = sourceLongitudeRadians + Math.Atan2(Math.Sin(bearingRadians)
                                                                             * Math.Sin(distanceRadians) *
                                                                             Math.Cos(sourceLatitudeRadians),
                                             Math.Cos(distanceRadians)
                                             - Math.Sin(sourceLatitudeRadians) * Math.Sin(targetLatitudeRadians));

            // adjust toLonRadians to be in the range -180 to +180...
            targetLongitudeRadians = (targetLongitudeRadians + 3 * Math.PI) % (2 * Math.PI) - Math.PI;

            return new GeoCoordinate(ToDegrees(targetLatitudeRadians), ToDegrees(targetLongitudeRadians), altitude);
        }

        public double DegreeBearing(GeoCoordinate sourceLocation, GeoCoordinate targetLocation)
        // from http://stackoverflow.com/questions/2042599/direction-between-2-latitude-longitude-points-in-c-sharp
        {
            var dLon = ToRad(targetLocation.Longitude - sourceLocation.Longitude);
            var dPhi = Math.Log(
                Math.Tan(ToRad(targetLocation.Latitude) / 2 + Math.PI / 4) /
                Math.Tan(ToRad(sourceLocation.Latitude) / 2 + Math.PI / 4));
            if (Math.Abs(dLon) > Math.PI)
                dLon = dLon > 0 ? -(2 * Math.PI - dLon) : 2 * Math.PI + dLon;
            return ToBearing(Math.Atan2(dLon, dPhi));
        }

        public double ToBearing(double radians)
        {
            // convert radians to degrees (as bearing: 0...360)
            return (ToDegrees(radians) + 360) % 360;
        }

        public double ToDegrees(double radians)
        {
            return radians * 180 / Math.PI;
        }

        public double ToRad(double degrees)
        {
            return degrees * (Math.PI / 180);
        }
    }
}