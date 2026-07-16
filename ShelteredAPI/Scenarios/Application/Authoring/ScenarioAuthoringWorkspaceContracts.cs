namespace ShelteredAPI.Scenarios.Application.Authoring
{
    internal enum ScenarioAuthoringWorkspaceLayoutKind
    {
        NavigatorDocument = 0,
        DocumentOnly = 1
    }

    internal enum ScenarioAuthoringStatusTone
    {
        Neutral = 0,
        Informational = 1,
        Ready = 2,
        Warning = 3,
        Error = 4
    }

    internal sealed class ScenarioAuthoringWorkspaceViewModel
    {
        public string Id { get; set; }
        public ScenarioAuthoringWorkspaceLayoutKind LayoutKind { get; set; }
        public string ActiveSubtabId { get; set; }
        public ScenarioAuthoringWorkspaceSubtabViewModel[] Subtabs { get; set; }
        public ScenarioAuthoringNavigatorViewModel Navigator { get; set; }
        public ScenarioAuthoringWorkspaceDocumentViewModel Document { get; set; }
    }

    internal sealed class ScenarioAuthoringWorkspaceSubtabViewModel
    {
        public string Id { get; set; }
        public string Label { get; set; }
        public string IconText { get; set; }
        public bool Selected { get; set; }
        public ScenarioAuthoringStatusChipViewModel[] StatusChips { get; set; }
        public ScenarioAuthoringInspectorAction SelectAction { get; set; }
    }

    internal sealed class ScenarioAuthoringNavigatorViewModel
    {
        public string Id { get; set; }
        public string SearchControlId { get; set; }
        public string SearchText { get; set; }
        public string SearchPlaceholder { get; set; }
        public string SelectedEntityId { get; set; }
        public string EmptyMessage { get; set; }
        public ScenarioAuthoringNavigatorGroupViewModel[] Groups { get; set; }
    }

    internal sealed class ScenarioAuthoringNavigatorGroupViewModel
    {
        public string Id { get; set; }
        public string Label { get; set; }
        public string IconText { get; set; }
        public bool Expanded { get; set; }
        public ScenarioAuthoringStatusChipViewModel[] StatusChips { get; set; }
        public ScenarioAuthoringInspectorAction ToggleAction { get; set; }
        public ScenarioAuthoringInspectorAction CreateAction { get; set; }
        public ScenarioAuthoringNavigatorRowViewModel[] Rows { get; set; }
    }

    internal sealed class ScenarioAuthoringNavigatorRowViewModel
    {
        public string EntityId { get; set; }
        public string Title { get; set; }
        public string Subtitle { get; set; }
        public string IconText { get; set; }
        public bool Selected { get; set; }
        public bool Expanded { get; set; }
        public ScenarioAuthoringStatusChipViewModel[] StatusChips { get; set; }
        public ScenarioAuthoringInspectorAction SelectAction { get; set; }
        public ScenarioAuthoringInspectorAction ToggleAction { get; set; }
        public ScenarioAuthoringNavigatorRowViewModel[] Children { get; set; }
    }

    internal sealed class ScenarioAuthoringWorkspaceDocumentViewModel
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Subtitle { get; set; }
        public ScenarioAuthoringBreadcrumbViewModel[] Breadcrumbs { get; set; }
        public ScenarioAuthoringStatusChipViewModel[] StatusChips { get; set; }
        public ScenarioAuthoringInspectorAction[] HeaderActions { get; set; }
        public ScenarioAuthoringInspectorAction BackAction { get; set; }
        public ScenarioAuthoringInspectorSection[] Sections { get; set; }
    }

    internal sealed class ScenarioAuthoringBreadcrumbViewModel
    {
        public string Label { get; set; }
        public ScenarioAuthoringInspectorAction Action { get; set; }
    }

    internal sealed class ScenarioAuthoringStatusChipViewModel
    {
        public string Id { get; set; }
        public string Text { get; set; }
        public string IconText { get; set; }
        public ScenarioAuthoringStatusTone Tone { get; set; }
        public ScenarioAuthoringInspectorAction Action { get; set; }
    }

    internal sealed class ScenarioAuthoringCompactChoiceViewModel
    {
        public string Id { get; set; }
        public string Label { get; set; }
        public string CurrentLabel { get; set; }
        public int ColumnCount { get; set; }
        public ScenarioAuthoringCompactChoiceOptionViewModel[] Options { get; set; }
    }

    internal sealed class ScenarioAuthoringCompactChoiceOptionViewModel
    {
        public string Id { get; set; }
        public string Label { get; set; }
        public bool Selected { get; set; }
        public ScenarioAuthoringInspectorAction Action { get; set; }
    }
}
