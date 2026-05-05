using System.Reflection;

namespace ModAPI.Inspector
{
    /// <summary>
    /// Public facade over cached decompiled source and IL-to-source maps.
    /// Use this from debug UI instead of reaching into the cache manager directly.
    /// </summary>
    public static class DebugSourceCache
    {
        public static string LastError { get { return SourceCacheManager.LastError; } }

        public static int MapSourceLineToILOffset(MethodBase method, int sourceLine)
        {
            return SourceCacheManager.MapSourceLineToILOffset(method, sourceLine);
        }

        public static int MapILToSourceLine(MethodBase method, int ilOffset)
        {
            return SourceCacheManager.MapILToSourceLine(method, ilOffset);
        }

        public static string GetCachePath(MethodBase method)
        {
            return SourceCacheManager.GetCachePath(method);
        }

        public static string GetMapPath(MethodBase method)
        {
            return SourceCacheManager.GetMapPath(method);
        }

        public static string GetSource(MethodBase method)
        {
            return SourceCacheManager.GetSource(method);
        }
    }
}
