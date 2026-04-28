using System;

namespace ModAPI.Saves
{
    internal static class IdGenerator
    {
        public static string NewId()
        {
            return Guid.NewGuid().ToString("N");
        }
    }
}

