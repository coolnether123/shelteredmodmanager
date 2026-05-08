using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using ModAPI.Core;
using ShelteredAPI.Networking.World;
using UnityEngine;

namespace ShelteredAPI.Networking.Travel
{
    internal static class ShelteredTravelCorrectionReasons
    {
        public const string Reroute = "reroute";
        public const string Slowdown = "slowdown";
        public const string Ambush = "ambush";
        public const string Stopped = "stopped";
        public const string HostCorrection = "host-correction";
        public const string Recall = "recall";
    }

    internal static class ShelteredTravelArrivalKinds
    {
        public const string Arrived = "arrived";
        public const string Interrupted = "interrupted";
        public const string Cancelled = "cancelled";
        public const string ReturnedHome = "returned-home";
    }

    internal sealed class ShelteredExpeditionTravelReadResult
    {
        public ShelteredExpeditionTravelReadResult()
        {
            Error = string.Empty;
        }

        public bool Success { get; set; }
        public string Error { get; set; }
        public ShelteredTravelStartedEvent Started { get; set; }
        public ShelteredTravelCorrectedEvent Corrected { get; set; }
        public ShelteredTravelArrivedEvent Arrived { get; set; }
    }

    internal sealed class ShelteredExpeditionTravelReader
    {
        private const string LogSource = "ShelteredAPI.ExpeditionTravel";
        private const string TravelIdPrefix = "expedition";
        private const string SeedStreamPrefix = "MultiplayerSync.Travel";
        private static readonly FieldInfo TravelSpeedField = typeof(ExplorationParty).GetField("m_travelSpeed", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo SpeedModifierField = typeof(ExplorationParty).GetField("m_speedModifier", BindingFlags.Instance | BindingFlags.NonPublic);

        public ShelteredExpeditionTravelReadResult TryBuildTravelStarted(ExplorationParty party)
        {
            ShelteredExpeditionTravelReadResult result = new ShelteredExpeditionTravelReadResult();
            ShelteredMultiplayerSessionContext context = ShelteredMultiplayerSessionCoordinator.Instance.Context;
            if (context == null || !context.IsMultiplayerActive)
                return Fail(result, "multiplayer-inactive");
            if (party == null)
                return Fail(result, "missing-party");

            IList<Vector2> route = party.GetRoute();
            if (route == null || route.Count < 2)
                return Fail(result, "party-route-missing");

            Vector2 start = ResolveStart(party, route);
            Vector2 destination = ResolvePrimaryDestination(route);
            ExpeditionMap.GridRef startGrid;
            ExpeditionMap.GridRef destinationGrid;
            if (!TryWorldToGrid(start, out startGrid) || !TryWorldToGrid(destination, out destinationGrid))
                return Fail(result, "grid-extraction-failed");

            int partyId = party.id;
            long startTick = context.WorldTick;
            float worldUnitsPerTick = CalculateWorldUnitsPerTick(party, context);
            long expectedArrivalTick = CalculateExpectedArrivalTick(route, party, start, destination, startTick, context);

            ShelteredTravelStartedEvent started = new ShelteredTravelStartedEvent();
            started.TravelId = CreateTravelId(context, partyId, startTick);
            started.OwnerPlayerId = context.LocalPlayerId;
            started.OwnerPeerId = context.NetworkLocalPeerId;
            started.PartyId = partyId;
            started.StartTick = startTick;
            started.StartGridX = startGrid.x;
            started.StartGridY = startGrid.y;
            started.DestinationGridX = destinationGrid.x;
            started.DestinationGridY = destinationGrid.y;
            started.HasWorldPosition = true;
            started.StartWorldX = start.x;
            started.StartWorldY = start.y;
            started.DestinationWorldX = destination.x;
            started.DestinationWorldY = destination.y;
            started.WorldUnitsPerTick = worldUnitsPerTick;
            started.ExpectedArrivalTick = expectedArrivalTick;
            started.SeedStreamName = CreateSeedStreamName(context.SessionId, started.TravelId);

            result.Success = true;
            result.Started = started;
            return result;
        }

        public ShelteredExpeditionTravelReadResult TryBuildTravelCorrected(ExplorationParty party, string reason)
        {
            ShelteredExpeditionTravelReadResult result = new ShelteredExpeditionTravelReadResult();
            ShelteredMultiplayerSessionContext context = ShelteredMultiplayerSessionCoordinator.Instance.Context;
            if (context == null || !context.IsMultiplayerActive)
                return Fail(result, "multiplayer-inactive");
            if (party == null)
                return Fail(result, "missing-party");

            ExpeditionMap.GridRef currentGrid;
            ExpeditionMap.GridRef destinationGrid;
            if (!TryWorldToGrid(party.location, out currentGrid))
                return Fail(result, "current-grid-extraction-failed");

            Vector2 destination = ResolveCorrectionDestination(party, reason);
            if (!TryWorldToGrid(destination, out destinationGrid))
                return Fail(result, "destination-grid-extraction-failed");

            long correctionTick = context.WorldTick;
            ShelteredTravelCorrectedEvent corrected = new ShelteredTravelCorrectedEvent();
            corrected.TravelId = ResolveTravelIdForParty(context, party.id, correctionTick);
            corrected.CorrectionTick = correctionTick;
            corrected.CorrectedGridX = currentGrid.x;
            corrected.CorrectedGridY = currentGrid.y;
            corrected.DestinationGridX = destinationGrid.x;
            corrected.DestinationGridY = destinationGrid.y;
            corrected.HasWorldPosition = true;
            corrected.CorrectedWorldX = party.location.x;
            corrected.CorrectedWorldY = party.location.y;
            corrected.DestinationWorldX = destination.x;
            corrected.DestinationWorldY = destination.y;
            corrected.WorldUnitsPerTick = CalculateWorldUnitsPerTick(party, context);
            corrected.ExpectedArrivalTick = correctionTick + CalculateTicksForDistance(Vector2.Distance(party.location, destination), party, context);
            corrected.Reason = reason ?? ShelteredTravelCorrectionReasons.HostCorrection;

            result.Success = true;
            result.Corrected = corrected;
            return result;
        }

        public ShelteredExpeditionTravelReadResult TryBuildTravelArrived(ExplorationParty party, string resultKind)
        {
            ShelteredExpeditionTravelReadResult result = new ShelteredExpeditionTravelReadResult();
            ShelteredMultiplayerSessionContext context = ShelteredMultiplayerSessionCoordinator.Instance.Context;
            if (context == null || !context.IsMultiplayerActive)
                return Fail(result, "multiplayer-inactive");
            if (party == null)
                return Fail(result, "missing-party");

            ExpeditionMap.GridRef arrivalGrid;
            if (!TryWorldToGrid(party.location, out arrivalGrid))
                return Fail(result, "arrival-grid-extraction-failed");

            long arrivalTick = context.WorldTick;
            ShelteredTravelArrivedEvent arrived = new ShelteredTravelArrivedEvent();
            arrived.TravelId = ResolveTravelIdForParty(context, party.id, arrivalTick);
            arrived.ArrivalTick = arrivalTick;
            arrived.ArrivalGridX = arrivalGrid.x;
            arrived.ArrivalGridY = arrivalGrid.y;
            arrived.HasWorldPosition = true;
            arrived.ArrivalWorldX = party.location.x;
            arrived.ArrivalWorldY = party.location.y;
            arrived.ResultKind = string.IsNullOrEmpty(resultKind) ? ShelteredTravelArrivalKinds.Arrived : resultKind;
            arrived.ResultPayloadJson = "{\"partyId\":" + party.id.ToString(CultureInfo.InvariantCulture) + "}";

            result.Success = true;
            result.Arrived = arrived;
            return result;
        }

        internal static bool IsArrivalState(ExplorationParty.ePartyState state)
        {
            return state == ExplorationParty.ePartyState.EnteringShelter
                || state == ExplorationParty.ePartyState.EnteringShelterNextUpdate
                || state == ExplorationParty.ePartyState.ReturnedShowExperienceGained
                || state == ExplorationParty.ePartyState.ReturnedShowHazmatExperienceGained
                || state == ExplorationParty.ePartyState.Finished;
        }

        internal static bool IsInterruptionState(ExplorationParty.ePartyState state)
        {
            return state == ExplorationParty.ePartyState.EncounteredNPCsStart
                || state == ExplorationParty.ePartyState.OpenGroundNpcEncounterStart
                || state == ExplorationParty.ePartyState.QuestEncounterStart
                || state == ExplorationParty.ePartyState.EncounteredQuestNPCs;
        }

        internal static string ReasonForState(ExplorationParty.ePartyState state)
        {
            if (state == ExplorationParty.ePartyState.OpenGroundNpcEncounterStart
                || state == ExplorationParty.ePartyState.EncounteredNPCsStart
                || state == ExplorationParty.ePartyState.QuestEncounterStart
                || state == ExplorationParty.ePartyState.EncounteredQuestNPCs)
            {
                return ShelteredTravelCorrectionReasons.Ambush;
            }

            return ShelteredTravelCorrectionReasons.HostCorrection;
        }

        private static ShelteredExpeditionTravelReadResult Fail(ShelteredExpeditionTravelReadResult result, string error)
        {
            result.Success = false;
            result.Error = error ?? string.Empty;
            return result;
        }

        private static Vector2 ResolveStart(ExplorationParty party, IList<Vector2> route)
        {
            if (party != null && party.location != Vector2.zero)
                return party.location;
            return route[0];
        }

        private static Vector2 ResolvePrimaryDestination(IList<Vector2> route)
        {
            if (route == null || route.Count == 0)
                return Vector2.zero;
            return route[ResolvePrimaryDestinationIndex(route)];
        }

        private static Vector2 ResolveCorrectionDestination(ExplorationParty party, string reason)
        {
            IList<Vector2> route = party.GetRoute();
            if (string.Equals(reason, ShelteredTravelCorrectionReasons.Recall, StringComparison.Ordinal)
                || string.Equals(reason, ShelteredTravelCorrectionReasons.Stopped, StringComparison.Ordinal))
            {
                return route != null && route.Count > 0 ? route[route.Count - 1] : Vector2.zero;
            }

            return ResolvePrimaryDestination(route);
        }

        private static bool TryWorldToGrid(Vector2 world, out ExpeditionMap.GridRef grid)
        {
            grid = new ExpeditionMap.GridRef(0, 0);
            if (ExpeditionMap.Instance == null)
                return false;

            grid = ExpeditionMap.Instance.WorldPosToGridRef(world);
            return true;
        }

        private static long CalculateExpectedArrivalTick(
            IList<Vector2> route,
            ExplorationParty party,
            Vector2 start,
            Vector2 destination,
            long startTick,
            ShelteredMultiplayerSessionContext context)
        {
            float distance = CalculateDistanceToDestination(route, start, destination);
            return startTick + CalculateTicksForDistance(distance, party, context);
        }

        private static int ResolvePrimaryDestinationIndex(IList<Vector2> route)
        {
            if (route == null || route.Count == 0)
                return 0;
            if (route.Count > 2 && Vector2.Distance(route[0], route[route.Count - 1]) <= Mathf.Epsilon)
                return route.Count - 2;
            return route.Count - 1;
        }

        private static float CalculateDistanceToDestination(IList<Vector2> route, Vector2 start, Vector2 destination)
        {
            if (route == null || route.Count == 0)
                return Vector2.Distance(start, destination);

            int destinationIndex = ResolvePrimaryDestinationIndex(route);
            if (destinationIndex < 0 || destinationIndex >= route.Count)
                return Vector2.Distance(start, destination);

            float distance = 0f;
            Vector2 previous = start;
            int firstRouteIndex = Vector2.Distance(start, route[0]) <= Mathf.Epsilon ? 1 : 0;

            for (int i = firstRouteIndex; i <= destinationIndex; i++)
            {
                distance += Vector2.Distance(previous, route[i]);
                previous = route[i];
            }

            if (Vector2.Distance(previous, destination) > Mathf.Epsilon)
                distance += Vector2.Distance(previous, destination);

            return distance;
        }

        private static long CalculateTicksForDistance(float distanceWorldUnits, ExplorationParty party, ShelteredMultiplayerSessionContext context)
        {
            float unitsPerTick = CalculateWorldUnitsPerTick(party, context);
            if (unitsPerTick <= 0f)
                return 1;

            long ticks = (long)Math.Ceiling(distanceWorldUnits / unitsPerTick);
            return ticks > 0 ? ticks : 1;
        }

        private static float CalculateWorldUnitsPerTick(ExplorationParty party, ShelteredMultiplayerSessionContext context)
        {
            float travelSpeed = ReadFloat(TravelSpeedField, party, 0.75f);
            float speedModifier = ReadFloat(SpeedModifierField, party, 1f);
            float worldUnitsPerMile = ExplorationManager.Instance != null ? ExplorationManager.Instance.worldUnitsPerMile : 1f;
            int tickRate = context != null && context.TickRate > 0 ? context.TickRate : ShelteredMultiplayerWorldClock.DefaultTickRate;
            float gameSecondsPerRealSecond = GameTime.RealSecondsToGameSeconds(1f);
            float unitsPerRealSecond = travelSpeed * speedModifier * worldUnitsPerMile / 3600f * gameSecondsPerRealSecond;
            return ShelteredMultiplayerTimePolicy.ApplyTravelDistance(unitsPerRealSecond) / tickRate;
        }

        private static float ReadFloat(FieldInfo field, object instance, float fallback)
        {
            if (field == null || instance == null)
                return fallback;

            object value = field.GetValue(instance);
            return value is float ? (float)value : fallback;
        }

        private static string ResolveTravelIdForParty(ShelteredMultiplayerSessionContext context, int partyId, long worldTick)
        {
            IList<ShelteredTravelState> active = ShelteredExpeditionTravelHookService.Instance.Registry.GetActive();
            for (int i = 0; i < active.Count; i++)
            {
                if (active[i] != null && active[i].PartyId == partyId)
                    return active[i].TravelId;
            }

            return CreateTravelId(context, partyId, worldTick);
        }

        private static string CreateTravelId(ShelteredMultiplayerSessionContext context, int partyId, long tick)
        {
            string sessionId = context != null ? context.SessionId : string.Empty;
            int ownerPlayerId = context != null ? context.LocalPlayerId : 0;
            return TravelIdPrefix + ":" + NormalizeIdPart(sessionId) + ":"
                + ownerPlayerId.ToString(CultureInfo.InvariantCulture) + ":"
                + partyId.ToString(CultureInfo.InvariantCulture) + ":"
                + tick.ToString(CultureInfo.InvariantCulture);
        }

        private static string CreateSeedStreamName(string sessionId, string travelId)
        {
            return SeedStreamPrefix + "." + NormalizeIdPart(sessionId) + "." + NormalizeIdPart(travelId);
        }

        private static string NormalizeIdPart(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "none";

            char[] chars = value.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '.' || c == '_' || c == '-')
                    continue;
                chars[i] = '_';
            }

            return new string(chars);
        }

        internal static void WarnExtractionFailed(string source, ShelteredExpeditionTravelReadResult result)
        {
            if (result == null || result.Success)
                return;

            MMLog.WarnOnce(
                LogSource + "." + (source ?? string.Empty) + "." + result.Error,
                "Skipped expedition travel event from " + (source ?? string.Empty) + ": " + result.Error + ".");
        }
    }
}
