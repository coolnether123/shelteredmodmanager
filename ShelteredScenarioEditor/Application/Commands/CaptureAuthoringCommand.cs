using ShelteredScenarioEditor.Application.Authoring;

namespace ShelteredScenarioEditor.Application.Commands
{
    internal static class CaptureAuthoringAutomationIds
    {
        public const string PreviewFamily = "capture.family.current";
        public const string ConfirmFamily = "capture.family.confirm";
        public const string CaptureShelterObjects = "capture.shelter.objects";
        public const string CaptureSelectedObject = "capture.shelter.selected_object";
        public const string RemoveSelectedPlacement = "capture.shelter.remove_selected_object";
    }

    internal enum CaptureAuthoringCommandKind
    {
        PreviewFamily,
        ConfirmFamily,
        CaptureShelterObjects,
        CaptureSelectedObject,
        RemoveSelectedPlacement
    }

    internal sealed class CaptureAuthoringCommand : ScenarioAuthoringCommand
    {
        private static readonly CaptureAuthoringCommand PreviewFamilyCommand =
            new CaptureAuthoringCommand(CaptureAuthoringCommandKind.PreviewFamily, CaptureAuthoringAutomationIds.PreviewFamily);
        private static readonly CaptureAuthoringCommand ConfirmFamilyCommand =
            new CaptureAuthoringCommand(CaptureAuthoringCommandKind.ConfirmFamily, CaptureAuthoringAutomationIds.ConfirmFamily);
        private static readonly CaptureAuthoringCommand ShelterObjectsCommand =
            new CaptureAuthoringCommand(CaptureAuthoringCommandKind.CaptureShelterObjects, CaptureAuthoringAutomationIds.CaptureShelterObjects);
        private static readonly CaptureAuthoringCommand SelectedObjectCommand =
            new CaptureAuthoringCommand(CaptureAuthoringCommandKind.CaptureSelectedObject, CaptureAuthoringAutomationIds.CaptureSelectedObject);
        private static readonly CaptureAuthoringCommand RemovePlacementCommand =
            new CaptureAuthoringCommand(CaptureAuthoringCommandKind.RemoveSelectedPlacement, CaptureAuthoringAutomationIds.RemoveSelectedPlacement);

        private CaptureAuthoringCommand(CaptureAuthoringCommandKind kind, string automationId)
            : base(automationId, kind == CaptureAuthoringCommandKind.PreviewFamily
                ? ScenarioAuthoringCommandPolicy.World
                : ScenarioAuthoringCommandPolicy.WorldSafetySnapshot)
        {
            Kind = kind;
        }

        public CaptureAuthoringCommandKind Kind { get; private set; }
        public static CaptureAuthoringCommand PreviewFamily { get { return PreviewFamilyCommand; } }
        public static CaptureAuthoringCommand ConfirmFamily { get { return ConfirmFamilyCommand; } }
        public static CaptureAuthoringCommand CaptureShelterObjects { get { return ShelterObjectsCommand; } }
        public static CaptureAuthoringCommand CaptureSelectedObject { get { return SelectedObjectCommand; } }
        public static CaptureAuthoringCommand RemoveSelectedPlacement { get { return RemovePlacementCommand; } }
    }
}
