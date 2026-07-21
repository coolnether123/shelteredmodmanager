using System;
using System.Collections.Generic;

namespace ModAPI.UI.ColorPicker
{
    /// <summary>
    /// Recent and pinned color collection used by color picker sessions.
    /// </summary>
    public sealed class ColorPickerPalette
    {
        public const int DefaultMaxRecent = 18;
        public const int DefaultMaxPinned = 9;

        private readonly List<ModColor> _recentColors = new List<ModColor>();
        private readonly List<ModColor> _pinnedColors = new List<ModColor>();

        public ColorPickerPalette()
            : this(DefaultMaxRecent, DefaultMaxPinned)
        {
        }

        public ColorPickerPalette(int maxRecent, int maxPinned)
        {
            MaxRecent = maxRecent < 1 ? DefaultMaxRecent : maxRecent;
            MaxPinned = maxPinned < 0 ? DefaultMaxPinned : maxPinned;
        }

        public int MaxRecent { get; private set; }
        public int MaxPinned { get; private set; }
        public int RecentCount { get { return _recentColors.Count; } }
        public int PinnedCount { get { return _pinnedColors.Count; } }
        public bool IsDirty { get; private set; }

        public ModColor[] RecentColors
        {
            get { return _recentColors.ToArray(); }
        }

        public ModColor[] PinnedColors
        {
            get { return _pinnedColors.ToArray(); }
        }

        public void MarkClean()
        {
            IsDirty = false;
        }

        public void AddRecent(ModColor color)
        {
            RemoveAll(_recentColors, color);
            _recentColors.Insert(0, color);

            while (_recentColors.Count > MaxRecent)
                _recentColors.RemoveAt(_recentColors.Count - 1);

            IsDirty = true;
        }

        public bool IsPinned(ModColor color)
        {
            return IndexOf(_pinnedColors, color) >= 0;
        }

        public bool CanPin()
        {
            return _pinnedColors.Count < MaxPinned;
        }

        public bool Pin(ModColor color)
        {
            if (IsPinned(color) || !CanPin())
                return false;

            _pinnedColors.Insert(0, color);
            IsDirty = true;
            return true;
        }

        public bool Unpin(ModColor color)
        {
            int index = IndexOf(_pinnedColors, color);
            if (index < 0)
                return false;

            _pinnedColors.RemoveAt(index);
            IsDirty = true;
            return true;
        }

        public bool TogglePin(ModColor color)
        {
            return IsPinned(color) ? Unpin(color) : Pin(color);
        }

        public void Load(ModColor[] recent, ModColor[] pinned)
        {
            _recentColors.Clear();
            _pinnedColors.Clear();

            AppendUnique(_recentColors, recent, MaxRecent);
            AppendUnique(_pinnedColors, pinned, MaxPinned);
            IsDirty = false;
        }

        private static void AppendUnique(List<ModColor> target, ModColor[] colors, int max)
        {
            if (target == null || colors == null)
                return;

            for (int i = 0; i < colors.Length && target.Count < max; i++)
            {
                if (IndexOf(target, colors[i]) < 0)
                    target.Add(colors[i]);
            }
        }

        private static void RemoveAll(List<ModColor> list, ModColor color)
        {
            if (list == null)
                return;

            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (list[i].NearlyEquals(color))
                    list.RemoveAt(i);
            }
        }

        private static int IndexOf(List<ModColor> list, ModColor color)
        {
            if (list == null)
                return -1;

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].NearlyEquals(color))
                    return i;
            }

            return -1;
        }
    }
}
