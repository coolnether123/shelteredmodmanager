using System;
using Manager.Core.Models;

namespace Manager.Core.Services
{
    public static class NexusMigrationRegistry
    {
        public sealed class Entry
        {
            internal Entry(int modId, string packageId, string historicalAlias = null)
            {
                ModId = modId;
                PackageId = packageId;
                HistoricalAlias = historicalAlias;
            }

            public string GameDomain { get { return "sheltered"; } }
            public int ModId { get; private set; }
            public string PackageId { get; private set; }
            public string HistoricalAlias { get; private set; }

            internal bool MatchesPackageId(string packageId)
            {
                return string.Equals(PackageId, packageId, StringComparison.OrdinalIgnoreCase);
            }

            internal bool MatchesAlias(string packageId)
            {
                return HistoricalAlias != null &&
                    string.Equals(HistoricalAlias, packageId, StringComparison.OrdinalIgnoreCase);
            }

            internal bool MatchesPage(string gameDomain, int modId)
            {
                return ModId == modId && string.Equals(GameDomain, gameDomain, StringComparison.OrdinalIgnoreCase);
            }
        }

        // Package IDs and Nexus listings from the 2.0 migration audit.
        private static readonly Entry[] Entries =
        {
            new Entry(2, "Coolnether123.FourPersonExpeditions"),
            new Entry(3, "Coolnether123.TradingAmount"),
            new Entry(4, "coolnether123.Lifespan"),
            new Entry(5, "coolnether123.BunkerRandomLocation"),
            // Alias: unverified against the distributed ZIP.
            new Entry(6, "com.procreation.framework", "coolnether123.FamilyExpansion"),
            new Entry(7, "Coolnether123.ShelteredVanillaFixes"),
            new Entry(8, "coolnether123.betteraiqueue"),
            // Alias: unverified against the distributed ZIP.
            new Entry(9, "coolnether123.deepexpansion", "deepexpansion.researchdepth"),
            new Entry(10, "coolnether123.ShelteredSystemsExpansion")
            // TODO: Map Expanded Map Sizes (expandedmapsizes) and Sheltered
            // Display Fixes (Coolnether123.ShelteredDisplayFixes) once they have Nexus pages.
        };

        public static bool TryGet(string modId, out Entry entry)
        {
            string id = (modId ?? string.Empty).Trim();
            entry = Array.Find(Entries, candidate => candidate.MatchesPackageId(id) || candidate.MatchesAlias(id));
            return entry != null;
        }

        internal static Entry FindPage(NexusRemoteMod mod)
        {
            return Array.Find(Entries, candidate => candidate.MatchesPage(mod.GameDomain, mod.ModId));
        }
    }
}
