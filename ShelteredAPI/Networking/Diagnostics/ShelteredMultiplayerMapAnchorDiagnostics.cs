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
        public int CanonicalMapBunkerOwnerId = -1;
        public int BunkerCount;
        public Vector2 AssignedWorldPosition;
        public Vector2 ActiveWorldPosition;
        public Vector2 CanonicalMapWorldPosition;
        public Vector3 ActiveMapPixels;
        public int RequestedGridX;
        public int RequestedGridY;
        public int GridX;
        public int GridY;
        public int MapWidth;
        public int MapHeight;
        public float WorldMinX;
        public float WorldMaxX;
        public float WorldMinY;
        public float WorldMaxY;
        public int ValidRegionCount;
        public bool HasExplorationManager;
        public bool HasExpeditionMap;
        public bool HasMapSprite;
        public bool AnchorValid;
        public bool AnchorFallback;
        public bool AnchorOverrideEnabled;
        public bool ChosenRegionIsShelter;
        public bool ShelterCellValid;
        public string ValidationReason = string.Empty;
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
                report.BunkerCount = hasAssignments ? context.BunkerAssignments.Length : 0;
                if (!hasAssignments)
                    warnings.Add("No multiplayer bunker assignments are available.");

                ShelteredMultiplayerBunkerAssignmentRecord activeAssignment;
                bool localPlayerResolved = TryResolveActiveAssignment(context, out activeAssignment);
                if (activeAssignment != null)
                {
                    report.ActiveBunkerOwnerId = activeAssignment.BunkerOwnerId;
                    report.AssignedWorldPosition = activeAssignment.Position;
                }
                else
                {
                    report.ActiveBunkerOwnerId = ShelteredMultiplayerBunkerAssignments.ResolveBunkerOwnerId(
                        context.BunkerAssignments,
                        context.LocalPlayerId);
                    report.AssignedWorldPosition = ShelteredBunkers.GetBunkerWorldPosition(report.ActiveBunkerOwnerId);
                }

                if (hasAssignments && !localPlayerResolved)
                {
                    warnings.Add("Local player id " + context.LocalPlayerId
                        + " does not resolve to any multiplayer bunker owner id.");
                }

                ShelteredMultiplayerBunkerAssignmentRecord canonicalAssignment;
                if (TryResolveBunkerOwnerAssignment(context, 0, out canonicalAssignment))
                {
                    report.CanonicalMapBunkerOwnerId = canonicalAssignment.BunkerOwnerId;
                    report.CanonicalMapWorldPosition = canonicalAssignment.Position;
                }
                else
                {
                    Vector2 canonicalWorldPosition;
                    if (ShelteredMultiplayerBunkerAnchorRuntime.TryGetCanonicalMapBunkerWorldPosition(out canonicalWorldPosition))
                    {
                        report.CanonicalMapBunkerOwnerId = 0;
                        report.CanonicalMapWorldPosition = canonicalWorldPosition;
                    }
                    else if (hasAssignments)
                    {
                        warnings.Add("No canonical multiplayer map-generation bunker owner could be resolved.");
                    }
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
                if (serviceBunker != null && Vector2.Distance(serviceBunker.Position, report.AssignedWorldPosition) > PositionEpsilon)
                {
                    warnings.Add("Coordinator active bunker world position does not match ShelteredBunkers for owner "
                        + report.ActiveBunkerOwnerId + ".");
                }

                if (ShelteredBunkers.Service.ActivePlayerId != report.ActiveBunkerOwnerId)
                {
                    warnings.Add("ShelteredBunkers active owner " + ShelteredBunkers.Service.ActivePlayerId
                        + " does not match coordinator active bunker owner " + report.ActiveBunkerOwnerId + ".");
                }

                ShelteredMultiplayerMapAnchorValidationResult validation =
                    ShelteredMultiplayerMapAnchorValidator.ValidateActiveBunker("diagnostics");
                ApplyValidation(report, validation, warnings);

                if (!report.AnchorValid)
                {
                    report.ActiveWorldPosition = report.AssignedWorldPosition;
                    report.ActiveMapPixels = ShelteredBunkers.GetBunkerMapPixels(report.ActiveBunkerOwnerId);
                    ExpeditionMap.GridRef gridRef = ShelteredBunkers.GetBunkerGridRef(report.ActiveBunkerOwnerId);
                    if (gridRef != null)
                    {
                        report.RequestedGridX = gridRef.x;
                        report.RequestedGridY = gridRef.y;
                        report.GridX = gridRef.x;
                        report.GridY = gridRef.y;
                    }
                }

                bool hasWorldPosition = report.AssignedWorldPosition.sqrMagnitude > PositionEpsilon;
                if (!hasWorldPosition && hasAssignments)
                    warnings.Add("Active multiplayer bunker world position is zero.");

                if (report.AnchorValid && report.ActiveMapPixels.sqrMagnitude <= PositionEpsilon)
                    warnings.Add("Active bunker map pixels are zero while the active world position is nonzero.");

                if (hasWorldPosition && report.RequestedGridX == 0 && report.RequestedGridY == 0)
                    warnings.Add("Active bunker grid ref is 0,0 while the active world position is nonzero.");

                VerifyMapPixelAgreement(report, warnings);
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
                + ", canonicalMapOwner=" + report.CanonicalMapBunkerOwnerId
                + ", assignedWorld=(" + report.AssignedWorldPosition.x.ToString("F1") + ", " + report.AssignedWorldPosition.y.ToString("F1") + ")"
                + ", chosenWorld=(" + report.ActiveWorldPosition.x.ToString("F1") + ", " + report.ActiveWorldPosition.y.ToString("F1") + ")"
                + ", canonicalMapWorld=(" + report.CanonicalMapWorldPosition.x.ToString("F1") + ", " + report.CanonicalMapWorldPosition.y.ToString("F1") + ")"
                + ", mapPixels=(" + report.ActiveMapPixels.x.ToString("F1") + ", " + report.ActiveMapPixels.y.ToString("F1") + ")"
                + ", requestedGrid=(" + report.RequestedGridX + ", " + report.RequestedGridY + ")"
                + ", chosenGrid=(" + report.GridX + ", " + report.GridY + ")"
                + ", mapSize=(" + report.MapWidth + "x" + report.MapHeight + ")"
                + ", validRegions=" + report.ValidRegionCount
                + ", fallback=" + report.AnchorFallback
                + ", reason=" + report.ValidationReason + ".");

            string timelineDetail = "reason=" + (reason ?? string.Empty)
                + " owner=" + report.ActiveBunkerOwnerId
                + " canonicalMapOwner=" + report.CanonicalMapBunkerOwnerId
                + " requestedGrid=" + report.RequestedGridX + "," + report.RequestedGridY
                + " chosenGrid=" + report.GridX + "," + report.GridY
                + " valid=" + report.AnchorValid
                + " fallback=" + report.AnchorFallback
                + " warnings=" + report.Warnings.Length;
            if (report.AnchorValid && !report.AnchorFallback && report.Warnings.Length == 0)
                ShelteredMultiplayerTimeline.Instance.AppendMapAnchorValidated(timelineDetail);
            else
                ShelteredMultiplayerTimeline.Instance.AppendMapAnchorFallback(timelineDetail);

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

        private static bool TryResolveBunkerOwnerAssignment(
            ShelteredMultiplayerSessionContext context,
            int bunkerOwnerId,
            out ShelteredMultiplayerBunkerAssignmentRecord assignment)
        {
            assignment = null;
            if (context == null || context.BunkerAssignments == null)
                return false;

            for (int i = 0; i < context.BunkerAssignments.Length; i++)
            {
                ShelteredMultiplayerBunkerAssignmentRecord record = context.BunkerAssignments[i];
                if (record != null && record.BunkerOwnerId == bunkerOwnerId)
                {
                    assignment = record;
                    return true;
                }
            }

            return false;
        }

        private static void ApplyValidation(
            ShelteredMultiplayerMapAnchorReport report,
            ShelteredMultiplayerMapAnchorValidationResult validation,
            List<string> warnings)
        {
            if (report == null || validation == null)
                return;

            report.AnchorValid = validation.IsValid;
            report.AnchorFallback = validation.IsFallback;
            report.AnchorOverrideEnabled = validation.IsValid;
            report.ActiveWorldPosition = validation.IsValid
                ? validation.ChosenWorldPosition
                : validation.AssignedWorldPosition;
            report.ActiveMapPixels = validation.ChosenMapPixels;
            report.RequestedGridX = validation.RequestedGridX;
            report.RequestedGridY = validation.RequestedGridY;
            report.GridX = validation.ChosenGridX;
            report.GridY = validation.ChosenGridY;
            report.MapWidth = validation.MapWidth;
            report.MapHeight = validation.MapHeight;
            report.WorldMinX = validation.WorldMinX;
            report.WorldMaxX = validation.WorldMaxX;
            report.WorldMinY = validation.WorldMinY;
            report.WorldMaxY = validation.WorldMaxY;
            report.ValidRegionCount = validation.ValidRegionCount;
            report.ChosenRegionIsShelter = validation.ChosenRegionIsShelter;
            report.ValidationReason = validation.Reason ?? string.Empty;

            if (warnings == null)
                return;

            if (!validation.IsValid && validation.HasExpeditionMap && validation.HasMapRegionSource)
            {
                warnings.Add("No valid ExpeditionMap region exists for the active multiplayer bunker anchor. Multiplayer anchor override is disabled.");
            }
            else if (validation.IsFallback)
            {
                warnings.Add("Active multiplayer bunker anchor requested grid "
                    + validation.RequestedGridX + "," + validation.RequestedGridY
                    + " but fell back to nearest valid grid "
                    + validation.ChosenGridX + "," + validation.ChosenGridY + ".");
            }
        }

        private static void VerifyMapPixelAgreement(
            ShelteredMultiplayerMapAnchorReport report,
            List<string> warnings)
        {
            if (report == null || warnings == null || !report.HasExplorationManager || !report.HasMapSprite)
                return;
            if (!report.AnchorValid || report.ActiveWorldPosition.sqrMagnitude <= PositionEpsilon)
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

        private static void VerifyShelterCell(
            ShelteredMultiplayerMapAnchorReport report,
            List<string> warnings)
        {
            if (report == null || warnings == null || !report.HasExpeditionMap)
                return;
            if (!report.AnchorValid)
                return;
            if (report.CanonicalMapBunkerOwnerId >= 0
                && report.ActiveBunkerOwnerId != report.CanonicalMapBunkerOwnerId)
                return;

            MapRegion region = ExpeditionMap.Instance.GetRegionOnMap(new ExpeditionMap.GridRef(report.GridX, report.GridY));
            if (region == null)
            {
                warnings.Add("ExpeditionMap has no region at the active bunker grid ref.");
                return;
            }

            report.ShelterCellValid = region.topography == MapRegion.Topography.Shelter;
            if (!report.ShelterCellValid)
                warnings.Add("ExpeditionMap active bunker grid ref is not marked as a Shelter cell.");
        }
    }
}
