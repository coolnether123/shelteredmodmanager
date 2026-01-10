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
            
            MMLog.Write("[NGUIScrollHelper] Initialized with " + items.Count + " items, max visible: " + _maxVisibleItems);
            
            UpdateItemPositions();
        }
        
        private int _frameCount = 0;
        
        void Update()
        {
            _frameCount++;
            
            // Log every 60 frames to confirm Update is running
            if (_frameCount % 60 == 0)
            {
                MMLog.Write("[NGUIScrollHelper] Update alive, offset=" + _currentOffset + " items=" + (_items != null ? _items.Count : 0));
            }
            
            if (_items == null || _items.Count == 0)
            {
                if (_frameCount % 60 == 0) MMLog.Write("[NGUIScrollHelper] No items to scroll");
                return;
            }
            
            if (_items.Count <= _maxVisibleItems)
            {
                if (_frameCount % 60 == 0) MMLog.Write("[NGUIScrollHelper] All items visible, no scroll needed");
                return;
            }
            
            // Mouse wheel scrolling - but only if mouse is in our bounds
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            
            // Also try raw mouse scroll
            if (scroll == 0f)
            {
                scroll = Input.mouseScrollDelta.y;
            }
            
            if (scroll != 0f)
            {
                // Check if mouse is within our horizontal bounds
                // Need to convert screen mouse position to UI coordinates
                Vector3 mousePos = Input.mousePosition;
                
                // Convert to UI space (NGUI uses screen height as reference)
                float screenHeight = Screen.height;
                float uiX = (mousePos.x - Screen.width / 2f);
                
                // Check horizontal bounds
                if (uiX < _minX || uiX > _maxX)
                {
                    if (_frameCount % 120 == 0)
                    {
                        MMLog.Write(string.Format("[NGUIScrollHelper] Scroll ignored - mouse X {0:F1} outside bounds [{1:F1}, {2:F1}]", 
                            uiX, _minX, _maxX));
                    }
                    return; // Mouse not in our scroll area
                }
                
                MMLog.Write("[NGUIScrollHelper] Scroll input detected: " + scroll + " at mouse X: " + uiX);
                
                int previousOffset = _currentOffset;
                
                // Scroll up (positive) = decrease offset (show earlier items)
                // Scroll down (negative) = increase offset (show later items)
                if (scroll > 0f && _currentOffset > 0)
                {
                    _currentOffset--;
                    MMLog.Write("[NGUIScrollHelper] Scrolling up, new offset: " + _currentOffset);
                }
                else if (scroll < 0f && _currentOffset < _items.Count - _maxVisibleItems)
                {
                    _currentOffset++;
                    MMLog.Write("[NGUIScrollHelper] Scrolling down, new offset: " + _currentOffset);
                }
                else
                {
                    MMLog.Write("[NGUIScrollHelper] At scroll boundary, offset: " + _currentOffset + " max: " + (_items.Count - _maxVisibleItems));
                }
                
                if (_currentOffset != previousOffset)
                {
                    UpdateItemPositions();
                    MMLog.Write("[NGUIScrollHelper] Updated positions, offset now: " + _currentOffset);
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
