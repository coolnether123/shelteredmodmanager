using System;

using ShelteredScenarioEditor.Application.Authoring;
namespace ShelteredScenarioEditor.Presentation.Authoring.Windows{
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
                // SyncTarget(null) runs every authoring frame. Closing an already
                // closed menu must be a no-op or its revision invalidates the
                // entire cached shell projection, including the asset catalog.
                if (_model == null
                    || (!_model.Visible
                        && string.IsNullOrEmpty(_model.Title)
                        && string.IsNullOrEmpty(_model.Detail)
                        && _model.AnchorX == 0f
                        && _model.AnchorY == 0f
                        && !_model.CenterOnScreen
                        && (_model.Actions == null || _model.Actions.Length == 0)))
                {
                    return;
                }

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
