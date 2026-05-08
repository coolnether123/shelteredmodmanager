using System;
using System.Collections.Generic;
using ModAPI.Core;
using ShelteredAPI.Bunkers;
using UnityEngine;

namespace ShelteredAPI.Networking.Diagnostics
{
    internal sealed class ShelteredMultiplayerMapAnchorReport
    {
        public bool MultiplayerActive;
        public string SessionId = string.Empty;
        public int LocalPlayerId;
        public int ActiveBunkerOwnerId;
        public Vector2 ActiveWorldPosition;
        public Vector3 ActiveMapPixels;
        public int GridX;
        public int GridY;
        public bool HasExplorationManager;
        public bool HasExpeditionMap;
        public bool HasMapSprite;
        public string[] Warnings = new string[0];
    }

    internal static class ShelteredMultiplayerMapAnchorDiagnostics
    {
        private const string LogSource = "ShelteredAPI.Multiplayer.MapAnchorDiagnostics";
        private const float PositionEpsilon = 0.001f;

        public static ShelteredMultiplayerMapAnchorReport BuildReport()
        {
            ShelteredMultiplayerMapAnchorReport report = new ShelteredMultiplayerMapAnchorReport();
            List<string> warnings = new List<string>();

            try
            {
                ShelteredMultiplayerSessionContext context = ShelteredMultiplayerSessionCoordinator.Instance.Context;
                if (context == null || !context.IsMultiplayerActive)
                {
                    report.MultiplayerActive = false;
                    report.Warnings = warnings.ToArray();
                    return report;
                }

                report.MultiplayerActive = true;
                report.SessionId = context.SessionId ?? string.Empty;
                report.LocalPlayerId = context.LocalPlayerId;

                bool hasAssignments = context.BunkerAssignments != null && context.BunkerAssignments.Length > 0;
                if (!hasAssignments)
                    warnings.Add("No multiplayer bunker assignments are available.");

                ShelteredMultiplayerBunkerAssignmentRecord activeAssignment;
                bool localPlayerResolved = TryResolveActiveAssignment(context, out activeAssignment);
                if (activeAssignment != null)
                {
                    report.ActiveBunkerOwnerId = activeAssignment.BunkerOwnerId;
                    report.ActiveWorldPosition = activeAssignment.Position;
                }
                else
                {
                    report.ActiveBunkerOwnerId = ShelteredMultiplayerBunkerAssignments.ResolveBunkerOwnerId(
                        context.BunkerAssignments,
                        context.LocalPlayerId);
                    report.ActiveWorldPosition = ShelteredBunkers.GetBunkerWorldPosition(report.ActiveBunkerOwnerId);
                }

                if (hasAssignments && !localPlayerResolved)
                {
                    warnings.Add("Local player id " + context.LocalPlayerId
                        + " does not resolve to any multiplayer bunker owner id.");
                }

                report.HasExplorationManager = ExplorationManager.Instance != null;
                report.HasExpeditionMap = ExpeditionMap.Instance != null;
                report.HasMapSprite = report.HasExplorationManager && ExplorationManager.Instance.mapSourceSprite != null;

                if (!report.HasExplorationManager)
                    warnings.Add("ExplorationManager is not available; map pixel and grid anchor checks may be incomplete.");
                if (!report.HasExpeditionMap)
                    warnings.Add("ExpeditionMap is not available; grid and shelter-cell anchor checks may be incomplete.");
                if (report.HasExplorationManager && !report.HasMapSprite)
                    warnings.Add("ExplorationManager map source sprite is not available; active bunker map pixels cannot be verified.");

                BunkerDefinition serviceBunker = ShelteredBunkers.GetBunker(report.ActiveBunkerOwnerId);
                if (serviceBunker != null && Vector2.Distance(serviceBunker.Position, report.ActiveWorldPosition) > PositionEpsilon)
                {
                    warnings.Add("Coordinator active bunker world position does not match ShelteredBunkers for owner "
                        + report.ActiveBunkerOwnerId + ".");
                }

                if (ShelteredBunkers.Service.ActivePlayerId != report.ActiveBunkerOwnerId)
                {
                    warnings.Add("ShelteredBunkers active owner " + ShelteredBunkers.Service.ActivePlayerId
                        + " does not match coordinator active bunker owner " + report.ActiveBunkerOwnerId + ".");
                }

                report.ActiveMapPixels = ShelteredBunkers.GetBunkerMapPixels(report.ActiveBunkerOwnerId);
                ExpeditionMap.GridRef gridRef = ShelteredBunkers.GetBunkerGridRef(report.ActiveBunkerOwnerId);
                if (gridRef != null)
                {
                    report.GridX = gridRef.x;
                    report.GridY = gridRef.y;
                }

                bool hasWorldPosition = report.ActiveWorldPosition.sqrMagnitude > PositionEpsilon;
                if (!hasWorldPosition && hasAssignments)
                    warnings.Add("Active multiplayer bunker world position is zero.");

                if (hasWorldPosition && report.ActiveMapPixels.sqrMagnitude <= PositionEpsilon)
                    warnings.Add("Active bunker map pixels are zero while the active world position is nonzero.");

                if (hasWorldPosition && report.GridX == 0 && report.GridY == 0)
                    warnings.Add("Active bunker grid ref is 0,0 while the active world position is nonzero.");

                VerifyMapPixelAgreement(report, warnings);
                VerifyGridAgreement(report, warnings);
                VerifyShelterCell(report, warnings);
            }
            catch (Exception ex)
            {
                warnings.Add("Map anchor diagnostics failed: " + ex.Message);
            }

            report.Warnings = warnings.ToArray();
            return report;
        }

