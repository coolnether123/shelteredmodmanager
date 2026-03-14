using ModAPI.InputServices;
using ModAPI.UI;
using UnityEngine;

namespace ModAPI.Internal.UI
{
    internal sealed class ModManagerDescriptionScroller : MonoBehaviour
    {
        private UILabel _label;
        private float _clipHeight;
        private float _minX;
        private float _maxX;
        private float _startY;
        private float _scrollSpeed;

        public void Initialize(UILabel label, float clipHeight, float minX, float maxX, float startY, float scrollSpeed)
        {
            _label = label;
            _clipHeight = clipHeight;
            _minX = minX;
            _maxX = maxX;
            _startY = startY;
            _scrollSpeed = scrollSpeed;
        }

        public void ResetToTop()
        {
            if (_label == null)
                return;

            _label.transform.localPosition = new Vector3(0f, _startY, 0f);
        }

        private void Update()
        {
            if (_label == null || _label.height <= _clipHeight)
                return;

            float scroll;
            if (!ScrollInputService.TryGetVerticalScroll(ScrollInputQuery.ForUiRange(_minX, _maxX), out scroll))
            {
                if (Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.PageUp))
                    scroll = 3f * Time.unscaledDeltaTime;
                else if (Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.PageDown))
                    scroll = -3f * Time.unscaledDeltaTime;
            }

            if (scroll == 0f)
                return;

            Vector3 pos = _label.transform.localPosition;
            pos.y -= scroll * _scrollSpeed;

            float minY = _startY;
            float maxY = _startY + (_label.height - _clipHeight);
            pos.y = Mathf.Clamp(pos.y, minY, maxY);
            _label.transform.localPosition = pos;
        }
    }
}
