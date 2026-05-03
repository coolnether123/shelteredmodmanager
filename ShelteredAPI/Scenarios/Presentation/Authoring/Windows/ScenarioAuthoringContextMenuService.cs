using System;

namespace ShelteredAPI.Scenarios
{
    internal sealed class ScenarioAuthoringContextMenuService
    {
        private readonly object _sync = new object();
        private ScenarioAuthoringContextMenuModel _model = new ScenarioAuthoringContextMenuModel();

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

        public void Open(string title, string detail, float anchorX, float anchorY, ScenarioAuthoringInspectorAction[] actions)
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
                    Actions = actions ?? new ScenarioAuthoringInspectorAction[0]
                };
            }
        }

        public void Close()
        {
            lock (_sync)
            {
                _model = new ScenarioAuthoringContextMenuModel();
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
