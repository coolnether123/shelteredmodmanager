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
            tests.Add(new TestCase("ConnectionPanel_Wizard_OfflineJoinUsesManualEndpointPrimary", WizardOfflineJoinUsesManualEndpointPrimary));
            tests.Add(new TestCase("ConnectionPanel_Wizard_HostingPrimaryBeginsSetup", WizardHostingPrimaryBeginsSetup));
            tests.Add(new TestCase("ConnectionPanel_Wizard_SetupReleaseExplainsDisabledReason", WizardSetupReleaseExplainsDisabledReason));
            tests.Add(new TestCase("ConnectionPanel_EndpointCandidates_ClassifyCommonAdapters", EndpointCandidatesClassifyCommonAdapters));
            tests.Add(new TestCase("ConnectionPanel_TimelineStatus_ReportsEmptyAvailableAccessor", TimelineStatusReportsEmptyAvailableAccessor));
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

        private static void WizardOfflineJoinUsesManualEndpointPrimary()
        {
            MultiplayerConnectionPanelState state = new MultiplayerConnectionPanelState();
            state.SelectedRole = MultiplayerConnectionWizardRole.Join;

            MultiplayerConnectionPanelViewModel model = new MultiplayerConnectionPanelViewModel();
            model.HasActiveSession = false;
            model.JoinAction = MultiplayerConnectionActionState.Available("Join");
            model.Wizard = MultiplayerConnectionWizardTextBuilder.Build(model, state);
            MultiplayerConnectionWizardActionBuilder.Populate(model, state);

            TestAssert.Equal(
                MultiplayerConnectionWizardSectionKind.Offline,
                model.Wizard.CurrentSection,
                "Offline model should stay in the offline wizard section.");
            TestAssert.Equal(
                MultiplayerConnectionWizardActionKind.Join,
                model.Wizard.PrimaryAction.Kind,
                "Join-selected offline flow should make manual join the primary action.");
            TestAssert.True(model.Wizard.PrimaryAction.Enabled, "Join primary action should be enabled when validation allows it.");
        }

        private static void WizardHostingPrimaryBeginsSetup()
        {
            MultiplayerConnectionPanelViewModel model = new MultiplayerConnectionPanelViewModel();
            model.HasActiveSession = true;
            model.Mode = NetworkSessionMode.Host;
            model.SessionState = NetworkSessionState.Listening;
            model.SetupReadiness = MultiplayerSetupReadinessTextBuilder.Build(
                "idle",
                string.Empty,
                NetworkSessionMode.Host,
                true,
                false,
                1);
            model.BeginSetupAction = MultiplayerConnectionActionState.Available("Begin Game Setup");

            MultiplayerConnectionPanelState state = new MultiplayerConnectionPanelState();
            model.Wizard = MultiplayerConnectionWizardTextBuilder.Build(model, state);
            MultiplayerConnectionWizardActionBuilder.Populate(model, state);

            TestAssert.Equal(
                MultiplayerConnectionWizardSectionKind.Hosting,
                model.Wizard.CurrentSection,
                "Active host without setup should show the hosting section.");
            TestAssert.Equal(
                MultiplayerConnectionWizardActionKind.BeginSetup,
                model.Wizard.PrimaryAction.Kind,
                "Hosting primary action should begin setup.");
            TestAssert.True(model.Wizard.PrimaryAction.Enabled, "Host setup action should be enabled when the presenter allows it.");
        }

        private static void WizardSetupReleaseExplainsDisabledReason()
        {
            MultiplayerConnectionPanelViewModel model = new MultiplayerConnectionPanelViewModel();
            model.HasActiveSession = true;
            model.Mode = NetworkSessionMode.Host;
            model.SessionState = NetworkSessionState.Listening;
            model.SetupReadiness = MultiplayerSetupReadinessTextBuilder.Build(
                "waiting for host startup to finish",
                string.Empty,
                NetworkSessionMode.Host,
                true,
                false,
                1);

            MultiplayerConnectionPanelState state = new MultiplayerConnectionPanelState();
            model.Wizard = MultiplayerConnectionWizardTextBuilder.Build(model, state);
            MultiplayerConnectionWizardActionBuilder.Populate(model, state);

            TestAssert.Equal(
                MultiplayerConnectionWizardSectionKind.Setup,
                model.Wizard.CurrentSection,
                "Active setup should show the setup section.");
            TestAssert.Equal(
                MultiplayerConnectionWizardActionKind.ReleaseSetup,
                model.Wizard.PrimaryAction.Kind,
                "Setup primary action should be the host release gate.");
            TestAssert.False(model.Wizard.PrimaryAction.Enabled, "Release should stay disabled while host load is not done.");
            TestAssert.True(
                model.Wizard.PrimaryAction.DisabledReason.IndexOf("host save", System.StringComparison.OrdinalIgnoreCase) >= 0,
                "Disabled release should explain that the host save is still loading.");
        }

        private static void EndpointCandidatesClassifyCommonAdapters()
        {
            TestAssert.Equal(
                "Loopback",
                MultiplayerEndpointCandidateBuilder.ClassifyForText("Loopback", string.Empty, string.Empty, true, false, false),
                "Loopback addresses should be clearly labeled.");
            TestAssert.Equal(
                "VPN",
                MultiplayerEndpointCandidateBuilder.ClassifyForText("Radmin VPN", "Radmin adapter", "Ethernet", false, true, false),
                "Known VPN adapter names should be labeled as VPN.");
            TestAssert.Equal(
                "LAN",
                MultiplayerEndpointCandidateBuilder.ClassifyForText("Ethernet", "Intel adapter", "Ethernet", false, true, false),
                "Private non-VPN adapters should be labeled as LAN.");
        }

        private static void TimelineStatusReportsEmptyAvailableAccessor()
        {
            MultiplayerTimelineStatusText status = MultiplayerConnectionWizardTextBuilder.BuildTimelineStatus(new string[0]);

            TestAssert.Equal("No timeline entries", status.StatusText, "Empty timeline should report that the accessor is available.");
            TestAssert.True(
                status.DetailText.IndexOf("available", System.StringComparison.OrdinalIgnoreCase) >= 0,
                "Timeline status should explain that diagnostics are available.");
        }
    }
}
