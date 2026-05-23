using System;
using System.Collections.Generic;

namespace ShelteredAPI.Map
{
    /// <summary>
    /// A location in the vanilla expedition-map grid.
    /// </summary>
    public struct ExpeditionMapGridPosition : IEquatable<ExpeditionMapGridPosition>
    {
        /// <summary>Creates a grid coordinate.</summary>
        public ExpeditionMapGridPosition(int x, int y)
        {
            X = x;
            Y = y;
        }

        /// <summary>Horizontal grid index.</summary>
        public int X { get; private set; }
        /// <summary>Vertical grid index.</summary>
        public int Y { get; private set; }

        /// <inheritdoc />
        public bool Equals(ExpeditionMapGridPosition other)
        {
            return X == other.X && Y == other.Y;
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return obj is ExpeditionMapGridPosition && Equals((ExpeditionMapGridPosition)obj);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return (X * 397) ^ Y;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return "(" + X + ", " + Y + ")";
        }
    }

    /// <summary>
    /// A world-space expedition-map location without exposing a live Unity or Sheltered object.
    /// </summary>
    public struct ExpeditionMapWorldPosition : IEquatable<ExpeditionMapWorldPosition>
    {
        /// <summary>Creates a world coordinate.</summary>
        public ExpeditionMapWorldPosition(float x, float y)
        {
            X = x;
            Y = y;
        }

        /// <summary>Horizontal world coordinate.</summary>
        public float X { get; private set; }
        /// <summary>Vertical world coordinate.</summary>
        public float Y { get; private set; }

        /// <inheritdoc />
        public bool Equals(ExpeditionMapWorldPosition other)
        {
            return X.Equals(other.X) && Y.Equals(other.Y);
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return obj is ExpeditionMapWorldPosition && Equals((ExpeditionMapWorldPosition)obj);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return (X.GetHashCode() * 397) ^ Y.GetHashCode();
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return "(" + X + ", " + Y + ")";
        }
    }

    /// <summary>
    /// Read-only result of route distance calculation in vanilla world units and miles.
    /// </summary>
    public sealed class ExpeditionRouteDistance
    {
        internal ExpeditionRouteDistance(float worldUnits, float miles, bool includesHomeLegs)
        {
            WorldUnits = worldUnits;
            Miles = miles;
            IncludesHomeLegs = includesHomeLegs;
        }

        /// <summary>Total distance measured in expedition world units.</summary>
        public float WorldUnits { get; private set; }
        /// <summary>Total distance measured using the active map's world-units-per-mile value.</summary>
        public float Miles { get; private set; }
        /// <summary>Whether the calculated path includes home-to-route and route-to-home legs.</summary>
        public bool IncludesHomeLegs { get; private set; }
    }

    /// <summary>
    /// Immutable snapshot of the active vanilla expedition map.
    /// A context may report partial facts while <see cref="IsValid"/> is false; conversion and
    /// distance methods only succeed for a generated, authoritative runtime map.
    /// </summary>
    public sealed class ExpeditionMapContext
    {
        private readonly float _gridScaleX;
        private readonly float _gridScaleY;
        private readonly float _worldWidth;
        private readonly float _worldHeight;

