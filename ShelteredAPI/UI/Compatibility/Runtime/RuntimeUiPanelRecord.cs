using System;
using UnityEngine;

namespace ShelteredAPI.UI.Internal
{
    internal sealed class RuntimeUiPanelRecord
    {
        public string PanelId;
        public string OwnerId;
        public string Kind;
        public GameObject Root;
        public object Request;
        public bool RefreshEveryFrame;
        public bool RebindRequested;
        public Action<RuntimeUiPanelRecord> Build;
        public Action<RuntimeUiPanelRecord> Refresh;
        public Action<RuntimeUiPanelRecord> Close;
    }
}
