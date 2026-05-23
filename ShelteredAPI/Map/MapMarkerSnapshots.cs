using System.Collections.Generic;
using System.Collections.ObjectModel;
using ModAPI.Actors;
using ShelteredAPI.Characters;
using ShelteredAPI.Scenarios.Domain.Map;
using UnityEngine;

namespace ShelteredAPI.Map
{
    /// <summary>
    /// Detached route data associated with a marker or expedition party.
    /// Waypoints are copied when a snapshot is created and never expose a live vanilla route list.
    /// </summary>
    public sealed class ExpeditionRouteSnapshot
    {
        private readonly ReadOnlyCollection<ExpeditionMapWorldPosition> _worldWaypoints;

        /// <summary>Creates a route snapshot by copying the supplied world-space waypoints.</summary>
        public ExpeditionRouteSnapshot(IEnumerable<ExpeditionMapWorldPosition> worldWaypoints)
        {
            List<ExpeditionMapWorldPosition> copy = new List<ExpeditionMapWorldPosition>();
            if (worldWaypoints != null)
            {
                foreach (ExpeditionMapWorldPosition waypoint in worldWaypoints)
                    copy.Add(waypoint);
            }

            _worldWaypoints = copy.AsReadOnly();
        }

        /// <summary>Copied route positions in vanilla expedition world coordinates.</summary>
        public ReadOnlyCollection<ExpeditionMapWorldPosition> WorldWaypoints
        {
            get { return _worldWaypoints; }
        }

        internal ExpeditionRouteSnapshot Clone()
        {
            return new ExpeditionRouteSnapshot(_worldWaypoints);
        }
    }

    /// <summary>
    /// Detached map marker data for vanilla projections or mod-owned markers.
    /// <see cref="Kind"/> reuses the scenario map marker vocabulary to keep one public marker-kind enum.
    /// </summary>
    public sealed class MapMarkerSnapshot
    {
        /// <summary>Creates an unpositioned point-of-interest marker DTO.</summary>
        public MapMarkerSnapshot()
        {
            MarkerId = string.Empty;
            DisplayName = string.Empty;
            Kind = MapMarkerKind.PointOfInterest;
            SourceModId = string.Empty;
        }

        /// <summary>Stable marker ID within <see cref="SourceModId"/>.</summary>
        public string MarkerId { get; set; }
        /// <summary>Optional mod-facing label for this projection.</summary>
        public string DisplayName { get; set; }
        /// <summary>Marker semantic category shared with authored scenario map markers.</summary>
        public MapMarkerKind Kind { get; set; }
        /// <summary>Single actor represented by this marker, when the marker has one stable actor identity.</summary>
        public ActorId ActorId { get; set; }
        /// <summary>Position in vanilla expedition map-pixel coordinates, when available.</summary>
        public Vector2? MapPosition { get; set; }
        /// <summary>Position in expedition grid coordinates, when a generated map context is available.</summary>
        public ExpeditionMapGridPosition? GridPosition { get; set; }
        /// <summary>Position in vanilla expedition world coordinates, when available.</summary>
        public ExpeditionMapWorldPosition? WorldPosition { get; set; }
        /// <summary>Whether the marker should currently be visible to mod-facing map consumers.</summary>
        public bool IsVisible { get; set; }
        /// <summary>Whether the backing map entity is discovered, rather than only visible.</summary>
        public bool IsDiscovered { get; set; }
        /// <summary>Owning mod ID, or <c>vanilla</c> for vanilla-projected snapshots.</summary>
        public string SourceModId { get; set; }
        /// <summary>Optional copied route for a moving or route-associated marker.</summary>
        public ExpeditionRouteSnapshot Route { get; set; }

        internal MapMarkerSnapshot Clone()
        {
            return new MapMarkerSnapshot
            {
                MarkerId = MarkerId,
                DisplayName = DisplayName,
                Kind = Kind,
                ActorId = ActorId == null ? null : new ActorId(ActorId.Kind, ActorId.LocalId, ActorId.Domain),
                MapPosition = MapPosition,
                GridPosition = GridPosition,
                WorldPosition = WorldPosition,
                IsVisible = IsVisible,
                IsDiscovered = IsDiscovered,
                SourceModId = SourceModId,
                Route = Route == null ? null : Route.Clone()
            };
        }
    }

    /// <summary>
    /// Detached view of an active player expedition and its projected moving map marker.
    /// A party can contain multiple actors, so its marker has no single actor ID; use <see cref="MemberActorIds"/>.
    /// </summary>
    public sealed class ExpeditionActorSnapshot
    {
        private readonly ReadOnlyCollection<ActorId> _memberActorIds;

        internal ExpeditionActorSnapshot(
            ExpeditionPartyInfo partyInfo,
            IList<ActorId> memberActorIds,
            MapMarkerSnapshot marker)
        {
            PartyInfo = partyInfo;

            List<ActorId> actorCopies = new List<ActorId>();
            if (memberActorIds != null)
            {
                for (int i = 0; i < memberActorIds.Count; i++)
                {
                    ActorId actorId = memberActorIds[i];
                    if (actorId != null)
                        actorCopies.Add(new ActorId(actorId.Kind, actorId.LocalId, actorId.Domain));
                }
            }

            _memberActorIds = actorCopies.AsReadOnly();
            Marker = marker == null ? null : marker.Clone();
        }

        /// <summary>Existing read-only party snapshot for the projected expedition.</summary>
        public ExpeditionPartyInfo PartyInfo { get; private set; }

        /// <summary>Copied actor IDs for each known family member travelling in this party.</summary>
        public ReadOnlyCollection<ActorId> MemberActorIds
        {
            get { return _memberActorIds; }
        }

        /// <summary>Moving marker projection for the party's current location.</summary>
        public MapMarkerSnapshot Marker { get; private set; }

        /// <summary>Copied current party route, or null when the party has no route.</summary>
        public ExpeditionRouteSnapshot Route
        {
            get { return Marker != null ? Marker.Route : null; }
        }

        internal ExpeditionActorSnapshot Clone()
        {
            return new ExpeditionActorSnapshot(PartyInfo, _memberActorIds, Marker);
        }
    }
}
