using UnityEngine;


using ShelteredAPI.Saves;
namespace ShelteredAPI.UI.FieldManual.Tooltips
{
    /// <summary>
    /// Drop-in MonoBehaviour that publishes a <see cref="TooltipMessage"/> to a
    /// shared <see cref="ITooltipBus"/> while the host GameObject is being hovered.
    /// Cleans up its own stack frame on disable/destroy so panels can tear down
    /// without leaking stale tooltip state.
    ///
    /// Attach to any GameObject that already has a collider; NGUI's hover events
    /// drive the lifecycle.
    /// </summary>
    internal sealed class HoverTooltipTrigger : MonoBehaviour
    {
        private ITooltipBus _bus;
        private TooltipMessage _message;
        private int _token;

        public static HoverTooltipTrigger Attach(GameObject host, ITooltipBus bus, TooltipMessage message)
        {
            if (host == null || bus == null) return null;
            HoverTooltipTrigger trigger = host.GetComponent<HoverTooltipTrigger>();
            if (trigger == null) trigger = host.AddComponent<HoverTooltipTrigger>();
            trigger._bus = bus;
            trigger._message = message;
            return trigger;
        }

        public void SetMessage(TooltipMessage message)
        {
            _message = message;
            // If we are currently hovered, refresh by re-pushing.
            if (_token != 0)
            {
                _bus.Pop(_token);
                _token = _bus.Push(_message);
            }
        }

        private void OnHover(bool isOver)
        {
            if (_bus == null) return;
            if (isOver)
            {
                if (_token == 0) _token = _bus.Push(_message);
            }
            else
            {
                if (_token != 0) { _bus.Pop(_token); _token = 0; }
            }
        }

        private void OnDisable()
        {
            if (_bus != null && _token != 0) { _bus.Pop(_token); _token = 0; }
        }

        private void OnDestroy()
        {
            if (_bus != null && _token != 0) { _bus.Pop(_token); _token = 0; }
        }
    }
}
