using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using ShelteredModManager.Shared.PixelEditing;

namespace Manager.Controls
{
    /// <summary>
    /// System.Drawing host for the shared pixel-editing session. This control owns
    /// pointer/rendering concerns only; history and document mutations stay shared.
    /// </summary>
    public sealed class PixelEditorCanvas : Control
    {
        private PixelEditorSession _session;
        private bool _pointerDown;
        private int _zoom = 12;

        public event EventHandler DocumentChanged;
        public event EventHandler ActiveColorChanged;

        public PixelEditorCanvas()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.UserPaint,
                true);
            BackColor = Color.FromArgb(36, 36, 38);
            TabStop = true;
            Session = new PixelEditorSession(new PixelDocument(32, 32), 50);
        }

        public PixelEditorSession Session
        {
            get { return _session; }
            private set
            {
                _session = value;
                Invalidate();
            }
        }

        public PixelEditorTool ActiveTool
        {
            get { return Session.ActiveTool; }
            set { Session.ActiveTool = value; }
        }

        public Color ActiveColor
        {
            get
            {
                Rgba32 value = Session.ActiveColor;
                return Color.FromArgb(value.A, value.R, value.G, value.B);
            }
            set
            {
                Session.ActiveColor = new Rgba32(value.R, value.G, value.B, value.A);
                OnActiveColorChanged();
            }
        }

        public int Zoom
        {
            get { return _zoom; }
            set
            {
                _zoom = Math.Max(2, Math.Min(48, value));
                AutoScrollMinSizeChanged();
                Invalidate();
            }
        }

        public Size CanvasPixelSize
        {
            get { return new Size(Session.Document.Width, Session.Document.Height); }
        }

        public void CreateDocument(int width, int height)
        {
            Session = new PixelEditorSession(new PixelDocument(width, height), 50);
            AutoScrollMinSizeChanged();
            OnDocumentChanged();
        }

        public bool LoadPng(string path, out string error)
        {
            PixelDocument document;
            if (!PixelEditorDrawingCodec.TryLoadPng(path, out document, out error))
                return false;
            Session = new PixelEditorSession(document, 50);
            Session.MarkSaved();
            AutoScrollMinSizeChanged();
            OnDocumentChanged();
            return true;
        }

        public bool SavePng(string path, out string error)
        {
            if (!PixelEditorDrawingCodec.TrySavePng(Session.Document, path, out error))
                return false;
            Session.MarkSaved();
            return true;
        }

        public bool Undo()
        {
            if (!Session.Undo())
                return false;
            Invalidate();
            OnDocumentChanged();
            return true;
        }

        public bool Redo()
        {
            if (!Session.Redo())
                return false;
            Invalidate();
            OnDocumentChanged();
            return true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            PixelDocument document = Session.Document;
            int width = document.Width * Zoom;
            int height = document.Height * Zoom;
            int originX = Math.Max(0, (ClientSize.Width - width) / 2);
            int originY = Math.Max(0, (ClientSize.Height - height) / 2);

            e.Graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.Half;
            for (int y = 0; y < document.Height; y++)
            {
                for (int x = 0; x < document.Width; x++)
                {
                    Rectangle cell = new Rectangle(
                        originX + x * Zoom,
                        originY + y * Zoom,
                        Zoom,
                        Zoom);
                    Rgba32 pixel = document.GetPixel(x, y);
                    if (pixel.A < 255)
                        DrawTransparency(e.Graphics, cell, x, y);
                    if (pixel.A > 0)
                    {
                        using (SolidBrush brush = new SolidBrush(
                            Color.FromArgb(pixel.A, pixel.R, pixel.G, pixel.B)))
                            e.Graphics.FillRectangle(brush, cell);
                    }
                    if (Zoom >= 8)
                    {
                        using (Pen grid = new Pen(Color.FromArgb(45, Color.Black)))
                            e.Graphics.DrawRectangle(grid, cell.X, cell.Y, cell.Width, cell.Height);
                    }
                }
            }
            using (Pen border = new Pen(Color.DimGray))
                e.Graphics.DrawRectangle(border, originX, originY, width, height);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            if (e.Button != MouseButtons.Left)
                return;
            _pointerDown = true;
            Session.BeginStroke();
            ApplyPointer(e.Location);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_pointerDown)
                ApplyPointer(e.Location);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (!_pointerDown)
                return;
            _pointerDown = false;
            Session.EndStroke();
            OnDocumentChanged();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if ((ModifierKeys & Keys.Control) == Keys.Control)
                Zoom += e.Delta > 0 ? 2 : -2;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.Z)
            {
                Undo();
                e.Handled = true;
            }
            else if (e.Control && e.KeyCode == Keys.Y)
            {
                Redo();
                e.Handled = true;
            }
            base.OnKeyDown(e);
        }

        private void ApplyPointer(Point point)
        {
            int width = Session.Document.Width * Zoom;
            int height = Session.Document.Height * Zoom;
            int originX = Math.Max(0, (ClientSize.Width - width) / 2);
            int originY = Math.Max(0, (ClientSize.Height - height) / 2);
            int x = (point.X - originX) / Zoom;
            int y = (point.Y - originY) / Zoom;
            if (point.X < originX || point.Y < originY || !Session.Document.Contains(x, y))
                return;

            if (Session.ActiveTool == PixelEditorTool.Pick)
            {
                if (Session.PickColor(x, y))
                    OnActiveColorChanged();
                return;
            }
            if (Session.ActiveTool == PixelEditorTool.Paint ||
                Session.ActiveTool == PixelEditorTool.Erase)
            {
                if (Session.PaintPixel(x, y))
                {
                    Invalidate();
                    OnDocumentChanged();
                }
            }
        }

        private static void DrawTransparency(Graphics graphics, Rectangle cell, int x, int y)
        {
            Color color = ((x + y) & 1) == 0
                ? Color.FromArgb(210, 210, 210)
                : Color.FromArgb(160, 160, 160);
            using (SolidBrush brush = new SolidBrush(color))
                graphics.FillRectangle(brush, cell);
        }

        private void AutoScrollMinSizeChanged()
        {
            MinimumSize = new Size(
                Math.Min(100, Session.Document.Width * Zoom),
                Math.Min(100, Session.Document.Height * Zoom));
        }

        private void OnDocumentChanged()
        {
            EventHandler handler = DocumentChanged;
            if (handler != null)
                handler(this, EventArgs.Empty);
        }

        private void OnActiveColorChanged()
        {
            Invalidate();
            EventHandler handler = ActiveColorChanged;
            if (handler != null)
                handler(this, EventArgs.Empty);
        }
    }

    internal static class PixelEditorDrawingCodec
    {
        public static bool TryLoadPng(string path, out PixelDocument document, out string error)
        {
            document = null;
            error = null;
            try
            {
                using (Image source = Image.FromFile(path))
                using (Bitmap bitmap = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb))
                {
                    using (Graphics graphics = Graphics.FromImage(bitmap))
                        graphics.DrawImageUnscaled(source, 0, 0);
                    document = FromBitmap(bitmap);
                }
                return true;
            }
            catch (Exception ex)
            {
                error = "Could not load PNG: " + ex.Message;
                return false;
            }
        }

        public static bool TrySavePng(PixelDocument document, string path, out string error)
        {
            error = null;
            string temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                using (Bitmap bitmap = ToBitmap(document))
                    bitmap.Save(temporary, ImageFormat.Png);
                if (File.Exists(path))
                    File.Delete(path);
                File.Move(temporary, path);
                return true;
            }
            catch (Exception ex)
            {
                try { if (File.Exists(temporary)) File.Delete(temporary); }
                catch { }
                error = "Could not save PNG: " + ex.Message;
                return false;
            }
        }

        private static PixelDocument FromBitmap(Bitmap bitmap)
        {
            Rectangle area = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
            BitmapData data = bitmap.LockBits(area, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                byte[] rgba = new byte[bitmap.Width * bitmap.Height * 4];
                byte[] row = new byte[Math.Abs(data.Stride)];
                for (int y = 0; y < bitmap.Height; y++)
                {
                    Marshal.Copy(new IntPtr(data.Scan0.ToInt64() + y * data.Stride), row, 0, row.Length);
                    for (int x = 0; x < bitmap.Width; x++)
                    {
                        int source = x * 4;
                        int target = (y * bitmap.Width + x) * 4;
                        rgba[target] = row[source + 2];
                        rgba[target + 1] = row[source + 1];
                        rgba[target + 2] = row[source];
                        rgba[target + 3] = row[source + 3];
                    }
                }
                return new PixelDocument(bitmap.Width, bitmap.Height, rgba);
            }
            finally
            {
                bitmap.UnlockBits(data);
            }
        }

        private static Bitmap ToBitmap(PixelDocument document)
        {
            Bitmap bitmap = new Bitmap(document.Width, document.Height, PixelFormat.Format32bppArgb);
            Rectangle area = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
            BitmapData data = bitmap.LockBits(area, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try
            {
                byte[] row = new byte[Math.Abs(data.Stride)];
                for (int y = 0; y < document.Height; y++)
                {
                    Array.Clear(row, 0, row.Length);
                    for (int x = 0; x < document.Width; x++)
                    {
                        Rgba32 pixel = document.GetPixel(x, y);
                        int target = x * 4;
                        row[target] = pixel.B;
                        row[target + 1] = pixel.G;
                        row[target + 2] = pixel.R;
                        row[target + 3] = pixel.A;
                    }
                    Marshal.Copy(row, 0, new IntPtr(data.Scan0.ToInt64() + y * data.Stride), row.Length);
                }
            }
            finally
            {
                bitmap.UnlockBits(data);
            }
            return bitmap;
        }
    }
}
