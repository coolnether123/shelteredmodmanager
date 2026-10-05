using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace Manager.Controls
{
    /// <summary>
    /// Draws a small pixel-art sprite from a character grid at an integer
    /// scale, matching the game's pixel style without shipping image assets.
    /// </summary>
    public class PixelSprite : Control
    {
        // A wooden supply crate: the Content Workshop's mark.
        public static readonly string[] Crate = new string[]
        {
            "................",
            ".oooooooooooooo.",
            ".omnhhhhhhhhnmo.",
            ".onwwwwwwwwwwno.",
            ".ohwwwwwwwwwdwo.",
            ".ohwwwwwwwwdwwo.",
            ".oddddddddddddo.",
            ".ohwwwwwwdwwwwo.",
            ".ohwwwwwdwwwwwo.",
            ".ohwwwwdwwwwwwo.",
            ".oddddddddddddo.",
            ".ohwwdwwwwwwwwo.",
            ".onwdwwwwwwwwno.",
            ".omnddddddddnmo.",
            ".oooooooooooooo.",
            "................"
        };

        private static readonly Dictionary<char, Color> CratePalette = new Dictionary<char, Color>
        {
            { 'o', Color.FromArgb(43, 29, 20) },
            { 'h', Color.FromArgb(199, 146, 88) },
            { 'w', Color.FromArgb(158, 107, 62) },
            { 'd', Color.FromArgb(107, 68, 39) },
            { 'm', Color.FromArgb(169, 164, 154) },
            { 'n', Color.FromArgb(95, 90, 82) }
        };

        private string[] _rows = Crate;
        private int _scale = 6;

        public PixelSprite()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.SupportsTransparentBackColor,
                true);
            SetStyle(ControlStyles.Selectable, false);
            TabStop = false;
            AccessibleRole = AccessibleRole.Graphic;
            AccessibleName = "Supply crate illustration";
            UpdateSize();
        }

        public int PixelScale
        {
            get { return _scale; }
            set
            {
                _scale = value < 1 ? 1 : value;
                UpdateSize();
                Invalidate();
            }
        }

        private void UpdateSize()
        {
            Size = new Size(_rows[0].Length * _scale, _rows.Length * _scale);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent != null ? Parent.BackColor : BackColor);
            for (int y = 0; y < _rows.Length; y++)
            {
                string row = _rows[y];
                for (int x = 0; x < row.Length; x++)
                {
                    Color color;
                    if (!CratePalette.TryGetValue(row[x], out color))
                        continue;
                    using (SolidBrush brush = new SolidBrush(color))
                        e.Graphics.FillRectangle(brush, x * _scale, y * _scale, _scale, _scale);
                }
            }
        }
    }
}
