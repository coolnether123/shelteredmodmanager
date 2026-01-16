using System;
using System.Drawing;
using System.Windows.Forms;

namespace Manager.Controls
{
    /// <summary>
    /// Modern styled action button with icons and hover effects.
    /// </summary>
    public class ActionButton : Button
    {
        private Color _normalBackColor;
        private Color _hoverBackColor;
        private Color _pressedBackColor;
        private bool _isHovered = false;
        private bool _isPressed = false;
        private bool _isPrimary = false;

        public bool IsPrimary
        {
            get { return _isPrimary; }
            set
            {
                _isPrimary = value;
                UpdateColors();
            }
        }

        public ActionButton()
        {
            this.FlatStyle = FlatStyle.Flat;
            this.FlatAppearance.BorderSize = 1;
            this.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            this.Cursor = Cursors.Hand;
            this.Height = 35;
            this.MinimumSize = new Size(100, 35);

            UpdateColors();
        }

        private void UpdateColors()
        {
            if (_isPrimary)
            {
                // Primary action button (blue)
                _normalBackColor = Color.FromArgb(0, 120, 215);
                _hoverBackColor = Color.FromArgb(0, 100, 180);
                _pressedBackColor = Color.FromArgb(0, 80, 150);
                this.ForeColor = Color.White;
                this.FlatAppearance.BorderColor = Color.FromArgb(0, 100, 180);
            }
            else
            {
                // Secondary button (neutral)
                _normalBackColor = Color.FromArgb(240, 240, 240);
                _hoverBackColor = Color.FromArgb(220, 220, 220);
                _pressedBackColor = Color.FromArgb(200, 200, 200);
                this.ForeColor = Color.FromArgb(60, 60, 60);
                this.FlatAppearance.BorderColor = Color.FromArgb(180, 180, 180);
            }

            this.BackColor = _normalBackColor;
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _isHovered = true;
            this.BackColor = _hoverBackColor;
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _isHovered = false;
            _isPressed = false;
            this.BackColor = _normalBackColor;
        }

        protected override void OnMouseDown(MouseEventArgs mevent)
        {
            base.OnMouseDown(mevent);
            _isPressed = true;
            this.BackColor = _pressedBackColor;
        }

        protected override void OnMouseUp(MouseEventArgs mevent)
        {
            base.OnMouseUp(mevent);
            _isPressed = false;
            this.BackColor = _isHovered ? _hoverBackColor : _normalBackColor;
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            if (!this.Enabled)
            {
                this.BackColor = Color.FromArgb(230, 230, 230);
                this.ForeColor = Color.Gray;
            }
            else
            {
                UpdateColors();
            }
        }

        /// <summary>
        /// Apply dark theme colors
        /// </summary>
        public void ApplyTheme(bool isDark)
        {
            if (isDark)
            {
                if (_isPrimary)
                {
                    _normalBackColor = Color.FromArgb(0, 122, 204);
                    _hoverBackColor = Color.FromArgb(0, 140, 230);
                    _pressedBackColor = Color.FromArgb(0, 100, 180);
                    this.ForeColor = Color.White;
                    this.FlatAppearance.BorderColor = Color.FromArgb(0, 140, 230);
                }
                else
                {
                    _normalBackColor = Color.FromArgb(70, 70, 70);
                    _hoverBackColor = Color.FromArgb(90, 90, 90);
                    _pressedBackColor = Color.FromArgb(50, 50, 50);
                    this.ForeColor = Color.White;
                    this.FlatAppearance.BorderColor = Color.FromArgb(100, 100, 100);
                }
            }
            else
            {
                UpdateColors();
            }

            this.BackColor = _normalBackColor;
        }
    }
}
