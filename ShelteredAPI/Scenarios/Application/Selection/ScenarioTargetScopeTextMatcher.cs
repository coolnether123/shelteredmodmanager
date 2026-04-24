using System;

namespace ShelteredAPI.Scenarios
{
    internal static class ScenarioTargetScopeTextMatcher
    {
        private static readonly string[] BackgroundTokens = new[]
        {
            "background", "backdrop", "sky", "sun", "moon", "weather", "cloud", "wall", "wallpaper", "wire", "pipe", "decal", "grime", "panel", "scenery", "farback"
        };

        private static readonly string[] SurfaceTokens = new[]
        {
            "surface", "terrain", "outside", "outdoor", "exterior", "hatch", "entrance", "eventanchor"
        };

        private static readonly string[] InteriorTokens = new[]
        {
            "room", "foundation", "ladder", "shelter", "bunker", "interior", "inside", "furniture", "storage", "object", "tile", "floor"
        };

        public static bool ContainsBackgroundToken(string value)
        {
            return ContainsAny(value, BackgroundTokens);
        }

        public static bool ContainsSurfaceToken(string value)
        {
            return ContainsAny(value, SurfaceTokens);
        }

        public static bool ContainsInteriorToken(string value)
        {
            return ContainsAny(value, InteriorTokens);
        }

        public static ScenarioTargetScope MatchBunkerScope(string value)
        {
            if (ContainsBackgroundToken(value))
                return ScenarioTargetScope.BunkerBackground;
            if (ContainsSurfaceToken(value))
                return ScenarioTargetScope.BunkerSurface;
            if (ContainsInteriorToken(value))
                return ScenarioTargetScope.BunkerInside;
            return ScenarioTargetScope.Unknown;
        }

        public static bool CandidateMatchesScope(string value, ScenarioTargetScope scope)
        {
            switch (scope)
            {
                case ScenarioTargetScope.BunkerBackground:
                    return ContainsBackgroundToken(value);
                case ScenarioTargetScope.BunkerSurface:
                    return ContainsSurfaceToken(value);
                case ScenarioTargetScope.BunkerInside:
                    return !ContainsBackgroundToken(value) && !ContainsSurfaceToken(value);
                default:
                    return true;
            }
        }

        private static bool ContainsAny(string value, string[] tokens)
        {
            if (string.IsNullOrEmpty(value) || tokens == null)
                return false;

            for (int i = 0; i < tokens.Length; i++)
            {
                if (!string.IsNullOrEmpty(tokens[i]) && value.IndexOf(tokens[i], StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }
    }
}
