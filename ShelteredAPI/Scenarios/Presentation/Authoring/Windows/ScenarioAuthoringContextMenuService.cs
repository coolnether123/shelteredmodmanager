using System;

using ShelteredAPI.Hooks;
using ShelteredAPI.Scenarios.Application.Authoring;
namespace ShelteredAPI.Scenarios.Presentation.Authoring.Windows{
    internal sealed class ScenarioAuthoringContextMenuService
    {
        private readonly object _sync = new object();
        private ScenarioAuthoringContextMenuModel _model = new ScenarioAuthoringContextMenuModel();
        private int _revision;

        public int Revision
        {
            get
            {
                lock (_sync)
                {
                    return _revision;
                }
            }
        }

        public ScenarioAuthoringContextMenuModel Current
        {
            get
            {
                lock (_sync)
                {
                    return _model != null ? _model.Copy() : new ScenarioAuthoringContextMenuModel();
                }
            }
        }

        public void Open(string title, string detail, float anchorX, float anchorY, bool centerOnScreen, ScenarioAuthoringInspectorAction[] actions)
        {
            lock (_sync)
            {
                _model = new ScenarioAuthoringContextMenuModel
                {
                    Visible = actions != null && actions.Length > 0,
                    Title = title,
                    Detail = detail,
                    AnchorX = anchorX,
                    AnchorY = anchorY,
                    CenterOnScreen = centerOnScreen,
                    Actions = actions ?? new ScenarioAuthoringInspectorAction[0]
                };
                _revision++;
            }
        }

        public void Close()
        {
            lock (_sync)
            {
                _model = new ScenarioAuthoringContextMenuModel();
                _revision++;
            }
        }

        public void SyncTarget(ScenarioAuthoringTarget target)
        {
            if (target != null)
                return;

            Close();
        }
    }
}
