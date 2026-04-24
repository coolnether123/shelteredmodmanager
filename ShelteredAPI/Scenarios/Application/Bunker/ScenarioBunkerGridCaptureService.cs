using System.Collections.Generic;
using System.Globalization;
using ModAPI.Scenarios;

namespace ShelteredAPI.Scenarios
{
    internal sealed class ScenarioBunkerGridCaptureService
    {
        private readonly ScenarioBunkerSupportResolver _supportResolver;

        public ScenarioBunkerGridCaptureService(ScenarioBunkerSupportResolver supportResolver)
        {
            _supportResolver = supportResolver;
        }

        public ScenarioBunkerGridCaptureSummary Capture(ScenarioEditorSession session)
        {
            ScenarioBunkerGridCaptureSummary summary = new ScenarioBunkerGridCaptureSummary();
            ScenarioDefinition definition = session != null ? session.WorkingDefinition : null;
            if (definition == null)
                return summary;

            if (definition.BunkerGrid == null)
                definition.BunkerGrid = new ScenarioBunkerGridDefinition();

            EnsureFoundationGrid(definition, summary);
            AttachPlacements(definition, summary);
            if ((summary.GeneratedFoundations + summary.AttachedPlacements) > 0 && session != null)
                ScenarioBunkerDraftService.MarkBunkerDirty(session);
            return summary;
        }

        private static void EnsureFoundationGrid(ScenarioDefinition definition, ScenarioBunkerGridCaptureSummary summary)
        {
            if (definition.BunkerGrid.Foundations.Count > 0)
                return;

            for (int i = 0; definition.BunkerEdits != null && definition.BunkerEdits.RoomChanges != null && i < definition.BunkerEdits.RoomChanges.Count; i++)
            {
                RoomEdit room = definition.BunkerEdits.RoomChanges[i];
                if (room == null)
                    continue;
                ScenarioFoundationDefinition foundation = new ScenarioFoundationDefinition();
                foundation.Id = "foundation." + room.GridX.ToString(CultureInfo.InvariantCulture) + "." + room.GridY.ToString(CultureInfo.InvariantCulture);
                foundation.GridX = room.GridX;
                foundation.GridY = room.GridY;
                foundation.Width = 1;
                foundation.Height = 1;
                foundation.ActiveAtStart = true;
                definition.BunkerGrid.Foundations.Add(foundation);
                summary.GeneratedFoundations++;
            }
        }

        private void AttachPlacements(ScenarioDefinition definition, ScenarioBunkerGridCaptureSummary summary)
        {
            for (int i = 0; definition.BunkerEdits != null && definition.BunkerEdits.ObjectPlacements != null && i < definition.BunkerEdits.ObjectPlacements.Count; i++)
            {
                ObjectPlacement placement = definition.BunkerEdits.ObjectPlacements[i];
                if (placement == null)
                    continue;
                if (string.IsNullOrEmpty(placement.RequiredFoundationId))
                {
                    placement.RequiredFoundationId = _supportResolver.ResolveNearestFoundationId(definition.BunkerGrid, placement.Position);
                    if (!string.IsNullOrEmpty(placement.RequiredFoundationId))
                        summary.AttachedPlacements++;
                }
                if (!_supportResolver.IsSupported(definition, placement))
                    summary.UnsupportedPlacements++;
            }
        }
    }

    internal sealed class ScenarioBunkerGridCaptureSummary
    {
        public ScenarioBunkerGridCaptureSummary()
        {
            Messages = new List<string>();
        }

        public int GeneratedFoundations { get; set; }
        public int AttachedPlacements { get; set; }
        public int UnsupportedPlacements { get; set; }
        public List<string> Messages { get; private set; }
    }
}
