using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShelteredAPI.Bunkers
{
    /// <summary>
    /// Defines supported spatial deployment strategies for the player bunker.
    /// </summary>
    public enum BunkerLocationMode
    {
        /// <summary>Place the bunker at the center/origin.</summary>
        Center,
        /// <summary>Place the bunker in the top-left map quadrant.</summary>
        TopLeft,
        /// <summary>Place the bunker in the top-right map quadrant.</summary>
        TopRight,
        /// <summary>Place the bunker in the bottom-left map quadrant.</summary>
        BottomLeft,
        /// <summary>Place the bunker in the bottom-right map quadrant.</summary>
        BottomRight,
        /// <summary>Choose one of the four map quadrants randomly.</summary>
        RandomQuadrant,
        /// <summary>Choose a random position inside the configured placement bounds.</summary>
        FullyRandom
    }

    /// <summary>
    /// Immutable snapshot of bunker-generation settings that should travel with a save/session.
    /// </summary>
    [Serializable]
    public struct BunkerSessionSettings
    {
        /// <summary>The placement algorithm used when the world/session was generated.</summary>
        public BunkerLocationMode LocationMode;
        /// <summary>Whether starter houses should be generated near the primary bunker.</summary>
        public bool EnableStartingHouses;
        /// <summary>Whether vanilla-style clearance around the shelter should be preserved.</summary>
        public bool PreserveShelterClearance;

        /// <summary>Creates a settings snapshot from the supplied generation settings.</summary>
        public BunkerSessionSettings(BunkerLocationMode locationMode, bool enableStartingHouses, bool preserveShelterClearance)
        {
            LocationMode = locationMode;
            EnableStartingHouses = enableStartingHouses;
            PreserveShelterClearance = preserveShelterClearance;
        }
    }

    /// <summary>
    /// Represents a single bunker instance on the world map.
    /// </summary>
    [Serializable]
    public class BunkerDefinition
    {
        /// <summary>
        /// Unique bunker owner identifier. ID 0 is reserved for the host/local shelter.
        /// </summary>
        public int Id;

        /// <summary>
        /// Alias for Id, used by multiplayer code where player IDs and bunker IDs are separate concerns.
        /// </summary>
        public int BunkerOwnerId
        {
            get { return Id; }
            set { Id = value; }
        }

        /// <summary>
        /// Network peer that owns this bunker during the active session.
        /// </summary>
        public byte PeerId;

        /// <summary>
        /// Geographical world position of the bunker entrance.
        /// </summary>
        public Vector2 Position;

        /// <summary>
        /// Optional display name for UI elements.
        /// </summary>
        public string DisplayName;

        /// <summary>
        /// Whether starter houses should be generated near this bunker.
        /// </summary>
        public bool EnableStarterHouses;

        /// <summary>
        /// Whether the owner of this bunker is currently connected/active.
        /// </summary>
        public bool IsOnline = true;

        /// <summary>Creates a bunker definition with default display and starter-house settings.</summary>
        public BunkerDefinition(int id, Vector2 position)
            : this(id, position, string.Empty, true, true, 255)
        {
        }

        /// <summary>Creates a bunker definition with a display name.</summary>
        public BunkerDefinition(int id, Vector2 position, string displayName)
            : this(id, position, displayName, true, true, 255)
        {
        }

        /// <summary>Creates a bunker definition with display and starter-house settings.</summary>
        public BunkerDefinition(int id, Vector2 position, string displayName, bool enableStarterHouses)
            : this(id, position, displayName, enableStarterHouses, true, 255)
        {
        }

        /// <summary>Creates a bunker definition with all public state supplied explicitly.</summary>
        public BunkerDefinition(int id, Vector2 position, string displayName, bool enableStarterHouses, bool isOnline)
            : this(id, position, displayName, enableStarterHouses, isOnline, 255)
        {
        }

        /// <summary>Creates a bunker definition with all public state supplied explicitly.</summary>
        public BunkerDefinition(int id, Vector2 position, string displayName, bool enableStarterHouses, bool isOnline, byte peerId)
        {
            Id = id;
            PeerId = peerId;
            Position = position;
            DisplayName = displayName;
            EnableStarterHouses = enableStarterHouses;
            IsOnline = isOnline;
        }

        /// <summary>Creates a detached copy suitable for persistence snapshots.</summary>
        public BunkerDefinition Clone()
        {
            return new BunkerDefinition(Id, Position, DisplayName, EnableStarterHouses, IsOnline, PeerId);
        }
    }

    /// <summary>
    /// Map-facing bunker snapshot for UI and expedition-map consumers.
    /// </summary>
    [Serializable]
    public sealed class BunkerMapRecord
    {
        public BunkerMapRecord(
            byte peerId,
            int bunkerOwnerId,
            string displayName,
            Vector2 worldPosition,
            Vector3 mapPixels,
            ExpeditionMap.GridRef gridRef,
            bool isOnline)
        {
            PeerId = peerId;
            BunkerOwnerId = bunkerOwnerId;
            DisplayName = displayName ?? string.Empty;
            WorldPosition = worldPosition;
            MapPixels = mapPixels;
            GridRef = gridRef;
            IsOnline = isOnline;
        }

        public readonly byte PeerId;
        public readonly int BunkerOwnerId;
        public readonly string DisplayName;
        public readonly Vector2 WorldPosition;
        public readonly Vector3 MapPixels;
        public readonly ExpeditionMap.GridRef GridRef;
        public readonly bool IsOnline;
    }

    /// <summary>
    /// Public contract for bunker state, placement, and coordinate management.
    /// </summary>
    public interface IShelteredBunkerService
    {
        /// <summary>
        /// Raised when any bunker is created, relocated, or has active-state changes.
        /// </summary>
        event Action<BunkerDefinition> OnBunkerChanged;

        /// <summary>
        /// Gets the placement mode used for primary bunker generation.
        /// </summary>
        BunkerLocationMode LocationMode { get; }

        /// <summary>
        /// Gets the current local-view bunker owner ID used by contextual UI calls.
        /// </summary>
        int ActivePlayerId { get; }

        /// <summary>Gets the primary local bunker, using owner ID 0.</summary>
        BunkerDefinition GetPrimaryBunker();

        /// <summary>Gets a bunker by logical owner ID, or null if none is registered.</summary>
        BunkerDefinition GetBunker(int id);

        /// <summary>Gets all registered bunker definitions.</summary>
        IEnumerable<BunkerDefinition> GetAllBunkers();

        /// <summary>Gets a map-facing bunker snapshot by owner ID, or null if none is registered.</summary>
        BunkerMapRecord GetBunkerMapRecord(int id);

        /// <summary>Gets map-facing snapshots for all registered bunkers.</summary>
        IEnumerable<BunkerMapRecord> GetAllBunkerMapRecords();

        /// <summary>
        /// Creates or refreshes the bunker for the supplied logical owner ID.
        /// Placement strategy and safety spacing are handled by the service.
        /// </summary>
        BunkerDefinition RequestNewBunker(int userId, string displayName = "", bool enableStarterHouses = true, bool force = false);

        /// <summary>Creates or refreshes a bunker from an authoritative definition.</summary>
        void RegisterBunker(BunkerDefinition bunker);

        /// <summary>Creates or moves a bunker to the supplied world position.</summary>
        void SetBunkerPosition(int id, Vector2 position);

        /// <summary>Sets whether a bunker owner is currently online/active.</summary>
        void SetBunkerOnline(int id, bool online);

        /// <summary>Sets the owner ID used for active/local contextual lookups.</summary>
        void SetActivePlayerId(int id);

        /// <summary>Sets the placement mode used by future primary bunker generation.</summary>
        void SetLocationMode(BunkerLocationMode mode);

        /// <summary>
        /// Checks whether a world position is home for any registered bunker.
        /// </summary>
        bool IsAnyHome(Vector2 worldPos, float tolerance = 0.1f);

        /// <summary>
        /// Calculates a primary bunker position using the supplied mode and the active map size.
        /// </summary>
        Vector2 CalculatePrimaryPosition();

        /// <summary>
        /// Calculates a primary bunker position using the supplied mode and the active map size.
        /// </summary>
        Vector2 CalculatePrimaryPosition(BunkerLocationMode mode);

        /// <summary>
        /// Calculates a safe position for a secondary bunker using existing registered bunkers.
        /// </summary>
        Vector2 CalculateSecondaryPosition();

        /// <summary>
        /// Gets the world position for the active bunker, or zero if unavailable.
        /// </summary>
        Vector2 GetActiveBunkerWorldPosition();

        /// <summary>
        /// Gets the world position for a bunker, or zero if unavailable.
        /// </summary>
        Vector2 GetBunkerWorldPosition(int id);

        /// <summary>
        /// Gets the map-pixel position for the active bunker, or zero if unavailable.
        /// </summary>
        Vector3 GetActiveBunkerMapPixels();

        /// <summary>
        /// Gets the map-pixel position for a bunker, or zero if unavailable.
        /// </summary>
        Vector3 GetBunkerMapPixels(int id);

        /// <summary>
        /// Gets the expedition grid reference for the active bunker, or (0,0) if unavailable.
        /// </summary>
        ExpeditionMap.GridRef GetActiveBunkerGridRef();

        /// <summary>
        /// Gets the expedition grid reference for a bunker, or (0,0) if unavailable.
        /// </summary>
        ExpeditionMap.GridRef GetBunkerGridRef(int id);

        /// <summary>Loads a batch of persisted bunker definitions into the service.</summary>
        void LoadDefinitions(List<BunkerDefinition> bunkers);

        /// <summary>Gets a detached snapshot of registered definitions for persistence.</summary>
        List<BunkerDefinition> GetDefinitions();

        /// <summary>Clears all registered bunker state.</summary>
        void Clear();
    }
}
