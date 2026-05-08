using System.Collections.Generic;
using System.Reflection;
using ModAPI.Networking;
using ShelteredAPI.Bunkers;
using ShelteredAPI.Networking.Diagnostics;
using UnityEngine;

namespace ShelteredAPI.Networking.Tests
{
    internal static class ShelteredMultiplayerMapAnchorDiagnosticsTests
    {
        public static void Register(List<TestCase> tests)
        {
            tests.Add(new TestCase("MapAnchorDiagnostics_InactiveSessionReportsInactive", InactiveSessionReportsInactive));
            tests.Add(new TestCase("MapAnchorDiagnostics_MissingAssignmentsWarns", MissingAssignmentsWarns));
            tests.Add(new TestCase("MapAnchorDiagnostics_UnresolvedLocalPlayerWarns", UnresolvedLocalPlayerWarns));
        }

        private static void InactiveSessionReportsInactive()
        {
            ResetState();

            ShelteredMultiplayerMapAnchorReport report = ShelteredMultiplayerMapAnchorDiagnostics.BuildReport();

            TestAssert.True(!report.MultiplayerActive, "Inactive coordinator context should report multiplayer inactive.");
            TestAssert.Equal(0, report.Warnings.Length, "Inactive multiplayer should not emit map-anchor warnings.");
        }

        private static void MissingAssignmentsWarns()
        {
            ResetState();
            SetContext(CreateContext(1, new ShelteredMultiplayerBunkerAssignmentRecord[0]));

            ShelteredMultiplayerMapAnchorReport report = ShelteredMultiplayerMapAnchorDiagnostics.BuildReport();

            TestAssert.True(report.MultiplayerActive, "Active host coordinator context should report multiplayer active.");
            TestAssert.True(ContainsWarning(report, "No multiplayer bunker assignments"),
                "Missing bunker assignments should be reported as a clear warning.");

            ResetState();
        }

        private static void UnresolvedLocalPlayerWarns()
        {
            ResetState();

            ShelteredMultiplayerBunkerAssignmentRecord[] assignments =
                new ShelteredMultiplayerBunkerAssignmentRecord[]
                {
                    new ShelteredMultiplayerBunkerAssignmentRecord(
                        NetworkDefaults.HostPeerId,
                        1,
                        0,
                        new Vector2(20f, 10f),
                        "Host",
                        true)
                };
            SetContext(CreateContext(2, assignments));

            ShelteredMultiplayerMapAnchorReport report = ShelteredMultiplayerMapAnchorDiagnostics.BuildReport();

            TestAssert.True(ContainsWarning(report, "does not resolve to any multiplayer bunker owner id"),
                "A local player id with no assignment should be reported.");
            TestAssert.Equal(1, report.BunkerCount,
                "Diagnostics should report the coordinator bunker assignment count.");

            ResetState();
        }

        private static bool ContainsWarning(ShelteredMultiplayerMapAnchorReport report, string value)
        {
            if (report == null || report.Warnings == null)
                return false;

            for (int i = 0; i < report.Warnings.Length; i++)
            {
                string warning = report.Warnings[i] ?? string.Empty;
                if (warning.IndexOf(value) >= 0)
                    return true;
            }

            return false;
        }

        private static void ResetState()
        {
            SetContext(new ShelteredMultiplayerSessionContext(
                ShelteredMultiplayerSessionMode.SinglePlayer,
                string.Empty,
                0,
                NetworkDefaults.UnassignedPeerId,
                string.Empty,
                20,
                0,
                0f,
                ShelteredMultiplayerGameTimeMode.Vanilla,
                ShelteredMultiplayerSetupPhase.Inactive,
                new ShelteredMultiplayerPeerInfo[0],
                new ShelteredMultiplayerBunkerAssignmentRecord[0],
                ShelteredMultiplayerSetupSettings.Empty,
                "test-reset"));
            ShelteredBunkers.Service.Clear();
        }

        private static ShelteredMultiplayerSessionContext CreateContext(
            int localPlayerId,
            ShelteredMultiplayerBunkerAssignmentRecord[] assignments)
        {
            return new ShelteredMultiplayerSessionContext(
                ShelteredMultiplayerSessionMode.Host,
                "map-anchor-tests",
                localPlayerId,
                NetworkDefaults.HostPeerId,
                "host",
                20,
                0,
                0f,
                ShelteredMultiplayerGameTimeMode.HostAuthoritative,
                ShelteredMultiplayerSetupPhase.Activated,
                new ShelteredMultiplayerPeerInfo[0],
                assignments,
                ShelteredMultiplayerSetupSettings.Empty,
                "test-active");
        }

        private static void SetContext(ShelteredMultiplayerSessionContext context)
        {
            FieldInfo field = typeof(ShelteredMultiplayerSessionCoordinator).GetField(
                "_context",
                BindingFlags.Instance | BindingFlags.NonPublic);
            field.SetValue(ShelteredMultiplayerSessionCoordinator.Instance, context);
        }
    }
}
