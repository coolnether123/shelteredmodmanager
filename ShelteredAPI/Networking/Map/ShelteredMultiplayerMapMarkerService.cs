using System;
using System.Collections.Generic;
using ModAPI.Networking;
using ShelteredAPI.Bunkers;
using UnityEngine;

namespace ShelteredAPI.Networking.Map
{
    internal sealed class ShelteredMultiplayerMapMarkerService
    {
        private static readonly ShelteredMultiplayerMapMarkerService _instance =
            new ShelteredMultiplayerMapMarkerService();

        public static ShelteredMultiplayerMapMarkerService Instance
        {
            get { return _instance; }
        }

        private ShelteredMultiplayerMapMarkerService()
        {
        }

        public List<ShelteredMultiplayerMapMarker> BuildBunkerMarkers()
        {
            List<ShelteredMultiplayerMapMarker> markers = new List<ShelteredMultiplayerMapMarker>();
            ShelteredMultiplayerSessionContext context = ShelteredMultiplayerSessionCoordinator.Instance.Context;
            if (context == null || !context.IsMultiplayerActive)
                return markers;

            ExplorationManager manager = ExplorationManager.Instance;
            if (manager == null || manager.mapSourceSprite == null)
                return markers;

            int localBunkerOwnerId = ShelteredMultiplayerMapMarkerAssignmentResolver.ResolveLocalBunkerOwnerId(
                context.BunkerAssignments,
                context.LocalPlayerId);

            IEnumerable<BunkerDefinition> bunkers = ShelteredBunkers.GetAllBunkers();
            if (bunkers == null)
                return markers;

            foreach (BunkerDefinition bunker in bunkers)
            {
                if (bunker == null)
                    continue;

                ShelteredMultiplayerBunkerAssignmentRecord assignment =
                    ShelteredMultiplayerMapMarkerAssignmentResolver.FindAssignment(
                        context.BunkerAssignments,
                        bunker.BunkerOwnerId);
                bool isLocal = bunker.BunkerOwnerId == localBunkerOwnerId;

                markers.Add(new ShelteredMultiplayerMapMarker(
                    ShelteredMultiplayerMapMarkerAssignmentResolver.CreateMarkerId(bunker.BunkerOwnerId),
                    ShelteredMultiplayerMapMarkerAssignmentResolver.ResolveLabel(bunker, assignment),
                    bunker.BunkerOwnerId,
                    ShelteredMultiplayerMapMarkerAssignmentResolver.ResolvePeerId(bunker, assignment),
                    ResolveMapPixels(manager, bunker, isLocal),
                    isLocal,
                    ShelteredMultiplayerMapMarkerAssignmentResolver.ResolveOnlineState(bunker, assignment)));
            }

            markers.Sort(CompareMarkers);
            return markers;
        }

        private static Vector3 ResolveMapPixels(ExplorationManager manager, BunkerDefinition bunker, bool isLocal)
        {
            if (isLocal)
            {
                Vector3 cached = ShelteredMultiplayerBunkerAnchorRuntime.GetActiveBunkerMapPixels();
                if (cached.sqrMagnitude > 0.0001f)
                    return cached;
            }

            try
            {
                return new Vector3(
                    manager.WorldToMapPixelsX(bunker.Position.x),
                    manager.WorldToMapPixelsY(bunker.Position.y),
                    0f);
            }
            catch
            {
                return Vector3.zero;
            }
        }

        private static int CompareMarkers(
            ShelteredMultiplayerMapMarker left,
            ShelteredMultiplayerMapMarker right)
        {
            if (left == null && right == null)
                return 0;
            if (left == null)
                return -1;
            if (right == null)
                return 1;

            return left.BunkerOwnerId.CompareTo(right.BunkerOwnerId);
        }
    }

    internal static class ShelteredMultiplayerMapMarkerAssignmentResolver
    {
        public static int ResolveLocalBunkerOwnerId(
            ShelteredMultiplayerBunkerAssignmentRecord[] assignments,
            int localPlayerId)
        {
            return ShelteredMultiplayerBunkerAssignments.ResolveBunkerOwnerId(assignments, localPlayerId);
        }

        public static ShelteredMultiplayerBunkerAssignmentRecord FindAssignment(
            ShelteredMultiplayerBunkerAssignmentRecord[] assignments,
            int bunkerOwnerId)
        {
            if (assignments == null)
                return null;

            for (int i = 0; i < assignments.Length; i++)
            {
                ShelteredMultiplayerBunkerAssignmentRecord assignment = assignments[i];
                if (assignment != null && assignment.BunkerOwnerId == bunkerOwnerId)
                    return assignment;
            }

            return null;
        }

        public static string CreateMarkerId(int bunkerOwnerId)
        {
            return "multiplayer-bunker-" + bunkerOwnerId;
        }

        public static string ResolveLabel(
            BunkerDefinition bunker,
            ShelteredMultiplayerBunkerAssignmentRecord assignment)
        {
            if (assignment != null && !string.IsNullOrEmpty(assignment.DisplayName))
                return assignment.DisplayName;
            if (bunker != null && !string.IsNullOrEmpty(bunker.DisplayName))
                return bunker.DisplayName;

            int bunkerOwnerId = bunker != null ? bunker.BunkerOwnerId : 0;
            return bunkerOwnerId == 0 ? "Host Bunker" : "Remote Bunker " + bunkerOwnerId;
        }

        public static byte ResolvePeerId(
            BunkerDefinition bunker,
            ShelteredMultiplayerBunkerAssignmentRecord assignment)
        {
            if (assignment != null)
                return assignment.NetworkPeerId;
            if (bunker != null)
                return bunker.PeerId;

            return NetworkDefaults.UnassignedPeerId;
        }

        public static bool ResolveOnlineState(
            BunkerDefinition bunker,
            ShelteredMultiplayerBunkerAssignmentRecord assignment)
        {
            if (assignment != null)
                return assignment.IsOnline;
            return bunker == null || bunker.IsOnline;
        }
    }
}
