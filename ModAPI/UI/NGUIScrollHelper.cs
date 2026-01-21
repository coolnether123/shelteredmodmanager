using System.Collections.Generic;
using UnityEngine;
using ModAPI.Core;

namespace ModAPI.UI
{
    /// <summary>
    /// Helper class to add mouse wheel scrolling to a list of NGUI elements.
    /// Useful for lists that exceed their container bounds.
    /// </summary>
    public class NGUIScrollHelper : MonoBehaviour
    {
        private List<GameObject> _items = new List<GameObject>();
        private float _itemSpacing;
        private float _startY;
        private float _minY;
        private float _maxY;
        private float _minX = -1000f; // Left bound for mouse check
        private float _maxX = 1000f;  // Right bound for mouse check
        private int _currentOffset = 0;
        private int _maxVisibleItems;
        
        /// <summary>
        /// Initialize the scroll helper with the list of items and bounds
        /// </summary>
        /// <param name="items">List of GameObjects to scroll (buttons, labels, etc)</param>
        /// <param name="startY">Starting Y position for first item</param>
        /// <param name="itemSpacing">Vertical spacing between items</param>
        /// <param name="minY">Minimum Y boundary (bottom of scroll area)</param>
        /// <param name="maxY">Maximum Y boundary (top of scroll area - typically startY)</param>
        /// <param name="minX">Optional minimum X boundary for mouse position check (default: -1000)</param>
        /// <param name="maxX">Optional maximum X boundary for mouse position check (default: 1000)</param>
        public void Initialize(List<GameObject> items, float startY, float itemSpacing, float minY, float maxY, float minX = -1000f, float maxX = 1000f)
        {
            _items = items;
            _itemSpacing = itemSpacing;
            _startY = startY;
            _minY = minY;
            _maxY = maxY;
            _minX = minX;
            _maxX = maxX;
            
            // Calculate how many items can be visible at once
            float availableHeight = maxY - minY;
            _maxVisibleItems = Mathf.FloorToInt(availableHeight / itemSpacing) + 1;
            
            MMLog.Write("[NGUIScrollHelper] Initialized with " + items.Count + " items.");
            
            UpdateItemPositions();
        }
        
        private int _frameCount = 0;
        
        void Update()
        {
            if (_items == null || _items.Count == 0 || _items.Count <= _maxVisibleItems) return;
            
            // Mouse wheel scrolling - restricted to horizontal bounds defined in Initialize()
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            
            // Fallback to delta for some Unity versions/input setups
            if (scroll == 0f)
            {
                scroll = Input.mouseScrollDelta.y;
            }
            
            if (scroll != 0f)
            {
                // Convert screen mouse position to UI coordinates for the boundary check
                Vector3 mousePos = Input.mousePosition;
                float uiX = (mousePos.x - Screen.width / 2f);
                
                // Only scroll if mouse is within the specified horizontal range (e.g. over the left page)
                if (uiX < _minX || uiX > _maxX) return; 
                
                int previousOffset = _currentOffset;
                
                if (scroll > 0f && _currentOffset > 0)
                {
                    _currentOffset--;
                }
                else if (scroll < 0f && _currentOffset < _items.Count - _maxVisibleItems)
                {
                    _currentOffset++;
                }
                if (_currentOffset != previousOffset)
                {
                    UpdateItemPositions();
                }
            }
        }
        
        private void UpdateItemPositions()
        {
            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i] == null) continue;
                
                // Calculate position relative to scroll offset
                int displayIndex = i - _currentOffset;
                
                // Hide items outside visible range
                if (displayIndex < 0 || displayIndex >= _maxVisibleItems)
                {
                    _items[i].SetActive(false);
                }
                else
                {
                    _items[i].SetActive(true);
                    
                    // Position within visible range
                    float yPos = _startY - (displayIndex * _itemSpacing);
                    var pos = _items[i].transform.localPosition;
                    _items[i].transform.localPosition = new Vector3(pos.x, yPos, pos.z);
                }
            }
        }
        
        /// <summary>
        /// Reset scroll to top
        /// </summary>
        public void ScrollToTop()
        {
            _currentOffset = 0;
            UpdateItemPositions();
        }
        
        /// <summary>
        /// Scroll to show a specific item index
        /// </summary>
        public void ScrollToItem(int index)
        {
            if (index < 0 || index >= _items.Count) return;
            
            // Ensure item is visible
            if (index < _currentOffset)
            {
                _currentOffset = index;
            }
            else if (index >= _currentOffset + _maxVisibleItems)
            {
                _currentOffset = index - _maxVisibleItems + 1;
            }
            
            _currentOffset = Mathf.Clamp(_currentOffset, 0, Mathf.Max(0, _items.Count - _maxVisibleItems));
            UpdateItemPositions();
        }
    }
}
