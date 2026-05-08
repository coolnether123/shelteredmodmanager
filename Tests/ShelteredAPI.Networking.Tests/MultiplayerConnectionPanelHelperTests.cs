using System.Collections.Generic;
using ModAPI.Networking;
using ModAPI.Networking.Sessions;
using ShelteredAPI.Networking;

namespace ShelteredAPI.Networking.Tests
{
    internal static class MultiplayerConnectionPanelHelperTests
    {
        public static void Register(List<TestCase> tests)
        {
            tests.Add(new TestCase("ConnectionPanel_PortValidation_BlankUsesDefaultPort", PortValidationBlankUsesDefaultPort));
            tests.Add(new TestCase("ConnectionPanel_PortValidation_RejectsInvalidPorts", PortValidationRejectsInvalidPorts));
            tests.Add(new TestCase("ConnectionPanel_EndpointValidation_NormalizesEndpoint", EndpointValidationNormalizesEndpoint));
            tests.Add(new TestCase("ConnectionPanel_EndpointValidation_ReportsFriendlyErrors", EndpointValidationReportsFriendlyErrors));
            tests.Add(new TestCase("ConnectionPanel_StatusText_IsReadableForHost", StatusTextIsReadableForHost));
            tests.Add(new TestCase("ConnectionPanel_SetupReadiness_MapsCommonStates", SetupReadinessMapsCommonStates));
        }

        private static void PortValidationBlankUsesDefaultPort()
        {
            MultiplayerPortValidationResult result = MultiplayerConnectionInputValidator.ValidatePortText(" ");

            TestAssert.True(result.IsValid, "Blank port text should use the default networking port.");
            TestAssert.Equal(NetworkDefaults.DefaultPort, result.Port, "Blank port text should normalize to the default port.");
        }

        private static void PortValidationRejectsInvalidPorts()
        {
            MultiplayerPortValidationResult text = MultiplayerConnectionInputValidator.ValidatePortText("abc");
            MultiplayerPortValidationResult range = MultiplayerConnectionInputValidator.ValidatePortText("70000");

            TestAssert.False(text.IsValid, "Non-numeric port text should be rejected.");
            TestAssert.False(range.IsValid, "Out-of-range port text should be rejected.");
        }

        private static void EndpointValidationNormalizesEndpoint()
        {
            MultiplayerEndpointValidationResult result =
                MultiplayerConnectionInputValidator.ValidateEndpointText(" 192.168.1.20 ", 8888);

            TestAssert.True(result.IsValid, "Endpoint without an explicit port should use the provided default port.");
            TestAssert.Equal("192.168.1.20", result.Host, "Endpoint host should be trimmed.");
            TestAssert.Equal(8888, result.Port, "Endpoint port should use the default when omitted.");
            TestAssert.Equal("192.168.1.20:8888", result.EndpointText, "Endpoint text should be normalized for service calls.");
        }

        private static void EndpointValidationReportsFriendlyErrors()
        {
            MultiplayerEndpointValidationResult result =
                MultiplayerConnectionInputValidator.ValidateEndpointText("[::1]:7777", NetworkDefaults.DefaultPort);

            TestAssert.False(result.IsValid, "IPv6 endpoints should be rejected by the IPv4 UDP transport.");
            TestAssert.True(result.ErrorText.IndexOf(MultiplayerConnectionInputValidator.EndpointExample) >= 0,
                "Endpoint error should include an example.");
        }

        private static void StatusTextIsReadableForHost()
        {
            MultiplayerConnectionStatusText result = MultiplayerConnectionStatusTextBuilder.Build(
                NetworkSessionMode.Host,
                NetworkSessionState.Listening,
                true,
                1,
                1);

            TestAssert.Equal("Host", result.RoleText, "Host role should be readable.");
            TestAssert.Equal("Listening", result.StateText, "Listening state should be readable.");
            TestAssert.True(result.SummaryText.IndexOf("Hosting") >= 0, "Summary should describe the host state.");
        }

        private static void SetupReadinessMapsCommonStates()
        {
            TestAssert.Equal(
                MultiplayerSetupReadinessKind.NotStarted,
                MultiplayerSetupReadinessTextBuilder.Build("inactive", string.Empty, NetworkSessionMode.Host, true, false, 0).Kind,
                "Inactive setup should read as not started.");

            TestAssert.Equal(
                MultiplayerSetupReadinessKind.Loading,
                MultiplayerSetupReadinessTextBuilder.Build("setup received; starting client new-save flow", string.Empty, NetworkSessionMode.Client, true, false, 0).Kind,
                "Client startup should read as loading.");

            TestAssert.Equal(
                MultiplayerSetupReadinessKind.Waiting,
                MultiplayerSetupReadinessTextBuilder.Build("waiting for 1 peer(s)", string.Empty, NetworkSessionMode.Host, true, false, 1).Kind,
                "Peer wait should read as waiting.");

            TestAssert.Equal(
                MultiplayerSetupReadinessKind.EveryoneLoaded,
                MultiplayerSetupReadinessTextBuilder.Build("all players loaded; waiting for host release", string.Empty, NetworkSessionMode.Host, true, true, 1).Kind,
                "Loaded setup should read as everyone loaded.");

            TestAssert.Equal(
                MultiplayerSetupReadinessKind.Released,
                MultiplayerSetupReadinessTextBuilder.Build("released", string.Empty, NetworkSessionMode.Host, true, false, 1).Kind,
                "Released setup should read as released.");
        }
    }
}
