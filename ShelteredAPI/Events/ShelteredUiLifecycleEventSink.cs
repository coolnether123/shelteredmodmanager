using ModAPI.Core;
using ShelteredAPI.Events;
using UnityEngine;

namespace ShelteredAPI.Events
{
    internal sealed class ShelteredUiLifecycleEventSink : IUiLifecycleEventSink
    {
        public void RaisePanelOpened(object panel)
        {
            UIEvents.RaisePanelOpened(panel as BasePanel);
        }

        public void RaisePanelClosed(object panel)
        {
            UIEvents.RaisePanelClosed(panel as BasePanel);
        }

        public void RaisePanelResumed(object panel)
        {
            UIEvents.RaisePanelResumed(panel as BasePanel);
        }

        public void RaisePanelPaused(object panel)
        {
            UIEvents.RaisePanelPaused(panel as BasePanel);
        }

        public void RaiseButtonClicked(object button, string buttonName)
        {
            UIEvents.RaiseButtonClicked(button as GameObject, buttonName);
        }
    }
}