        public static void LogReport(string reason)
        {
            ShelteredMultiplayerMapAnchorReport report = BuildReport();
            if (report == null || !report.MultiplayerActive)
                return;

            MMLog.WriteWithSource(MMLog.LogLevel.Info, MMLog.LogCategory.Network, LogSource,
                "Map anchor report for " + (reason ?? string.Empty)
                + ": session='" + report.SessionId + "', localPlayer=" + report.LocalPlayerId
                + ", activeOwner=" + report.ActiveBunkerOwnerId
                + ", world=(" + report.ActiveWorldPosition.x.ToString("F1") + ", " + report.ActiveWorldPosition.y.ToString("F1") + ")"
                + ", mapPixels=(" + report.ActiveMapPixels.x.ToString("F1") + ", " + report.ActiveMapPixels.y.ToString("F1") + ")"
                + ", grid=(" + report.GridX + ", " + report.GridY + ").");

            for (int i = 0; i < report.Warnings.Length; i++)
            {
                string warning = report.Warnings[i] ?? string.Empty;
                if (warning.Length == 0)
                    continue;

                MMLog.WarnOnce("ShelteredMultiplayerMapAnchorDiagnostics." + warning,
                    "Map anchor diagnostics warning: " + warning);
            }
        }

        private static bool TryResolveActiveAssignment(
            ShelteredMultiplayerSessionContext context,
            out ShelteredMultiplayerBunkerAssignmentRecord activeAssignment)
        {
            activeAssignment = null;
            if (context == null || context.BunkerAssignments == null)
                return false;

            for (int i = 0; i < context.BunkerAssignments.Length; i++)
            {
                ShelteredMultiplayerBunkerAssignmentRecord record = context.BunkerAssignments[i];
                if (record != null && record.PlayerId == context.LocalPlayerId)
                {
                    activeAssignment = record;
                    return true;
                }
            }

            return false;
        }

        private static void VerifyMapPixelAgreement(
            ShelteredMultiplayerMapAnchorReport report,
            List<string> warnings)
        {
            if (report == null || warnings == null || !report.HasExplorationManager || !report.HasMapSprite)
                return;
            if (report.ActiveWorldPosition.sqrMagnitude <= PositionEpsilon)
                return;

            Vector3 expected = new Vector3(
                ExplorationManager.Instance.WorldToMapPixelsX(report.ActiveWorldPosition.x),
                ExplorationManager.Instance.WorldToMapPixelsY(report.ActiveWorldPosition.y),
                0f);

            if (Vector3.Distance(expected, report.ActiveMapPixels) > PositionEpsilon)
            {
                warnings.Add("Active bunker map pixels do not match ExplorationManager conversion for the active world position.");
            }
        }

        private static void VerifyGridAgreement(
            ShelteredMultiplayerMapAnchorReport report,
            List<string> warnings)
        {
            if (report == null || warnings == null || !report.HasExpeditionMap || !report.HasExplorationManager)
                return;
            if (report.ActiveWorldPosition.sqrMagnitude <= PositionEpsilon)
                return;

            ExpeditionMap.GridRef expected = ExpeditionMap.Instance.WorldPosToGridRef(report.ActiveWorldPosition);
            if (expected == null)
                return;

            if (expected.x != report.GridX || expected.y != report.GridY)
            {
                warnings.Add("Active bunker grid ref does not match ExpeditionMap.WorldPosToGridRef for the active world position.");
            }
        }

        private static void VerifyShelterCell(
            ShelteredMultiplayerMapAnchorReport report,
            List<string> warnings)
        {
            if (report == null || warnings == null || !report.HasExpeditionMap)
                return;
            if (report.ActiveWorldPosition.sqrMagnitude <= PositionEpsilon)
                return;

            MapRegion region = ExpeditionMap.Instance.GetRegionOnMap(new ExpeditionMap.GridRef(report.GridX, report.GridY));
            if (region == null)
            {
                warnings.Add("ExpeditionMap has no region at the active bunker grid ref.");
                return;
            }

            if (region.topography != MapRegion.Topography.Shelter)
                warnings.Add("ExpeditionMap active bunker grid ref is not marked as a Shelter cell.");
        }
    }
}
