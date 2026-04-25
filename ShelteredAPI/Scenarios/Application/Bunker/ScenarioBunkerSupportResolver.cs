using System;
using ModAPI.Scenarios;

namespace ShelteredAPI.Scenarios
{
    internal sealed class ScenarioBunkerSupportResolver
    {
        public string ResolveNearestFoundationId(ScenarioBunkerGridDefinition grid, ScenarioVector3 position)
        {
            ScenarioFoundationDefinition best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; grid != null && grid.Foundations != null && i < grid.Foundations.Count; i++)
            {
                ScenarioFoundationDefinition foundation = grid.Foundations[i];
                if (foundation == null || string.IsNullOrEmpty(foundation.Id))
                    continue;
                float dx = (position != null ? position.X : 0f) - foundation.GridX;
                float dy = (position != null ? position.Y : 0f) - foundation.GridY;
                float distance = (dx * dx) + (dy * dy);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = foundation;
                }
            }
            return best != null ? best.Id : null;
        }

        public bool IsSupported(ScenarioDefinition definition, ObjectPlacement placement)
        {
            if (placement == null)
                return false;
            if (string.IsNullOrEmpty(placement.RequiredFoundationId) && string.IsNullOrEmpty(placement.RequiredBunkerExpansionId))
                return true;
            return HasFoundation(definition, placement.RequiredFoundationId) || HasExpansion(definition, placement.RequiredBunkerExpansionId);
        }

        private static bool HasFoundation(ScenarioDefinition definition, string id)
        {
            if (string.IsNullOrEmpty(id))
                return false;
            for (int i = 0; definition != null && definition.BunkerGrid != null && definition.BunkerGrid.Foundations != null && i < definition.BunkerGrid.Foundations.Count; i++)
            {
                ScenarioFoundationDefinition foundation = definition.BunkerGrid.Foundations[i];
                if (foundation != null && string.Equals(foundation.Id, id, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static bool HasExpansion(ScenarioDefinition definition, string id)
        {
            if (string.IsNullOrEmpty(id))
                return false;
            for (int i = 0; definition != null && definition.BunkerGrid != null && definition.BunkerGrid.Expansions != null && i < definition.BunkerGrid.Expansions.Count; i++)
            {
                ScenarioBunkerExpansionDefinition expansion = definition.BunkerGrid.Expansions[i];
                if (expansion != null && string.Equals(expansion.Id, id, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
    }
}
