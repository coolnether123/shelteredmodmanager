using System.Collections.Generic;
using System.Reflection;
using ModAPI.Core;
using ModAPI.Networking;
using ShelteredAPI.Networking.Diagnostics;
using ShelteredAPI.Networking.Travel;
using ShelteredAPI.Networking.World;

namespace ShelteredAPI.Networking.Tests
{
    internal static class DesyncDigestTests
    {
        public static void Register(List<TestCase> tests)
        {
            tests.Add(new TestCase("DesyncDiagnostics_ReportCanBeDumped", ReportCanBeDumped));
        }

        private static void ReportCanBeDumped()
        {
            FieldInfo contextField = typeof(ShelteredMultiplayerSessionCoordinator).GetField("_context", BindingFlags.Instance | BindingFlags.NonPublic);
            ShelteredMultiplayerSessionContext previous = (ShelteredMultiplayerSessionContext)contextField.GetValue(ShelteredMultiplayerSessionCoordinator.Instance);

            try
            {
                contextField.SetValue(
                    ShelteredMultiplayerSessionCoordinator.Instance,
                    new ShelteredMultiplayerSessionContext(
                        ShelteredMultiplayerSessionMode.Host,
                        "desync-session",
                        1,
                        NetworkDefaults.HostPeerId,
                        "host",
                        20,
                        55,
                        0.05f,
                        ShelteredMultiplayerGameTimeMode.HostAuthoritative,
                        ShelteredMultiplayerSetupPhase.Released,
                        new ShelteredMultiplayerPeerInfo[0],
                        new ShelteredMultiplayerBunkerAssignmentRecord[0],
                        ShelteredMultiplayerSetupSettings.Empty,
                        "test"));

                ModRandom.Initialize(789);
                ShelteredMultiplayerDesyncDiagnostics diagnostics = new ShelteredMultiplayerDesyncDiagnostics(
                    ShelteredMultiplayerSessionCoordinator.Instance,
                    new ShelteredWorldEventJournal(),
                    new ShelteredMapEntityRegistry(delegate { return 55; }),
                    new ShelteredTravelStateRegistry(),
                    delegate { return "compat"; });

                string text = diagnostics.DumpReport("test");

                TestAssert.True(text.IndexOf("desync-session") >= 0, "Report should include session id.");
                TestAssert.True(text.IndexOf("rngDigest=") >= 0, "Report should include RNG digest.");
            }
            finally
            {
                contextField.SetValue(ShelteredMultiplayerSessionCoordinator.Instance, previous);
            }
        }
    }
}
