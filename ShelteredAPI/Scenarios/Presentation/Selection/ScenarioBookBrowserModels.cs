using ShelteredAPI.Saves;

namespace ShelteredAPI.Scenarios
{
    internal enum ScenarioBookBrowserViewKind
    {
        Types,
        Scenarios,
        Saves
    }

    internal enum ScenarioBookType
    {
        Published,
        Surrounded,
        Stasis,
        Draft
    }

    internal enum ScenarioBookRowKind
    {
        Empty,
        Type,
        Scenario,
        StartScenario,
        OpenDraft,
        CreateDraft,
        LoadSave
    }

    internal sealed class ScenarioBookRowModel
    {
        public ScenarioBookRowKind Kind;
        public string Title;
        public string Detail;
        public string Badge;
        public bool IsLocked;
        public bool CanDelete;
        public ScenarioBookType Type;
        public ScenarioCatalogEntry Scenario;
        public SaveEntry Save;
    }
}
