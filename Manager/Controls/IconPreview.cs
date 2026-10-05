using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace Manager.Controls
{
    /// <summary>
    /// Pixel-art preview drawn with nearest-neighbour scaling on a checkerboard.
    /// </summary>
    public class IconPreview : Control
    {
        private Image _image;
        private string _emptyText = "No icon";

        public IconPreview()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw,
                true);
            SetStyle(ControlStyles.Selectable, false);
            TabStop = false;
            AccessibleRole = AccessibleRole.Graphic;
            AccessibleName = "Icon preview";
            Size = new Size(64, 64);
        }

        public Image Image
        {
            get { return _image; }
            set
            {
                if (_image != null && !ReferenceEquals(_image, value))
                    _image.Dispose();
                _image = value;
                Invalidate();
            }
        }

        public string EmptyText
        {
            get { return _emptyText; }
            set
            {
                _emptyText = value ?? string.Empty;
                Invalidate();
            }
        }

        /// <summary>Loads a copy of a PNG so the file is never locked by the preview.</summary>
        public static Bitmap LoadUnlocked(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return null;
            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                using (MemoryStream stream = new MemoryStream(bytes))
                using (Image source = Image.FromStream(stream))
                    return new Bitmap(source);
            }
            catch (ArgumentException) { return null; }
            catch (IOException) { return null; }
            catch (UnauthorizedAccessException) { return null; }
            catch (OutOfMemoryException) { return null; }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            ThemePalette palette = ManagerTheme.Current;
            Graphics g = e.Graphics;
            g.Clear(Parent != null ? Parent.BackColor : palette.Surface);
            Rectangle frame = new Rectangle(0, 0, Width - 1, Height - 1);

            if (_image == null)
            {
                using (SolidBrush well = new SolidBrush(palette.SurfaceAlt))
                    g.FillRectangle(well, frame);
                TextRenderer.DrawText(g, _emptyText, ManagerTheme.UiFont(8f), frame, palette.TextMuted,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
            }
            else
            {
                DrawCheckerboard(g, frame, palette);
                int scale = Math.Max(1, Math.Min((Width - 4) / Math.Max(1, _image.Width), (Height - 4) / Math.Max(1, _image.Height)));
                int drawWidth = _image.Width * scale;
                int drawHeight = _image.Height * scale;
                if (drawWidth > Width - 4 || drawHeight > Height - 4)
                {
                    float fit = Math.Min((Width - 4f) / _image.Width, (Height - 4f) / _image.Height);
                    drawWidth = Math.Max(1, (int)(_image.Width * fit));
                    drawHeight = Math.Max(1, (int)(_image.Height * fit));
                }
                Rectangle target = new Rectangle((Width - drawWidth) / 2, (Height - drawHeight) / 2, drawWidth, drawHeight);
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                g.DrawImage(_image, target);
                g.InterpolationMode = InterpolationMode.Default;
                g.PixelOffsetMode = PixelOffsetMode.Default;
            }
            ManagerTheme.DrawOutline(g, ClientRectangle, palette.Border);
        }

        private static void DrawCheckerboard(Graphics g, Rectangle bounds, ThemePalette palette)
        {
            Color light = palette.IsDark ? Color.FromArgb(58, 60, 66) : Color.FromArgb(236, 238, 241);
            Color dark = palette.IsDark ? Color.FromArgb(46, 48, 53) : Color.FromArgb(222, 225, 230);
            const int cell = 6;
            using (SolidBrush a = new SolidBrush(light))
            using (SolidBrush b = new SolidBrush(dark))
            {
                for (int y = bounds.Top; y < bounds.Bottom; y += cell)
                    for (int x = bounds.Left; x < bounds.Right; x += cell)
                        g.FillRectangle((((x - bounds.Left) / cell) + ((y - bounds.Top) / cell)) % 2 == 0 ? a : b,
                            x, y, Math.Min(cell, bounds.Right - x), Math.Min(cell, bounds.Bottom - y));
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _image != null)
            {
                _image.Dispose();
                _image = null;
            }
            base.Dispose(disposing);
        }
    }
}