        internal ExpeditionMapContext(
            bool isAvailable,
            bool isValid,
            string unavailableReason,
            int currentWidth,
            int currentHeight,
            int vanillaWidth,
            int vanillaHeight,
            float scaleFactor,
            bool hasScaleFactor,
            float densityMultiplier,
            bool hasDensityMultiplier,
            int mapSeed,
            bool hasMapSeed,
            ExpeditionMapWorldPosition homeShelterWorldPosition,
            ExpeditionMapGridPosition homeShelterGridPosition,
            bool hasHomeShelterPosition,
            float worldUnitsPerMile,
            bool hasWorldUnitsPerMile,
            float worldWidth,
            float worldHeight)
        {
            IsAvailable = isAvailable;
            IsValid = isValid;
            UnavailableReason = unavailableReason;
            CurrentWidth = currentWidth;
            CurrentHeight = currentHeight;
            VanillaWidth = vanillaWidth;
            VanillaHeight = vanillaHeight;
            ScaleFactor = scaleFactor;
            HasScaleFactor = hasScaleFactor;
            DensityMultiplier = densityMultiplier;
            HasDensityMultiplier = hasDensityMultiplier;
            MapSeed = mapSeed;
            HasMapSeed = hasMapSeed;
            HomeShelterWorldPosition = homeShelterWorldPosition;
            HomeShelterGridPosition = homeShelterGridPosition;
            HasHomeShelterPosition = hasHomeShelterPosition;
            WorldUnitsPerMile = worldUnitsPerMile;
            HasWorldUnitsPerMile = hasWorldUnitsPerMile;
            _worldWidth = worldWidth;
            _worldHeight = worldHeight;
            _gridScaleX = worldWidth > 0f ? currentWidth / worldWidth : 0f;
            _gridScaleY = worldHeight > 0f ? currentHeight / worldHeight : 0f;
        }

        /// <summary>Whether live expedition map and exploration manager instances were found.</summary>
        public bool IsAvailable { get; private set; }
        /// <summary>Whether this snapshot represents a completed generated map suitable for conversions.</summary>
        public bool IsValid { get; private set; }
        /// <summary>Reason this context cannot be used for authoritative coordinate operations, or null.</summary>
        public string UnavailableReason { get; private set; }
        /// <summary>Current generated map grid width.</summary>
        public int CurrentWidth { get; private set; }
        /// <summary>Current generated map grid height.</summary>
        public int CurrentHeight { get; private set; }
        /// <summary>Vanilla normal-map baseline width.</summary>
        public int VanillaWidth { get; private set; }
        /// <summary>Vanilla normal-map baseline height.</summary>
        public int VanillaHeight { get; private set; }
        /// <summary>Width-derived ratio between current and vanilla normal map size.</summary>
        public float ScaleFactor { get; private set; }
        /// <summary>Whether <see cref="ScaleFactor"/> is based on a generated map.</summary>
        public bool HasScaleFactor { get; private set; }
        /// <summary>Location-density multiplier when one authoritative runtime value exists.</summary>
        public float DensityMultiplier { get; private set; }
        /// <summary>Whether <see cref="DensityMultiplier"/> is known; vanilla does not expose a single value.</summary>
        public bool HasDensityMultiplier { get; private set; }
        /// <summary>Vanilla expedition generation seed, when assigned.</summary>
        public int MapSeed { get; private set; }
        /// <summary>Whether a non-zero vanilla expedition generation seed has been assigned.</summary>
        public bool HasMapSeed { get; private set; }
        /// <summary>Home shelter location in expedition world coordinates.</summary>
        public ExpeditionMapWorldPosition HomeShelterWorldPosition { get; private set; }
        /// <summary>Home shelter location in expedition grid coordinates.</summary>
        public ExpeditionMapGridPosition HomeShelterGridPosition { get; private set; }
        /// <summary>Whether home shelter positions were resolved from this map.</summary>
        public bool HasHomeShelterPosition { get; private set; }
        /// <summary>Vanilla conversion factor from route world units to route miles.</summary>
        public float WorldUnitsPerMile { get; private set; }
        /// <summary>Whether <see cref="WorldUnitsPerMile"/> is available for distance conversion.</summary>
        public bool HasWorldUnitsPerMile { get; private set; }

        /// <summary>Returns whether a grid location falls inside this generated map.</summary>
        public bool ContainsGridPosition(ExpeditionMapGridPosition position)
        {
            return IsValid
                && position.X >= 0
                && position.X < CurrentWidth
                && position.Y >= 0
                && position.Y < CurrentHeight;
        }

        /// <summary>
        /// Converts world coordinates using vanilla ExpeditionMap grid semantics.
        /// Out-of-map negative world positions can produce negative grid positions, matching vanilla.
        /// </summary>
        public bool TryWorldToGrid(ExpeditionMapWorldPosition position, out ExpeditionMapGridPosition gridPosition)
        {
            gridPosition = new ExpeditionMapGridPosition();
            if (!CanConvertCoordinates())
                return false;

            int x = (int)((position.X + (_worldWidth * 0.5f)) * _gridScaleX);
            int y = (int)((position.Y + (_worldHeight * 0.5f)) * _gridScaleY);
            x = Math.Min(x, CurrentWidth - 1);
            y = Math.Min(y, CurrentHeight - 1);
            gridPosition = new ExpeditionMapGridPosition(x, y);
            return true;
        }

        /// <summary>Converts a valid grid location to its lower-left expedition world position.</summary>
        public bool TryGridToWorld(ExpeditionMapGridPosition gridPosition, out ExpeditionMapWorldPosition worldPosition)
        {
            worldPosition = new ExpeditionMapWorldPosition();
            if (!CanConvertCoordinates() || !ContainsGridPosition(gridPosition))
                return false;

            worldPosition = new ExpeditionMapWorldPosition(
                (gridPosition.X / _gridScaleX) - (_worldWidth * 0.5f),
                (gridPosition.Y / _gridScaleY) - (_worldHeight * 0.5f));
            return true;
        }

        /// <summary>Converts a valid grid location to its cell-center expedition world position.</summary>
        public bool TryGridToWorldCenter(ExpeditionMapGridPosition gridPosition, out ExpeditionMapWorldPosition worldPosition)
        {
            worldPosition = new ExpeditionMapWorldPosition();
            if (!TryGridToWorld(gridPosition, out worldPosition))
                return false;

            worldPosition = new ExpeditionMapWorldPosition(
                worldPosition.X + (0.5f / _gridScaleX),
                worldPosition.Y + (0.5f / _gridScaleY));
            return true;
        }

        /// <summary>Calculates straight-line distance between two expedition world positions.</summary>
        public bool TryCalculateDistance(
            ExpeditionMapWorldPosition from,
            ExpeditionMapWorldPosition to,
            out ExpeditionRouteDistance distance)
        {
            distance = null;
            if (!IsValid || !HasWorldUnitsPerMile || WorldUnitsPerMile <= 0f)
                return false;

            float worldUnits = DistanceBetween(from, to);
            distance = new ExpeditionRouteDistance(worldUnits, worldUnits / WorldUnitsPerMile, false);
            return true;
        }

        /// <summary>
        /// Calculates distance through the provided waypoints. With home legs enabled, the result
        /// matches the normal expedition planner's shelter-to-route-to-shelter distance shape.
        /// </summary>
        public bool TryCalculateRouteDistance(
            IList<ExpeditionMapWorldPosition> waypoints,
            bool includeHomeLegs,
            out ExpeditionRouteDistance distance)
        {
            distance = null;
            if (!IsValid
                || !HasWorldUnitsPerMile
                || WorldUnitsPerMile <= 0f
                || waypoints == null
                || waypoints.Count == 0
                || (includeHomeLegs && !HasHomeShelterPosition))
            {
                return false;
            }

            float worldUnits = 0f;
            if (includeHomeLegs)
                worldUnits += DistanceBetween(HomeShelterWorldPosition, waypoints[0]);

            for (int i = 1; i < waypoints.Count; i++)
                worldUnits += DistanceBetween(waypoints[i - 1], waypoints[i]);

            if (includeHomeLegs)
                worldUnits += DistanceBetween(waypoints[waypoints.Count - 1], HomeShelterWorldPosition);

            distance = new ExpeditionRouteDistance(worldUnits, worldUnits / WorldUnitsPerMile, includeHomeLegs);
            return true;
        }

        private bool CanConvertCoordinates()
        {
            return IsValid
                && CurrentWidth > 0
                && CurrentHeight > 0
                && _worldWidth > 0f
                && _worldHeight > 0f
                && _gridScaleX > 0f
                && _gridScaleY > 0f;
        }

        private static float DistanceBetween(ExpeditionMapWorldPosition from, ExpeditionMapWorldPosition to)
        {
            float x = to.X - from.X;
            float y = to.Y - from.Y;
            return (float)Math.Sqrt((x * x) + (y * y));
        }
    }
}
