using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Reflection;

namespace Hytux
{
    class AnimatedColor
    {
        public Color Current;
        public Color Target;

        public AnimatedColor(Color initial)
        {
            Current = initial;
            Target = initial;
        }

        public void Update()
        {
            if (Current == Target) return;
            float speed = 0.05f;
            int r = (int)(Current.R + (Target.R - Current.R) * speed);
            int g = (int)(Current.G + (Target.G - Current.G) * speed);
            int b = (int)(Current.B + (Target.B - Current.B) * speed);
            r = Math.Max(0, Math.Min(255, r));
            g = Math.Max(0, Math.Min(255, g));
            b = Math.Max(0, Math.Min(255, b));
            Current = Color.FromArgb(r, g, b);
        }
    }

    class CustomScrollablePanel : Panel
    {
        public Panel Content;
        private float scrollY = 0;
        private float targetScrollY = 0;
        private System.Windows.Forms.Timer scrollTimer;
        private Rectangle scrollThumbRect;
        private bool isDraggingThumb = false;
        private int dragStartY;
        private float dragStartScrollY;
        private Color accentColor = Color.FromArgb(145, 100, 255);

        public CustomScrollablePanel()
        {
            DoubleBuffered = true;
            Content = new Panel { Location = new Point(0, 0), BackColor = Color.Transparent, Width = this.Width };
            this.Controls.Add(Content);

            scrollTimer = new System.Windows.Forms.Timer { Interval = 15 };
            scrollTimer.Tick += (s, e) =>
            {
                int maxB = 0;
                foreach (Control c in Content.Controls) if (c.Bottom > maxB) maxB = c.Bottom;
                if (Content.Height != maxB + 20) Content.Height = maxB + 20;

                if (Math.Abs(targetScrollY - scrollY) > 0.5f)
                {
                    scrollY += (targetScrollY - scrollY) * 0.3f;
                    Content.Top = -(int)scrollY;
                    Invalidate();
                }
            };
            scrollTimer.Start();
        }

        public void ScrollDelta(int delta)
        {
            int maxScroll = Math.Max(0, Content.Height - this.Height);
            if (maxScroll > 0)
            {
                targetScrollY -= delta;
                targetScrollY = Math.Max(0, Math.Min(maxScroll, targetScrollY));
            }
        }

        protected override void OnResize(EventArgs eventargs)
        {
            base.OnResize(eventargs);
            Content.Width = this.Width - 14;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            int maxScroll = Math.Max(0, Content.Height - this.Height);
            if (maxScroll > 0)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                int scrollbarWidth = 6;
                int trackHeight = this.Height - 20;
                int thumbHeight = Math.Max(30, (int)((this.Height / (float)Content.Height) * trackHeight));
                int thumbY = 10 + (int)((scrollY / maxScroll) * (trackHeight - thumbHeight));

                scrollThumbRect = new Rectangle(this.Width - scrollbarWidth - 4, thumbY, scrollbarWidth, thumbHeight);

                using (GraphicsPath trackPath = GetRoundedRect(new Rectangle(this.Width - scrollbarWidth - 4, 10, scrollbarWidth, trackHeight), scrollbarWidth / 2))
                using (SolidBrush bg = new SolidBrush(Color.FromArgb(15, 15, 15)))
                    e.Graphics.FillPath(bg, trackPath);

                using (GraphicsPath thumbPath = GetRoundedRect(scrollThumbRect, scrollbarWidth / 2))
                using (SolidBrush thumb = new SolidBrush(isDraggingThumb ? Color.White : accentColor))
                    e.Graphics.FillPath(thumb, thumbPath);
            }
        }

        private GraphicsPath GetRoundedRect(Rectangle bounds, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int d = radius * 2;
            if (d <= 0 || bounds.Width < d || bounds.Height < d) { path.AddRectangle(bounds); return path; }
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (scrollThumbRect.Contains(e.Location))
            {
                isDraggingThumb = true;
                dragStartY = e.Y;
                dragStartScrollY = targetScrollY;
                Invalidate();
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            isDraggingThumb = false;
            Invalidate();
            base.OnMouseUp(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (isDraggingThumb)
            {
                int maxScroll = Math.Max(0, Content.Height - this.Height);
                int trackHeight = this.Height - 20;
                int thumbHeight = Math.Max(30, (int)((this.Height / (float)Content.Height) * trackHeight));

                float scrollRatio = (float)(e.Y - dragStartY) / (trackHeight - thumbHeight);
                targetScrollY = dragStartScrollY + (scrollRatio * maxScroll);
                targetScrollY = Math.Max(0, Math.Min(maxScroll, targetScrollY));
                Invalidate();
            }
            base.OnMouseMove(e);
        }
    }

    class WheelMessageFilter : IMessageFilter
    {
        public Action<int> OnWheel;
        public bool PreFilterMessage(ref Message m)
        {
            if (m.Msg == 0x020A)
            {
                int delta = (short)((m.WParam.ToInt64() >> 16) & 0xFFFF);
                OnWheel?.Invoke(delta);
            }
            return false;
        }
    }

    class CustomToggle : Control
    {
        private bool _checked;
        private float _animationProgress = 0f;
        private System.Windows.Forms.Timer _animTimer;

        public bool Checked
        {
            get => _checked;
            set
            {
                if (_checked != value)
                {
                    _checked = value;
                    if (InvokeRequired) BeginInvoke(new Action(() => _animTimer.Start()));
                    else _animTimer.Start();
                }
            }
        }
        public Color AccentColor { get; set; } = Color.FromArgb(145, 100, 255);
        public event EventHandler CheckedChanged;

        public CustomToggle()
        {
            DoubleBuffered = true;
            Cursor = Cursors.Hand;
            Size = new Size(150, 20);
            _animTimer = new System.Windows.Forms.Timer { Interval = 1 };
            _animTimer.Tick += (s, e) =>
            {
                float target = _checked ? 1f : 0f;
                float easingFactor = 0.2f;
                float delta = target - _animationProgress;
                _animationProgress += delta * easingFactor;
                if (Math.Abs(target - _animationProgress) < 0.01f)
                {
                    _animationProgress = target;
                    _animTimer.Stop();
                }
                Invalidate();
            };
        }

        protected override void OnClick(EventArgs e)
        {
            Checked = !Checked;
            CheckedChanged?.Invoke(this, e);
            base.OnClick(e);
        }

        private GraphicsPath GetRoundedRect(Rectangle bounds, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private Color BlendColor(Color bg, Color fg, float factor)
        {
            int r = (int)(bg.R + (fg.R - bg.R) * factor);
            int g = (int)(bg.G + (fg.G - bg.G) * factor);
            int b = (int)(bg.B + (fg.B - bg.B) * factor);
            return Color.FromArgb(r, g, b);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.Clear(Parent.BackColor);
            int toggleWidth = 34;
            int toggleHeight = 18;
            int toggleY = (Height - toggleHeight) / 2;
            Rectangle toggleRect = new Rectangle(0, toggleY, toggleWidth, toggleHeight);
            Color bgColor = BlendColor(Color.FromArgb(50, 50, 50), AccentColor, _animationProgress);

            using (GraphicsPath path = GetRoundedRect(toggleRect, toggleHeight / 2))
            using (SolidBrush bgBrush = new SolidBrush(bgColor))
                e.Graphics.FillPath(bgBrush, path);

            int thumbSize = 14;
            float thumbX = 2 + (_animationProgress * (toggleWidth - thumbSize - 4));
            int thumbY = toggleY + 2;
            RectangleF thumbRect = new RectangleF(thumbX, thumbY, thumbSize, thumbSize);

            using (SolidBrush thumbBrush = new SolidBrush(Color.White))
                e.Graphics.FillEllipse(thumbBrush, thumbRect);

            using (SolidBrush textBrush = new SolidBrush(ForeColor))
                e.Graphics.DrawString(Text, Font, textBrush, toggleWidth + 5, (Height - Font.Height) / 2);
        }
    }

    class CustomSlider : Control
    {
        public int Minimum { get; set; } = 0;
        public int Maximum { get; set; } = 100;
        private int val = 0;
        public int Value
        {
            get => val;
            set { val = Math.Max(Minimum, Math.Min(Maximum, value)); Invalidate(); }
        }
        public Color AccentColor { get; set; } = Color.FromArgb(145, 100, 255);
        public event EventHandler Scroll;
        bool dragging = false;

        public CustomSlider() { DoubleBuffered = true; Size = new Size(150, 20); Cursor = Cursors.Hand; }

        protected override void OnMouseDown(MouseEventArgs e) { dragging = true; Capture = true; UpdateValue(e.X); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { dragging = false; Capture = false; base.OnMouseUp(e); }
        protected override void OnMouseMove(MouseEventArgs e) { if (dragging) UpdateValue(e.X); base.OnMouseMove(e); }

        private void UpdateValue(int x)
        {
            float percent = (float)Math.Max(0, Math.Min(x, Width)) / Width;
            int newVal = Minimum + (int)(percent * (Maximum - Minimum));
            if (newVal != val)
            {
                val = newVal;
                Scroll?.Invoke(this, EventArgs.Empty);
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.Clear(Parent.BackColor);
            int trackHeight = 4;
            int trackY = (Height - trackHeight) / 2;
            Rectangle track = new Rectangle(0, trackY, Width, trackHeight);

            using (SolidBrush bg = new SolidBrush(Color.FromArgb(50, 50, 50)))
                e.Graphics.FillRectangle(bg, track);

            float percent = Maximum == Minimum ? 0 : (float)(Value - Minimum) / (Maximum - Minimum);
            int fillWidth = (int)(percent * Width);
            Rectangle fill = new Rectangle(0, trackY, fillWidth, trackHeight);

            using (SolidBrush acc = new SolidBrush(AccentColor))
                e.Graphics.FillRectangle(acc, fill);

            int thumbSize = 12;
            int thumbX = Math.Max(0, Math.Min(Width - thumbSize, fillWidth - thumbSize / 2));
            Rectangle thumb = new Rectangle(thumbX, (Height - thumbSize) / 2, thumbSize, thumbSize);

            using (SolidBrush thumbBrush = new SolidBrush(Color.White))
                e.Graphics.FillEllipse(thumbBrush, thumb);
        }
    }

    class CustomColorButton : Control
    {
        private Color _targetColor = Color.White;
        private Color _currentColor = Color.White;
        private float _hoverAlpha = 0f;
        private System.Windows.Forms.Timer _animTimer;
        private bool _isHovered = false;

        public Color ButtonColor
        {
            get => _targetColor;
            set { _targetColor = value; if (!_animTimer.Enabled) _animTimer.Start(); }
        }

        public CustomColorButton()
        {
            DoubleBuffered = true;
            Size = new Size(60, 24);
            Cursor = Cursors.Hand;
            _animTimer = new System.Windows.Forms.Timer { Interval = 1 };
            _animTimer.Tick += (s, e) =>
            {
                bool needsUpdate = false;
                float targetAlpha = _isHovered ? 40f : 0f;
                float easingFactor = 0.05f;
                _hoverAlpha += (targetAlpha - _hoverAlpha) * easingFactor;
                if (_currentColor != _targetColor)
                {
                    int r = (int)(_currentColor.R + (_targetColor.R - _currentColor.R) * 0.1f);
                    int g = (int)(_currentColor.G + (_targetColor.G - _currentColor.G) * 0.1f);
                    int b = (int)(_currentColor.B + (_targetColor.B - _currentColor.B) * 0.1f);
                    if (Math.Abs(_targetColor.R - _currentColor.R) <= 2) r = _targetColor.R;
                    if (Math.Abs(_targetColor.G - _currentColor.G) <= 2) g = _targetColor.G;
                    if (Math.Abs(_targetColor.B - _currentColor.B) <= 2) b = _targetColor.B;
                    _currentColor = Color.FromArgb(r, g, b);
                    needsUpdate = true;
                }
                if (needsUpdate) Invalidate();
                else if (Math.Abs(targetAlpha - _hoverAlpha) < 0.5f && _currentColor == _targetColor) _animTimer.Stop();
            };
        }

        public void SetInitialColor(Color c)
        {
            _targetColor = c;
            _currentColor = c;
            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e) { _isHovered = true; _animTimer.Start(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _isHovered = false; _animTimer.Start(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.None;
            e.Graphics.Clear(Parent.BackColor);
            using (SolidBrush brush = new SolidBrush(_currentColor))
                e.Graphics.FillRectangle(brush, ClientRectangle);
            if (_hoverAlpha > 0)
            {
                using (SolidBrush hoverBrush = new SolidBrush(Color.FromArgb((int)_hoverAlpha, 255, 255, 255)))
                    e.Graphics.FillRectangle(hoverBrush, ClientRectangle);
            }
            using (Pen border = new Pen(Color.FromArgb(70, 70, 70), 1))
                e.Graphics.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
            double luminance = (0.299 * _currentColor.R + 0.587 * _currentColor.G + 0.114 * _currentColor.B) / 255;
            Color textColor = luminance > 0.5 ? Color.Black : Color.White;
            TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, textColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }

    class CustomComboBox : Control
    {
        public List<string> Items { get; set; } = new List<string>();
        private int _selectedIndex = 0;
        public int SelectedIndex
        {
            get => _selectedIndex;
            set { _selectedIndex = value; Invalidate(); SelectedIndexChanged?.Invoke(this, EventArgs.Empty); }
        }
        public Color AccentColor { get; set; } = Color.FromArgb(145, 100, 255);
        public event EventHandler SelectedIndexChanged;
        private bool _isDropped = false;

        public CustomComboBox() { Size = new Size(110, 24); DoubleBuffered = true; Cursor = Cursors.Hand; }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            if (_isDropped) return;
            ShowDropdown();
        }

        private void ShowDropdown()
        {
            _isDropped = true;
            Form dropdown = new Form
            {
                FormBorderStyle = FormBorderStyle.None,
                StartPosition = FormStartPosition.Manual,
                ShowInTaskbar = false,
                BackColor = Color.FromArgb(25, 25, 25),
                Size = new Size(Width, Items.Count * 24 + 2),
                TopMost = true
            };
            Point screenPos = Parent.PointToScreen(Location);
            dropdown.Location = new Point(screenPos.X, screenPos.Y + Height);
            ListBox lb = new ListBox
            {
                BackColor = Color.FromArgb(25, 25, 25),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.None,
                Dock = DockStyle.Fill,
                Font = this.Font,
                ItemHeight = 24,
                DrawMode = DrawMode.OwnerDrawFixed
            };
            foreach (var item in Items) lb.Items.Add(item);
            lb.SelectedIndex = _selectedIndex;
            lb.DrawItem += (s, e) =>
            {
                if (e.Index < 0) return;
                bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
                e.Graphics.FillRectangle(new SolidBrush(selected ? AccentColor : lb.BackColor), e.Bounds);
                TextRenderer.DrawText(e.Graphics, lb.Items[e.Index].ToString(), lb.Font, e.Bounds, Color.White, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
            };
            lb.Click += (s, e) =>
            {
                if (lb.SelectedIndex != -1) SelectedIndex = lb.SelectedIndex;
                dropdown.Close();
            };
            dropdown.Deactivate += (s, e) => dropdown.Close();
            dropdown.FormClosed += (s, e) => _isDropped = false;
            dropdown.Controls.Add(lb);
            dropdown.Show();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.Clear(Parent.BackColor);
            using (SolidBrush bg = new SolidBrush(Color.FromArgb(15, 15, 15)))
                e.Graphics.FillRectangle(bg, ClientRectangle);
            using (Pen border = new Pen(Color.FromArgb(50, 50, 50), 1))
                e.Graphics.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
            string text = Items.Count > 0 && SelectedIndex >= 0 && SelectedIndex < Items.Count ? Items[SelectedIndex] : "";
            TextRenderer.DrawText(e.Graphics, text, Font, new Rectangle(5, 0, Width - 20, Height), ForeColor, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
            using (SolidBrush arrowBrush = new SolidBrush(Color.Gray))
            {
                Point[] arrow = new Point[] {
                    new Point(Width - 15, Height / 2 - 2),
                    new Point(Width - 5, Height / 2 - 2),
                    new Point(Width - 10, Height / 2 + 3)
                };
                e.Graphics.FillPolygon(arrowBrush, arrow);
            }
        }
    }

    class CustomColorPickerForm : Form
    {
        public Color SelectedColor { get; private set; }
        private Panel pnlPreview;
        private CustomSlider sldR, sldG, sldB;
        private Label lblR, lblG, lblB;
        private Color _accent = Color.FromArgb(145, 100, 255);

        [DllImport("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();
        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HT_CAPTION = 0x2;

        public CustomColorPickerForm(Color initialColor)
        {
            SelectedColor = initialColor;
            try { this.Icon = new Icon(Assembly.GetExecutingAssembly().GetManifestResourceStream("Hytux.Hytux.ico")); } catch { }
            Size = new Size(240, 310);
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.FromArgb(20, 20, 20);
            TopMost = true;

            var topBar = new Panel { Size = new Size(240, 30), Location = new Point(0, 0), BackColor = Color.FromArgb(30, 30, 30) };
            var title = new Label { Text = "PICK COLOR", ForeColor = _accent, Font = new Font("Segoe UI", 9F, FontStyle.Bold), AutoSize = true, Location = new Point(8, 7) };
            topBar.Controls.Add(title);
            topBar.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) { ReleaseCapture(); SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0); } };
            title.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) { ReleaseCapture(); SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0); } };
            Controls.Add(topBar);

            pnlPreview = new Panel { Size = new Size(200, 60), Location = new Point(20, 45), BackColor = initialColor };
            Controls.Add(pnlPreview);

            Font f = new Font("Segoe UI", 9F);
            lblR = new Label { Text = "R: " + initialColor.R, ForeColor = Color.White, Font = f, AutoSize = true, Location = new Point(20, 115) };
            sldR = new CustomSlider { Minimum = 0, Maximum = 255, Value = initialColor.R, Location = new Point(70, 115), Size = new Size(150, 20), AccentColor = Color.Red };
            lblG = new Label { Text = "G: " + initialColor.G, ForeColor = Color.White, Font = f, AutoSize = true, Location = new Point(20, 155) };
            sldG = new CustomSlider { Minimum = 0, Maximum = 255, Value = initialColor.G, Location = new Point(70, 155), Size = new Size(150, 20), AccentColor = Color.LimeGreen };
            lblB = new Label { Text = "B: " + initialColor.B, ForeColor = Color.White, Font = f, AutoSize = true, Location = new Point(20, 195) };
            sldB = new CustomSlider { Minimum = 0, Maximum = 255, Value = initialColor.B, Location = new Point(70, 195), Size = new Size(150, 20), AccentColor = Color.DodgerBlue };

            EventHandler updateColor = (s, e) =>
            {
                SelectedColor = Color.FromArgb(sldR.Value, sldG.Value, sldB.Value);
                pnlPreview.BackColor = SelectedColor;
                lblR.Text = "R: " + sldR.Value;
                lblG.Text = "G: " + sldG.Value;
                lblB.Text = "B: " + sldB.Value;
            };

            sldR.Scroll += updateColor;
            sldG.Scroll += updateColor;
            sldB.Scroll += updateColor;

            Controls.Add(lblR); Controls.Add(sldR);
            Controls.Add(lblG); Controls.Add(sldG);
            Controls.Add(lblB); Controls.Add(sldB);

            var btnOk = new Button { Text = "APPLY", Size = new Size(95, 30), Location = new Point(20, 250), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(30, 30, 30), ForeColor = Color.White, Font = f };
            btnOk.FlatAppearance.BorderSize = 1; btnOk.FlatAppearance.BorderColor = _accent;
            btnOk.Click += (s, e) => { DialogResult = DialogResult.OK; Close(); };
            var btnCancel = new Button { Text = "CANCEL", Size = new Size(95, 30), Location = new Point(125, 250), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(30, 30, 30), ForeColor = Color.White, Font = f };
            btnCancel.FlatAppearance.BorderSize = 1; btnCancel.FlatAppearance.BorderColor = Color.FromArgb(70, 70, 70);
            btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

            Controls.Add(btnOk);
            Controls.Add(btnCancel);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (Pen border = new Pen(Color.FromArgb(50, 50, 50), 2))
                e.Graphics.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
        }
    }

    class Memory
    {
        private const string ProcessName = "RobloxPlayerBeta";
        private const string LocalOffsetsFile = "Offsets.json";
        private const uint ProcessAllAccess = 0x001F0FFF;

        [DllImport("kernel32.dll")]
        static extern IntPtr OpenProcess(uint Access, bool Inherit, int Pid);
        [DllImport("kernel32.dll")]
        static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr Address, Byte[] Buffer, int Size, out int BytesRead);
        [DllImport("kernel32.dll")]
        static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr Address, Byte[] Buffer, int Size, out int BytesWritten);

        private IntPtr Handler;
        private IntPtr BaseAddress;
        public Dictionary<string, string> Offsets;
        public int ProcessId { get; private set; }

        public struct Vector3 { public float X, Y, Z; }
        public struct Vector2 { public float X, Y; }
        public struct Vector4 { public float X, Y, Z, W; }

        [StructLayout(LayoutKind.Sequential)]
        public struct Matrix4x4
        {
            public float M11, M12, M13, M14;
            public float M21, M22, M23, M24;
            public float M31, M32, M33, M34;
            public float M41, M42, M43, M44;
        }

        public Memory()
        {
            Offsets = LoadOffsets();
            FindProcess();
        }

        private Dictionary<string, string> LoadOffsets()
        {
            try
            {
                if (!File.Exists(LocalOffsetsFile))
                {
                    MessageBox.Show($"File {LocalOffsetsFile} not found in program folder! ", "Error loading offsets", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Environment.Exit(1);
                }
                var json = File.ReadAllText(LocalOffsetsFile);
                var dict = new Dictionary<string, string>();
                using (var doc = JsonDocument.Parse(json))
                {
                    if (doc.RootElement.TryGetProperty("Offsets", out var OffsetsElement) && OffsetsElement.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var Element in OffsetsElement.EnumerateObject())
                            dict[Element.Name] = Element.Value.GetString();
                    }
                    else if (doc.RootElement.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var Element in doc.RootElement.EnumerateObject())
                            if (Element.Value.ValueKind == JsonValueKind.String) dict[Element.Name] = Element.Value.GetString();
                    }
                    else
                    {
                        MessageBox.Show("The JSON structure is invalid!", "Parsing error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        Environment.Exit(1);
                    }
                }
                return dict;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred while loading offsets:\n{ex.Message}", "Critical error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Environment.Exit(1);
                return new Dictionary<string, string>();
            }
        }

        private void FindProcess()
        {
            bool messaged = false;
            while (true)
            {
                var Processes = Process.GetProcessesByName(ProcessName);
                if (Processes.Length > 0)
                {
                    Handler = OpenProcess(ProcessAllAccess, false, Processes[0].Id);
                    if (Handler != IntPtr.Zero)
                    {
                        BaseAddress = Processes[0].MainModule.BaseAddress;
                        ProcessId = Processes[0].Id;
                        return;
                    }
                }
                else if (!messaged)
                {
                    MessageBox.Show("Waiting for RobloxPlayerBeta. Launch the game...", "Searching for process", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    messaged = true;
                }
                Thread.Sleep(2000);
            }
        }

        public Byte[] Read(IntPtr Address, int Size)
        {
            if (Handler == IntPtr.Zero) return new Byte[Size];
            Byte[] Buffer = new Byte[Size];
            if (!ReadProcessMemory(Handler, Address, Buffer, Size, out int BytesRead) || BytesRead != Size)
                return new Byte[Size];
            return Buffer;
        }

        public void WriteByte(IntPtr Address, byte Value)
        {
            if (Handler == IntPtr.Zero) return;
            Byte[] Buffer = new Byte[] { Value };
            WriteProcessMemory(Handler, Address, Buffer, 1, out _);
        }

        public void WriteFloat(IntPtr Address, float Value)
        {
            if (Handler == IntPtr.Zero) return;
            Byte[] Buffer = BitConverter.GetBytes(Value);
            WriteProcessMemory(Handler, Address, Buffer, Buffer.Length, out _);
        }

        public void WriteVector3(IntPtr Address, Vector3 Value)
        {
            if (Handler == IntPtr.Zero) return;
            Byte[] Buffer = new Byte[12];
            System.Buffer.BlockCopy(BitConverter.GetBytes(Value.X), 0, Buffer, 0, 4);
            System.Buffer.BlockCopy(BitConverter.GetBytes(Value.Y), 0, Buffer, 4, 4);
            System.Buffer.BlockCopy(BitConverter.GetBytes(Value.Z), 0, Buffer, 8, 4);
            WriteProcessMemory(Handler, Address, Buffer, 12, out _);
        }

        public long ReadPointer(IntPtr Address)
        {
            Byte[] Buffer = Read(Address, 8);
            if (Buffer.Length < 8) return 0;
            return BitConverter.ToInt64(Buffer, 0);
        }

        public long ReadLong(IntPtr Address)
        {
            Byte[] Buffer = Read(Address, 8);
            if (Buffer.Length < 8) return 0;
            return BitConverter.ToInt64(Buffer, 0);
        }

        public int ReadInt(IntPtr Address)
        {
            Byte[] Buffer = Read(Address, 4);
            if (Buffer.Length < 4) return 0;
            return BitConverter.ToInt32(Buffer, 0);
        }

        public float ReadFloat(IntPtr Address)
        {
            Byte[] Buffer = Read(Address, 4);
            if (Buffer.Length < 4) return 0f;
            return BitConverter.ToSingle(Buffer, 0);
        }

        private string ReadString(IntPtr Address)
        {
            if (Address == IntPtr.Zero) return "";
            int Length = ReadInt(new IntPtr(Address.ToInt64() + 0x18));
            if (Length <= 0 || Length > 1000) return "";
            IntPtr StringDataPointer = Address;
            if (Length >= 16)
            {
                StringDataPointer = new IntPtr(ReadPointer(Address));
                if (StringDataPointer == IntPtr.Zero) return "";
            }
            if (StringDataPointer == IntPtr.Zero) return "";
            string str = "";
            for (int i = 0; i < Length; i++)
            {
                Byte CharacterByte = Read(new IntPtr(StringDataPointer.ToInt64() + i), 1)[0];
                if (CharacterByte == 0) break;
                str += (char)CharacterByte;
            }
            return str;
        }

        public string GetName(long Instance)
        {
            if (Instance == 0) return "";
            long NamePointer = ReadPointer(new IntPtr(Instance + Convert.ToInt32(Offsets["Name"], 16)));
            return ReadString(new IntPtr(NamePointer));
        }

        public string GetClass(long Instance)
        {
            if (Instance == 0) return "";
            long Descriptor = ReadPointer(new IntPtr(Instance + Convert.ToInt32(Offsets["ClassDescriptor"], 16)));
            if (Descriptor == 0) return "";
            long NamePointer = ReadPointer(new IntPtr(Descriptor + Convert.ToInt32(Offsets["ClassDescriptorToClassName"], 16)));
            return ReadString(new IntPtr(NamePointer));
        }

        public List<long> GetChildren(long Instance)
        {
            var ChildrenList = new List<long>();
            if (Instance == 0) return ChildrenList;
            long ChildrenPointer = ReadPointer(new IntPtr(Instance + Convert.ToInt32(Offsets["Children"], 16)));
            if (ChildrenPointer == 0) return ChildrenList;
            long EndPointer = ReadPointer(new IntPtr(ChildrenPointer + Convert.ToInt32(Offsets["ChildrenEnd"], 16)));
            if (EndPointer == 0) return ChildrenList;
            long CurrentPointer = ReadPointer(new IntPtr(ChildrenPointer));
            int _l = 0;
            while (CurrentPointer < EndPointer && CurrentPointer != 0 && _l < 15000)
            {
                long ChildInstance = ReadPointer(new IntPtr(CurrentPointer));
                if (ChildInstance != 0) ChildrenList.Add(ChildInstance);
                CurrentPointer += 0x10;
                _l++;
            }
            return ChildrenList;
        }

        public long FindFirstChildByName(long ParentInstance, string Name)
        {
            if (ParentInstance == 0) return 0;
            foreach (var ChildInstance in GetChildren(ParentInstance))
                if (GetName(ChildInstance) == Name) return ChildInstance;
            return 0;
        }

        public long FindFirstClass(long ParentInstance, string ClassName)
        {
            if (ParentInstance == 0) return 0;
            foreach (var ChildInstance in GetChildren(ParentInstance))
                if (GetClass(ChildInstance) == ClassName) return ChildInstance;
            return 0;
        }

        public long GetDataModel()
        {
            if (BaseAddress == IntPtr.Zero) return 0;
            long FakeDataModelPointer = ReadPointer(new IntPtr(BaseAddress.ToInt64() + Convert.ToInt32(Offsets["FakeDataModelPointer"], 16)));
            if (FakeDataModelPointer == 0) return 0;
            return ReadPointer(new IntPtr(FakeDataModelPointer + Convert.ToInt32(Offsets["FakeDataModelToDataModel"], 16)));
        }

        public Matrix4x4 ReadViewMatrix()
        {
            if (BaseAddress == IntPtr.Zero) return new Matrix4x4();
            long VisualEnginePointer = ReadPointer(new IntPtr(BaseAddress.ToInt64() + Convert.ToInt64(Offsets["VisualEnginePointer"], 16)));
            if (VisualEnginePointer == 0) return new Matrix4x4();
            long Address = VisualEnginePointer + Convert.ToInt64(Offsets["ViewMatrix"], 16);
            Byte[] Buffer = Read(new IntPtr(Address), 64);
            if (Buffer.Length < 64) return new Matrix4x4();
            GCHandle Handle = GCHandle.Alloc(Buffer, GCHandleType.Pinned);
            Matrix4x4 Matrix = (Matrix4x4)Marshal.PtrToStructure(Handle.AddrOfPinnedObject(), typeof(Matrix4x4));
            Handle.Free();
            return Matrix;
        }

        public bool WorldToScreen(Vector3 WorldPosition, Matrix4x4 ViewMatrix, int ScreenWidth, int ScreenHeight, out Vector2 ScreenPos)
        {
            ScreenPos = new Vector2 { X = 0, Y = 0 };
            float[] MatrixArray = new float[] {
                ViewMatrix.M11, ViewMatrix.M12, ViewMatrix.M13, ViewMatrix.M14,
                ViewMatrix.M21, ViewMatrix.M22, ViewMatrix.M23, ViewMatrix.M24,
                ViewMatrix.M31, ViewMatrix.M32, ViewMatrix.M33, ViewMatrix.M34,
                ViewMatrix.M41, ViewMatrix.M42, ViewMatrix.M43, ViewMatrix.M44
            };
            float W = WorldPosition.X * MatrixArray[12] + WorldPosition.Y * MatrixArray[13] + WorldPosition.Z * MatrixArray[14] + MatrixArray[15];
            if (W < 0.1f) return false;
            float X = WorldPosition.X * MatrixArray[0] + WorldPosition.Y * MatrixArray[1] + WorldPosition.Z * MatrixArray[2] + MatrixArray[3];
            float Y = WorldPosition.X * MatrixArray[4] + WorldPosition.Y * MatrixArray[5] + WorldPosition.Z * MatrixArray[6] + MatrixArray[7];
            float NormalizedDeviceX = X / W;
            float NormalizedDeviceY = Y / W;
            ScreenPos.X = (ScreenWidth / 2f * NormalizedDeviceX) + (ScreenWidth / 2f);
            ScreenPos.Y = -(ScreenHeight / 2f * NormalizedDeviceY) + (ScreenHeight / 2f);
            return true;
        }
    }

    class OverlayForm : Form
    {
        public Action<Graphics> DrawAction;

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x00000020 | 0x00080000;
                return cp;
            }
        }

        public OverlayForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            TopMost = true;
            BackColor = Color.Black;
            TransparencyKey = Color.Black;
            WindowState = FormWindowState.Maximized;
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            try { this.Icon = new Icon(Assembly.GetExecutingAssembly().GetManifestResourceStream("Hytux.Hytux.ico")); } catch { }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            DrawAction?.Invoke(e.Graphics);
        }
    }

    class Hytux
    {
        struct ObjectCacheItem
        {
            public long Instance;
            public string Name;
            public string Class;
            public long Primitive;
        }
        private static List<ObjectCacheItem> CachedObjects = new List<ObjectCacheItem>();
        private static readonly object CacheLock = new object();

        private static readonly HashSet<string> ValidBodyParts = new HashSet<string>
        {
            "Head", "Torso", "Left Arm", "Right Arm", "Left Leg", "Right Leg",
            "UpperTorso", "LowerTorso", "LeftUpperArm", "LeftLowerArm", "LeftHand",
            "RightUpperArm", "RightLowerArm", "RightHand", "LeftUpperLeg",
            "LeftLowerLeg", "LeftFoot", "RightUpperLeg", "RightLowerLeg", "RightFoot",
            "HumanoidRootPart"
        };
        private static bool EspEnabled = true;
        private static bool AimbotEnabled = true;
        private static bool TriggerbotEnabled = false;
        private static bool TargetEnemies = true;
        private static bool TargetTeammates = false;
        private static bool TargetNeutrals = true;

        private static bool HitboxExpanderEnabled = false;
        private static int HitboxSize = 5;
        private static Keys HitboxKey = Keys.F4;
        private static bool WasHitboxKeyPressed = false;
        private static Dictionary<long, Memory.Vector3> OriginalSizes = new Dictionary<long, Memory.Vector3>();

        private static bool ObjectEspEnabled = false;
        private static string ObjectEspName = "";
        private static AnimatedColor ObjectColor = new AnimatedColor(Color.FromArgb(255, 215, 0));

        private static bool AutoTpToObjects = false;
        private static int AutoTpDelay = 500;
        private static int ObjectScanDelay = 1000;

        private static bool FlyEnabled = false;
        private static int FlyMode = 0;
        private static bool WasHoverEnabled = false;
        private static float OriginalHipHeight = 2.0f;
        private static int FlySpeed = 50;
        private static Keys FlyKey = Keys.F1;
        private static bool WasFlyKeyPressed = false;
        private static int TpMode = 0;
        private static Keys TpKey = Keys.F2;
        private static int TpDistance = 50;
        private static bool WasTpKeyPressed = false;
        private static bool NoclipEnabled = false;
        private static Keys NoclipKey = Keys.F3;
        private static bool WasNoclipKeyPressed = false;
        private static bool DrawBox = true;
        private static bool DrawSkeleton = false;
        private static bool DrawLine = false;
        private static bool ShowNames = true;
        private static bool ShowDistance = false;
        private static bool ShowHealth = false;
        private static bool HealthTextMode = false;
        private static bool ShowAvatar = false;
        private static int AvatarPosition = 0;
        private static AnimatedColor EnemyColor = new AnimatedColor(Color.FromArgb(90, 40, 150));
        private static AnimatedColor TeamColor = new AnimatedColor(Color.FromArgb(180, 130, 255));
        private static AnimatedColor NeutralColor = new AnimatedColor(Color.FromArgb(145, 100, 255));
        private static AnimatedColor FovColor = new AnimatedColor(Color.FromArgb(145, 100, 255));
        private static bool UseCustomNameColor = false;
        private static AnimatedColor CustomNameColor = new AnimatedColor(Color.FromArgb(220, 200, 255));
        private static bool UseCustomDistColor = false;
        private static AnimatedColor CustomDistColor = new AnimatedColor(Color.FromArgb(220, 200, 255));
        private static bool UseCustomHpColor = false;
        private static AnimatedColor CustomHpColor = new AnimatedColor(Color.FromArgb(220, 200, 255));
        private static float AimFov = 100f;
        private static float AimSmoothing = 1.0f;
        private static int MaxMove = 800;
        private static bool AimAtHead = true;
        private static bool PredictMovement = true;
        private static DateTime LastTriggerTime = DateTime.MinValue;
        private static Keys AimKey = Keys.RButton;
        private static bool AimToggleMode = false;
        private static bool IsAimbotLocked = false;
        private static bool WasAimKeyPressed = false;
        private static bool IsAimKeyDown = false;
        private static Dictionary<long, Image> AvatarCache = new Dictionary<long, Image>();
        private static HashSet<long> PendingAvatars = new HashSet<long>();
        private static HttpClient httpClient = new HttpClient();

        [DllImport("User32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]
        public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint cButtons, UIntPtr dwExtraInfo);
        [DllImport("User32.dll")]
        static extern short GetAsyncKeyState(Keys vKey);
        [DllImport("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
        private const uint MOUSEEVENTF_MOVE = 0x0001;
        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;
        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HT_CAPTION = 0x2;

        private static async Task FetchAvatarAsync(long UserId)
        {
            try
            {
                if (UserId <= 0 || UserId > 99999999999) return;
                string cacheFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Avatars");
                if (!Directory.Exists(cacheFolder)) Directory.CreateDirectory(cacheFolder);
                string filePath = Path.Combine(cacheFolder, $"{UserId}.png");
                if (File.Exists(filePath))
                {
                    var img = Image.FromFile(filePath);
                    lock (AvatarCache) { AvatarCache[UserId] = img; }
                    return;
                }
                string url = $"https://thumbnails.roblox.com/v1/users/avatar-headshot?userIds={UserId}&size=48x48&format=Png&isCircular=false";
                var response = await httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode) return;
                var json = await response.Content.ReadAsStringAsync();
                using (var doc = JsonDocument.Parse(json))
                {
                    var dataElement = doc.RootElement.GetProperty("data");
                    if (dataElement.GetArrayLength() > 0)
                    {
                        var playerInfo = dataElement[0];
                        if (playerInfo.GetProperty("state").GetString() == "Completed")
                        {
                            string imgUrl = playerInfo.GetProperty("imageUrl").GetString();
                            var imgBytes = await httpClient.GetByteArrayAsync(imgUrl);
                            File.WriteAllBytes(filePath, imgBytes);
                            var img = Image.FromStream(new MemoryStream(imgBytes));
                            lock (AvatarCache) { AvatarCache[UserId] = img; }
                        }
                    }
                }
            }
            catch { }
        }

        private static void RestoreDefaults(Memory memory)
        {
            try
            {
                if (memory != null && memory.Offsets != null && memory.Offsets.ContainsKey("PartSize"))
                {
                    int sizeOffset = Convert.ToInt32(memory.Offsets["PartSize"], 16);
                    lock (OriginalSizes)
                    {
                        foreach (var kvp in OriginalSizes)
                        {
                            memory.WriteVector3(new IntPtr(kvp.Key + sizeOffset), kvp.Value);
                        }
                        OriginalSizes.Clear();
                    }
                }
            }
            catch { }

            if (!WasHoverEnabled) return;
            try
            {
                long DataModel = memory.GetDataModel();
                if (DataModel == 0) return;
                long PlayersObject = memory.FindFirstClass(DataModel, "Players");
                if (PlayersObject == 0) return;
                long LocalPlayerPointer = memory.ReadPointer(new IntPtr(PlayersObject + Convert.ToInt32(memory.Offsets["LocalPlayer"], 16)));
                if (LocalPlayerPointer == 0)
                {
                    var PlayerList = memory.GetChildren(PlayersObject);
                    if (PlayerList.Count > 0) LocalPlayerPointer = PlayerList[0];
                }
                if (LocalPlayerPointer != 0)
                {
                    long LocalCharacter = memory.ReadPointer(new IntPtr(LocalPlayerPointer + Convert.ToInt32(memory.Offsets["ModelInstance"], 16)));
                    if (LocalCharacter != 0)
                    {
                        long LocalHumanoid = memory.FindFirstClass(LocalCharacter, "Humanoid");
                        long LocalRoot = memory.FindFirstChildByName(LocalCharacter, "HumanoidRootPart");
                        if (LocalHumanoid != 0)
                        {
                            long HipHeightOffset = Convert.ToInt32(memory.Offsets["HipHeight"], 16);
                            memory.WriteFloat(new IntPtr(LocalHumanoid + HipHeightOffset), OriginalHipHeight);
                        }
                        if (LocalRoot != 0)
                        {
                            long LocalPrimitiveRootPointer = memory.ReadPointer(new IntPtr(LocalRoot + Convert.ToInt32(memory.Offsets["Primitive"], 16)));
                            if (LocalPrimitiveRootPointer != 0)
                            {
                                long VelocityOffset = Convert.ToInt32(memory.Offsets["Velocity"], 16);
                                memory.WriteVector3(new IntPtr(LocalPrimitiveRootPointer + VelocityOffset), new Memory.Vector3 { X = 0, Y = 0, Z = 0 });
                            }
                        }
                    }
                }
            }
            catch { }
        }

        [STAThread]
        static void Main()
        {
            var Memory = new Memory();
            var OverlayForm = new OverlayForm();
            AppDomain.CurrentDomain.ProcessExit += (s, e) => RestoreDefaults(Memory);
            OverlayForm.FormClosing += (s, e) => RestoreDefaults(Memory);

            CustomToggle chkFlyInstance = null;
            CustomToggle chkNoclipInstance = null;
            CustomToggle chkHitboxInstance = null;

            var HitboxThread = new Thread(() =>
            {
                while (true)
                {
                    if (HitboxExpanderEnabled || OriginalSizes.Count > 0)
                    {
                        try
                        {
                            long DataModel = Memory.GetDataModel();
                            if (DataModel != 0)
                            {
                                long Players = Memory.FindFirstClass(DataModel, "Players");
                                if (Players != 0)
                                {
                                    long LocalPlayer = Memory.ReadPointer(new IntPtr(Players + Convert.ToInt32(Memory.Offsets["LocalPlayer"], 16)));
                                    var playerList = Memory.GetChildren(Players);

                                    if (LocalPlayer == 0 && playerList.Count > 0)
                                    {
                                        LocalPlayer = playerList[0];
                                    }

                                    long LocalTeamId = 0;
                                    long LocalCharacter = 0;

                                    if (LocalPlayer != 0)
                                    {
                                        LocalTeamId = Memory.ReadPointer(new IntPtr(LocalPlayer + Convert.ToInt32(Memory.Offsets["Team"], 16)));
                                        LocalCharacter = Memory.ReadPointer(new IntPtr(LocalPlayer + Convert.ToInt32(Memory.Offsets["ModelInstance"], 16)));
                                    }

                                    int sizeOffset = Convert.ToInt32(Memory.Offsets["PartSize"], 16);
                                    HashSet<long> currentFramePrimitives = new HashSet<long>();

                                    foreach (var player in playerList)
                                    {
                                        if (player == LocalPlayer) continue;

                                        long CurrentTeamId = Memory.ReadPointer(new IntPtr(player + Convert.ToInt32(Memory.Offsets["Team"], 16)));
                                        bool isNeutral = CurrentTeamId == 0;
                                        bool isTeam = CurrentTeamId != 0 && CurrentTeamId == LocalTeamId;
                                        bool isEnemy = CurrentTeamId != 0 && CurrentTeamId != LocalTeamId;

                                        if (isNeutral && !TargetNeutrals) continue;
                                        if (isTeam && !TargetTeammates) continue;
                                        if (isEnemy && !TargetEnemies) continue;

                                        long Character = Memory.ReadPointer(new IntPtr(player + Convert.ToInt32(Memory.Offsets["ModelInstance"], 16)));

                                        if (Character == 0 || Character == LocalCharacter) continue;

                                        long HeadPart = Memory.FindFirstChildByName(Character, "Head");
                                        if (HeadPart == 0) continue;

                                        long Prim = Memory.ReadPointer(new IntPtr(HeadPart + Convert.ToInt32(Memory.Offsets["Primitive"], 16)));
                                        if (Prim == 0) continue;

                                        currentFramePrimitives.Add(Prim);

                                        if (HitboxExpanderEnabled)
                                        {
                                            lock (OriginalSizes)
                                            {
                                                if (!OriginalSizes.ContainsKey(Prim))
                                                {
                                                    float ox = Memory.ReadFloat(new IntPtr(Prim + sizeOffset));
                                                    float oy = Memory.ReadFloat(new IntPtr(Prim + sizeOffset + 4));
                                                    float oz = Memory.ReadFloat(new IntPtr(Prim + sizeOffset + 8));
                                                    OriginalSizes[Prim] = new Memory.Vector3 { X = ox, Y = oy, Z = oz };
                                                }
                                            }

                                            float safeHitboxSize = Math.Min(HitboxSize, 200f);
                                            Memory.WriteVector3(new IntPtr(Prim + sizeOffset), new Memory.Vector3 { X = safeHitboxSize, Y = safeHitboxSize, Z = safeHitboxSize });
                                        }
                                    }

                                    lock (OriginalSizes)
                                    {
                                        var toRemove = new List<long>();
                                        foreach (var kvp in OriginalSizes)
                                        {
                                            if (!HitboxExpanderEnabled || !currentFramePrimitives.Contains(kvp.Key))
                                            {
                                                Memory.WriteVector3(new IntPtr(kvp.Key + sizeOffset), kvp.Value);
                                                toRemove.Add(kvp.Key);
                                            }
                                        }
                                        foreach (var key in toRemove) OriginalSizes.Remove(key);
                                    }
                                }
                            }
                        }
                        catch { }
                    }
                    Thread.Sleep(5);
                }
            });
            HitboxThread.IsBackground = true;
            HitboxThread.Start();

            var NoclipThread = new Thread(() =>
            {
                long cachedCharacter = 0;
                List<long> cachedPrimitives = new List<long>();
                int refreshTicks = 0;
                while (true)
                {
                    if (NoclipEnabled)
                    {
                        try
                        {
                            if (refreshTicks >= 50 || cachedPrimitives.Count == 0)
                            {
                                long DataModel = Memory.GetDataModel();
                                long Players = Memory.FindFirstClass(DataModel, "Players");
                                long LocalPlayer = Memory.ReadPointer(new IntPtr(Players + Convert.ToInt32(Memory.Offsets["LocalPlayer"], 16)));
                                long Character = Memory.ReadPointer(new IntPtr(LocalPlayer + Convert.ToInt32(Memory.Offsets["ModelInstance"], 16)));
                                if (Character != 0 && Character != cachedCharacter)
                                {
                                    cachedCharacter = Character;
                                    cachedPrimitives.Clear();
                                    var children = Memory.GetChildren(Character);
                                    foreach (var child in children)
                                    {
                                        string name = Memory.GetName(child);
                                        if (name.Contains("Torso") || name.Contains("Root") || name == "Head" || name.Contains("Leg") || name.Contains("Arm"))
                                        {
                                            long prim = Memory.ReadPointer(new IntPtr(child + Convert.ToInt32(Memory.Offsets["Primitive"], 16)));
                                            if (prim != 0) cachedPrimitives.Add(prim);
                                        }
                                    }
                                }
                                refreshTicks = 0;
                            }
                            if (cachedPrimitives.Count > 0)
                            {
                                int canCollideOffset = Convert.ToInt32(Memory.Offsets["CanCollide"], 16);
                                byte canCollideMask = Convert.ToByte(Memory.Offsets["CanCollideMask"], 16);
                                foreach (long prim in cachedPrimitives)
                                {
                                    byte[] flags = Memory.Read(new IntPtr(prim + canCollideOffset), 1);
                                    if (flags.Length > 0 && (flags[0] & canCollideMask) != 0)
                                    {
                                        byte newFlags = (byte)(flags[0] & ~canCollideMask);
                                        Memory.WriteByte(new IntPtr(prim + canCollideOffset), newFlags);
                                    }
                                }
                            }
                        }
                        catch { }
                    }
                    else
                    {
                        cachedCharacter = 0;
                        cachedPrimitives.Clear();
                    }
                    Thread.Sleep(2);
                    refreshTicks++;
                }
            });
            NoclipThread.IsBackground = true;
            NoclipThread.Start();

            var ObjectScannerThread = new Thread(() =>
            {
                while (true)
                {
                    if ((ObjectEspEnabled || AutoTpToObjects) && !string.IsNullOrEmpty(ObjectEspName))
                    {
                        try
                        {
                            long DataModel = Memory.GetDataModel();
                            if (DataModel != 0)
                            {
                                long Workspace = Memory.FindFirstClass(DataModel, "Workspace");
                                if (Workspace != 0)
                                {
                                    var newList = new List<ObjectCacheItem>();

                                    Action<long, int> ScanObjects = null;
                                    ScanObjects = (parentObj, depth) =>
                                    {
                                        if (depth > 8) return;

                                        var children = Memory.GetChildren(parentObj);
                                        foreach (var child in children)
                                        {
                                            string cName = Memory.GetName(child);
                                            if (string.IsNullOrEmpty(cName)) continue;

                                            string cClass = Memory.GetClass(child);

                                            if (cClass == "Terrain" || cClass == "Camera" || cClass.Contains("Script") || cClass == "ForceField") continue;

                                            bool isMatch = (ObjectEspName == "*") || (cName.IndexOf(ObjectEspName, StringComparison.OrdinalIgnoreCase) >= 0);

                                            if (isMatch)
                                            {
                                                long prim = Memory.ReadPointer(new IntPtr(child + Convert.ToInt32(Memory.Offsets["Primitive"], 16)));

                                                if (prim == 0 && (cClass == "Model" || cClass == "Tool" || cClass == "Accessory"))
                                                {
                                                    var subChildren = Memory.GetChildren(child);
                                                    foreach (var sub in subChildren)
                                                    {
                                                        long subPrim = Memory.ReadPointer(new IntPtr(sub + Convert.ToInt32(Memory.Offsets["Primitive"], 16)));
                                                        if (subPrim != 0) { prim = subPrim; break; }
                                                    }
                                                }

                                                if (prim != 0)
                                                {
                                                    newList.Add(new ObjectCacheItem { Instance = child, Name = cName, Class = cClass, Primitive = prim });
                                                }
                                            }

                                            if (cClass == "Folder" || cClass == "Model" || cClass == "ModelInstance" || cClass == "Tool" || cClass == "Workspace")
                                            {
                                                ScanObjects(child, depth + 1);
                                            }
                                        }
                                    };
                                    ScanObjects(Workspace, 0);

                                    lock (CacheLock)
                                    {
                                        CachedObjects = newList;
                                    }
                                }
                            }
                        }
                        catch { }
                    }
                    else
                    {
                        lock (CacheLock) { CachedObjects.Clear(); }
                    }

                    Thread.Sleep(ObjectScanDelay);
                }
            });
            ObjectScannerThread.IsBackground = true;
            ObjectScannerThread.Start();

            var AutoTpThread = new Thread(() =>
            {
                while (true)
                {
                    if (AutoTpToObjects && !string.IsNullOrEmpty(ObjectEspName))
                    {
                        try
                        {
                            long DataModel = Memory.GetDataModel();
                            long Players = Memory.FindFirstClass(DataModel, "Players");

                            long LocalPlayer = Memory.ReadPointer(new IntPtr(Players + Convert.ToInt32(Memory.Offsets["LocalPlayer"], 16)));
                            if (LocalPlayer == 0)
                            {
                                var playerList = Memory.GetChildren(Players);
                                if (playerList.Count > 0) LocalPlayer = playerList[0];
                            }

                            if (LocalPlayer != 0)
                            {
                                long LocalCharacter = Memory.ReadPointer(new IntPtr(LocalPlayer + Convert.ToInt32(Memory.Offsets["ModelInstance"], 16)));
                                long Root = Memory.FindFirstChildByName(LocalCharacter, "HumanoidRootPart");
                                long PrimRoot = Memory.ReadPointer(new IntPtr(Root + Convert.ToInt32(Memory.Offsets["Primitive"], 16)));

                                if (PrimRoot != 0)
                                {
                                    List<ObjectCacheItem> targets;
                                    lock (CacheLock) { targets = new List<ObjectCacheItem>(CachedObjects); }

                                    int posOffset = Convert.ToInt32(Memory.Offsets["Position"], 16);

                                    foreach (var obj in targets)
                                    {
                                        if (!AutoTpToObjects) break;

                                        float ox = Memory.ReadFloat(new IntPtr(obj.Primitive + posOffset));
                                        float oy = Memory.ReadFloat(new IntPtr(obj.Primitive + posOffset + 4));
                                        float oz = Memory.ReadFloat(new IntPtr(obj.Primitive + posOffset + 8));

                                        Memory.Vector3 tpPos = new Memory.Vector3 { X = ox, Y = oy + 2.0f, Z = oz };
                                        Memory.WriteVector3(new IntPtr(PrimRoot + posOffset), tpPos);

                                        Thread.Sleep(AutoTpDelay);
                                    }
                                }
                            }
                        }
                        catch { }
                    }
                    Thread.Sleep(50);
                }
            });
            AutoTpThread.IsBackground = true;
            AutoTpThread.Start();


            var MainFormThread = new Thread(() =>
            {
                Color bgDark = Color.FromArgb(20, 20, 20);
                Color bgLight = Color.FromArgb(30, 30, 30);
                Color accent = Color.FromArgb(145, 100, 255);
                Color textWhite = Color.FromArgb(240, 240, 240);
                Color textGray = Color.FromArgb(150, 150, 150);
                Font mainFont = new Font("Segoe UI", 9F, FontStyle.Regular);
                Font headerFont = new Font("Segoe UI", 10F, FontStyle.Bold);

                var MainForm = new Form { Size = new Size(360, 520), StartPosition = FormStartPosition.Manual, Location = new Point(Screen.PrimaryScreen.Bounds.Width - 380, 20), BackColor = bgDark, FormBorderStyle = FormBorderStyle.None, TopMost = true, KeyPreview = true };
                try { MainForm.Icon = new Icon(Assembly.GetExecutingAssembly().GetManifestResourceStream("Hytux.Hytux.ico")); } catch { }
                MainForm.FormClosing += (s, e) => { RestoreDefaults(Memory); Environment.Exit(0); };

                var topBar = new Panel { Size = new Size(360, 35), Location = new Point(0, 0), BackColor = bgLight };
                var titleLabel = new Label { Text = "Hytux", ForeColor = accent, Font = headerFont, AutoSize = true, Location = new Point(10, 8) };
                var closeBtn = new Button { Text = "✕", Size = new Size(35, 35), Location = new Point(325, 0), FlatStyle = FlatStyle.Flat, ForeColor = textGray, BackColor = bgLight, Font = new Font("Segoe UI", 10F, FontStyle.Bold) };
                closeBtn.FlatAppearance.BorderSize = 0;
                closeBtn.MouseEnter += (s, e) => { closeBtn.BackColor = Color.DarkRed; closeBtn.ForeColor = Color.White; };
                closeBtn.MouseLeave += (s, e) => { closeBtn.BackColor = bgLight; closeBtn.ForeColor = textGray; };
                closeBtn.Click += (s, e) => { RestoreDefaults(Memory); Environment.Exit(0); };

                topBar.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) { ReleaseCapture(); SendMessage(MainForm.Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0); } };
                titleLabel.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) { ReleaseCapture(); SendMessage(MainForm.Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0); } };
                topBar.Controls.Add(titleLabel);
                topBar.Controls.Add(closeBtn);
                MainForm.Controls.Add(topBar);

                var contentPanel = new Panel { Location = new Point(0, 65), Size = new Size(360, 455), BackColor = bgDark };
                MainForm.Controls.Add(contentPanel);

                var pnlTabCombat = new CustomScrollablePanel { Dock = DockStyle.Fill, Visible = true };
                var pnlTabVisuals = new CustomScrollablePanel { Dock = DockStyle.Fill, Visible = false };
                var pnlTabColors = new CustomScrollablePanel { Dock = DockStyle.Fill, Visible = false };
                var pnlTabMovement = new CustomScrollablePanel { Dock = DockStyle.Fill, Visible = false };

                contentPanel.Controls.Add(pnlTabCombat);
                contentPanel.Controls.Add(pnlTabVisuals);
                contentPanel.Controls.Add(pnlTabColors);
                contentPanel.Controls.Add(pnlTabMovement);

                var wheelFilter = new WheelMessageFilter();
                wheelFilter.OnWheel = (delta) => {
                    if (pnlTabCombat.Visible) pnlTabCombat.ScrollDelta(delta);
                    else if (pnlTabVisuals.Visible) pnlTabVisuals.ScrollDelta(delta);
                    else if (pnlTabColors.Visible) pnlTabColors.ScrollDelta(delta);
                    else if (pnlTabMovement.Visible) pnlTabMovement.ScrollDelta(delta);
                };
                Application.AddMessageFilter(wheelFilter);
                MainForm.FormClosing += (s, e) => Application.RemoveMessageFilter(wheelFilter);

                var tabBar = new Panel { Location = new Point(0, 35), Size = new Size(360, 30), BackColor = Color.FromArgb(25, 25, 25) };
                MainForm.Controls.Add(tabBar);

                List<Button> tabBtns = new List<Button>();
                Button CreateTabBtn(string text, int x, CustomScrollablePanel target)
                {
                    var btn = new Button { Text = text, Location = new Point(x, 0), Size = new Size(90, 30), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(25, 25, 25), ForeColor = textGray, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
                    btn.FlatAppearance.BorderSize = 0;
                    tabBtns.Add(btn);
                    btn.Click += (s, e) =>
                    {
                        pnlTabCombat.Visible = false; pnlTabVisuals.Visible = false; pnlTabColors.Visible = false; pnlTabMovement.Visible = false;
                        target.Visible = true;
                        foreach (var b in tabBtns) { b.ForeColor = textGray; b.BackColor = Color.FromArgb(25, 25, 25); }
                        btn.ForeColor = accent; btn.BackColor = bgLight;
                    };
                    tabBar.Controls.Add(btn);
                    return btn;
                }

                var btnTab1 = CreateTabBtn("Combat", 0, pnlTabCombat);
                var btnTab2 = CreateTabBtn("Visuals", 90, pnlTabVisuals);
                var btnTab3 = CreateTabBtn("Colors", 180, pnlTabColors);
                var btnTab4 = CreateTabBtn("Movement", 270, pnlTabMovement);
                btnTab1.ForeColor = accent; btnTab1.BackColor = bgLight;

                Panel CreateCard(string title, int y, int height, Panel parent)
                {
                    var p = new Panel { Size = new Size(330, height), Location = new Point(10, y), BackColor = bgLight };
                    var lbl = new Label { Text = title.ToUpper(), ForeColor = textGray, Font = headerFont, AutoSize = true, Location = new Point(10, 10) };
                    p.Controls.Add(lbl); parent.Controls.Add(p); return p;
                }

                CustomToggle CreateToggle(string text, bool check, int x, int y, int w, Panel parent)
                {
                    var cb = new CustomToggle { Text = text, Checked = check, Location = new Point(x, y), Size = new Size(w, 20), ForeColor = textWhite, Font = mainFont, AccentColor = accent };
                    parent.Controls.Add(cb); return cb;
                }

                Label CreateLabel(string text, int x, int y, Panel parent)
                {
                    var lbl = new Label { Text = text, Location = new Point(x, y), AutoSize = true, ForeColor = textWhite, Font = mainFont };
                    parent.Controls.Add(lbl); return lbl;
                }

                CustomSlider CreateSlider(int min, int max, int val, int x, int y, Panel parent)
                {
                    var tb = new CustomSlider { Minimum = min, Maximum = max, Value = val, Location = new Point(x, y), AccentColor = accent };
                    parent.Controls.Add(tb); return tb;
                }

                CustomColorButton CreateColorBtn(string text, Color c, int x, int y, int width, Panel parent)
                {
                    var btn = new CustomColorButton { Text = text, Location = new Point(x, y), Size = new Size(width, 24), Font = mainFont };
                    btn.SetInitialColor(c); parent.Controls.Add(btn); return btn;
                }

                bool isAimKeyBinding = false; bool isFlyKeyBinding = false; bool isTpKeyBinding = false; bool isNoclipKeyBinding = false; bool isHitboxKeyBinding = false;
                Keys tempAimKey = Keys.None; Keys tempFlyKey = Keys.None; Keys tempTpKey = Keys.None; Keys tempNoclipKey = Keys.None; Keys tempHitboxKey = Keys.None;

                var pnlCombat = CreateCard("Combat Settings", 10, 335, pnlTabCombat.Content);
                var chkAimbotEnabled = CreateToggle("Enable Aimbot", AimbotEnabled, 15, 35, 130, pnlCombat);
                chkAimbotEnabled.CheckedChanged += (s, e) => AimbotEnabled = chkAimbotEnabled.Checked;

                var btnAimKey = new Button { Text = "Key: " + AimKey, Location = new Point(150, 32), Size = new Size(80, 24), BackColor = Color.FromArgb(15, 15, 15), ForeColor = accent, FlatStyle = FlatStyle.Flat, Font = mainFont };
                btnAimKey.FlatAppearance.BorderSize = 1; btnAimKey.FlatAppearance.BorderColor = Color.FromArgb(50, 50, 50);
                Action<Keys> setAimKey = (k) => { AimKey = k; btnAimKey.Text = "Key: " + k; isAimKeyBinding = false; IsAimbotLocked = false; tempAimKey = Keys.None; };
                btnAimKey.Click += (s, e) => { isAimKeyBinding = true; tempAimKey = AimKey; btnAimKey.Text = "Press..."; };
                btnAimKey.MouseDown += (s, e) => { if (isAimKeyBinding) { if (e.Button == MouseButtons.Left) setAimKey(Keys.LButton); else if (e.Button == MouseButtons.Right) setAimKey(Keys.RButton); else if (e.Button == MouseButtons.Middle) setAimKey(Keys.MButton); else if (e.Button == MouseButtons.XButton1) setAimKey(Keys.XButton1); else if (e.Button == MouseButtons.XButton2) setAimKey(Keys.XButton2); } };
                pnlCombat.Controls.Add(btnAimKey);

                var chkAimToggle = CreateToggle("Toggle", AimToggleMode, 240, 35, 90, pnlCombat);
                chkAimToggle.CheckedChanged += (s, e) => { AimToggleMode = chkAimToggle.Checked; IsAimbotLocked = false; };
                var chkTriggerbot = CreateToggle("Enable Triggerbot", TriggerbotEnabled, 15, 65, 150, pnlCombat);
                chkTriggerbot.CheckedChanged += (s, e) => TriggerbotEnabled = chkTriggerbot.Checked;

                CreateLabel("Aim Fov", 15, 105, pnlCombat);
                var tbAimFov = CreateSlider(10, 600, (int)AimFov, 90, 105, pnlCombat);
                var lblAimFovValue = CreateLabel(AimFov.ToString(), 245, 105, pnlCombat);
                tbAimFov.Scroll += (s, e) => { AimFov = tbAimFov.Value; lblAimFovValue.Text = AimFov.ToString(); };

                CreateLabel("Smoothing", 15, 145, pnlCombat);
                var tbAimSmoothing = CreateSlider(1, 100, (int)(AimSmoothing * 100), 90, 145, pnlCombat);
                var lblAimSmoothingValue = CreateLabel(AimSmoothing.ToString("F2"), 245, 145, pnlCombat);
                tbAimSmoothing.Scroll += (s, e) => { AimSmoothing = tbAimSmoothing.Value / 100f; lblAimSmoothingValue.Text = AimSmoothing.ToString("F2"); };

                CreateLabel("Speed", 15, 185, pnlCombat);
                var tbMaxMove = CreateSlider(10, 2000, MaxMove, 90, 185, pnlCombat);
                var lblMaxMoveValue = CreateLabel(MaxMove.ToString(), 245, 185, pnlCombat);
                tbMaxMove.Scroll += (s, e) => { MaxMove = tbMaxMove.Value; lblMaxMoveValue.Text = MaxMove.ToString(); };

                var chkAimAtHead = CreateToggle("Aim at Head", AimAtHead, 15, 235, 120, pnlCombat);
                chkAimAtHead.CheckedChanged += (s, e) => AimAtHead = chkAimAtHead.Checked;
                var chkAimPredict = CreateToggle("Predict Movement", PredictMovement, 150, 235, 160, pnlCombat);
                chkAimPredict.CheckedChanged += (s, e) => PredictMovement = chkAimPredict.Checked;

                var chkHitbox = CreateToggle("Expand Hitboxes", HitboxExpanderEnabled, 15, 265, 130, pnlCombat);
                chkHitboxInstance = chkHitbox;
                chkHitbox.CheckedChanged += (s, e) => HitboxExpanderEnabled = chkHitbox.Checked;

                CreateLabel("Key", 150, 267, pnlCombat);
                var btnHitboxKey = new Button { Text = HitboxKey.ToString(), Location = new Point(190, 263), Size = new Size(60, 24), BackColor = Color.FromArgb(15, 15, 15), ForeColor = accent, FlatStyle = FlatStyle.Flat, Font = mainFont };
                btnHitboxKey.FlatAppearance.BorderSize = 1; btnHitboxKey.FlatAppearance.BorderColor = Color.FromArgb(50, 50, 50);
                Action<Keys> setHitboxKey = (k) => { HitboxKey = k; btnHitboxKey.Text = k.ToString(); isHitboxKeyBinding = false; tempHitboxKey = Keys.None; };
                btnHitboxKey.Click += (s, e) => { isHitboxKeyBinding = true; tempHitboxKey = HitboxKey; btnHitboxKey.Text = "..."; };
                btnHitboxKey.MouseDown += (s, e) => { if (isHitboxKeyBinding) { if (e.Button == MouseButtons.Left) setHitboxKey(Keys.LButton); else if (e.Button == MouseButtons.Right) setHitboxKey(Keys.RButton); else if (e.Button == MouseButtons.Middle) setHitboxKey(Keys.MButton); else if (e.Button == MouseButtons.XButton1) setHitboxKey(Keys.XButton1); else if (e.Button == MouseButtons.XButton2) setHitboxKey(Keys.XButton2); } };
                pnlCombat.Controls.Add(btnHitboxKey);

                CreateLabel("Hitbox Size", 15, 295, pnlCombat);
                var tbHitboxSize = CreateSlider(2, 200, HitboxSize, 90, 295, pnlCombat);
                var lblHitboxSizeValue = CreateLabel(HitboxSize.ToString(), 245, 295, pnlCombat);
                tbHitboxSize.Scroll += (s, e) => { HitboxSize = tbHitboxSize.Value; lblHitboxSizeValue.Text = HitboxSize.ToString(); };

                var pnlEspMain = CreateCard("ESP Main", 10, 85, pnlTabVisuals.Content);
                var chkEspEnabled = CreateToggle("Enable ESP", EspEnabled, 15, 35, 180, pnlEspMain);
                chkEspEnabled.CheckedChanged += (s, e) => EspEnabled = chkEspEnabled.Checked;
                var chkBox = CreateToggle("Box", DrawBox, 15, 60, 80, pnlEspMain);
                chkBox.CheckedChanged += (s, e) => DrawBox = chkBox.Checked;
                var chkSkel = CreateToggle("Skeleton", DrawSkeleton, 105, 60, 90, pnlEspMain);
                chkSkel.CheckedChanged += (s, e) => DrawSkeleton = chkSkel.Checked;
                var chkLine = CreateToggle("Line", DrawLine, 215, 60, 80, pnlEspMain);
                chkLine.CheckedChanged += (s, e) => DrawLine = chkLine.Checked;

                var pnlEspInfo = CreateCard("Visual Info", 105, 130, pnlTabVisuals.Content);
                var chkNames = CreateToggle("Names", ShowNames, 15, 35, 80, pnlEspInfo);
                chkNames.CheckedChanged += (s, e) => ShowNames = chkNames.Checked;
                var chkDist = CreateToggle("Distance", ShowDistance, 105, 35, 90, pnlEspInfo);
                chkDist.CheckedChanged += (s, e) => ShowDistance = chkDist.Checked;
                var chkHp = CreateToggle("Health", ShowHealth, 215, 35, 80, pnlEspInfo);
                chkHp.CheckedChanged += (s, e) => ShowHealth = chkHp.Checked;
                var chkHpText = CreateToggle("HP as Text (Off = Bar)", HealthTextMode, 15, 65, 180, pnlEspInfo);
                chkHpText.CheckedChanged += (s, e) => HealthTextMode = chkHpText.Checked;
                var chkAvatar = CreateToggle("User Avatar", ShowAvatar, 15, 95, 110, pnlEspInfo);
                chkAvatar.CheckedChanged += (s, e) => ShowAvatar = chkAvatar.Checked;
                var cbAvatarPos = new CustomComboBox { Location = new Point(135, 93), Width = 110, Font = mainFont, ForeColor = textWhite, AccentColor = accent };
                cbAvatarPos.Items.AddRange(new string[] { "Left", "Right", "Top", "Bottom", "Center" });
                cbAvatarPos.SelectedIndex = AvatarPosition;
                cbAvatarPos.SelectedIndexChanged += (s, e) => AvatarPosition = cbAvatarPos.SelectedIndex;
                pnlEspInfo.Controls.Add(cbAvatarPos);

                var pnlTargets = CreateCard("Target Filters", 245, 60, pnlTabVisuals.Content);
                var chkEnemy = CreateToggle("Enemies", TargetEnemies, 15, 35, 80, pnlTargets);
                chkEnemy.CheckedChanged += (s, e) => TargetEnemies = chkEnemy.Checked;
                var chkTeam = CreateToggle("Team", TargetTeammates, 105, 35, 80, pnlTargets);
                chkTeam.CheckedChanged += (s, e) => TargetTeammates = chkTeam.Checked;
                var chkNeutral = CreateToggle("Neutrals", TargetNeutrals, 195, 35, 80, pnlTargets);
                chkNeutral.CheckedChanged += (s, e) => TargetNeutrals = chkNeutral.Checked;

                var pnlObjectEsp = CreateCard("Object ESP & Auto TP", 315, 190, pnlTabVisuals.Content);
                var chkObjectEsp = CreateToggle("Enable Object ESP", ObjectEspEnabled, 15, 35, 150, pnlObjectEsp);
                chkObjectEsp.CheckedChanged += (s, e) => ObjectEspEnabled = chkObjectEsp.Checked;

                CreateLabel("Name (* for all):", 15, 62, pnlObjectEsp);
                var txtObjectName = new TextBox { Text = ObjectEspName, Location = new Point(120, 60), Size = new Size(110, 20), BackColor = Color.FromArgb(15, 15, 15), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle, Font = mainFont };
                txtObjectName.TextChanged += (s, e) => ObjectEspName = txtObjectName.Text;
                pnlObjectEsp.Controls.Add(txtObjectName);

                var chkAutoTp = CreateToggle("Enable Auto TP", AutoTpToObjects, 15, 90, 150, pnlObjectEsp);
                chkAutoTp.CheckedChanged += (s, e) => AutoTpToObjects = chkAutoTp.Checked;

                CreateLabel("TP Delay", 15, 120, pnlObjectEsp);
                var tbTpDelay = CreateSlider(10, 1000, AutoTpDelay, 90, 120, pnlObjectEsp);
                var lblTpDelay = CreateLabel(AutoTpDelay.ToString() + "ms", 245, 120, pnlObjectEsp);
                tbTpDelay.Scroll += (s, e) => { AutoTpDelay = tbTpDelay.Value; lblTpDelay.Text = AutoTpDelay.ToString() + "ms"; };

                CreateLabel("Scan Delay", 15, 155, pnlObjectEsp);
                var tbScanDelay = CreateSlider(10, 1000, ObjectScanDelay, 90, 155, pnlObjectEsp);
                var lblScanDelay = CreateLabel(ObjectScanDelay.ToString() + "ms", 245, 155, pnlObjectEsp);
                tbScanDelay.Scroll += (s, e) => { ObjectScanDelay = tbScanDelay.Value; lblScanDelay.Text = ObjectScanDelay.ToString() + "ms"; };

                var pnlColors = CreateCard("Color Configuration", 10, 10, pnlTabColors.Content);
                CreateLabel("Player Colors & FOV", 15, 35, pnlColors).ForeColor = textGray;
                var btnEnemyCol = CreateColorBtn("Enemy", EnemyColor.Target, 15, 60, 70, pnlColors);
                btnEnemyCol.Click += (s, e) => { using (var cp = new CustomColorPickerForm(EnemyColor.Target)) if (cp.ShowDialog() == DialogResult.OK) { EnemyColor.Target = cp.SelectedColor; btnEnemyCol.ButtonColor = EnemyColor.Target; } };
                var btnTeamCol = CreateColorBtn("Team", TeamColor.Target, 95, 60, 70, pnlColors);
                btnTeamCol.Click += (s, e) => { using (var cp = new CustomColorPickerForm(TeamColor.Target)) if (cp.ShowDialog() == DialogResult.OK) { TeamColor.Target = cp.SelectedColor; btnTeamCol.ButtonColor = TeamColor.Target; } };
                var btnNeutralCol = CreateColorBtn("Neutral", NeutralColor.Target, 175, 60, 70, pnlColors);
                btnNeutralCol.Click += (s, e) => { using (var cp = new CustomColorPickerForm(NeutralColor.Target)) if (cp.ShowDialog() == DialogResult.OK) { NeutralColor.Target = cp.SelectedColor; btnNeutralCol.ButtonColor = NeutralColor.Target; } };
                var btnFovColor = CreateColorBtn("Aim FOV", FovColor.Target, 255, 60, 70, pnlColors);
                btnFovColor.Click += (s, e) => { using (var cp = new CustomColorPickerForm(FovColor.Target)) if (cp.ShowDialog() == DialogResult.OK) { FovColor.Target = cp.SelectedColor; btnFovColor.ButtonColor = FovColor.Target; } };

                CreateLabel("Object ESP Color", 15, 100, pnlColors).ForeColor = textGray;
                var btnObjectCol = CreateColorBtn("Objects", ObjectColor.Target, 15, 125, 70, pnlColors);
                btnObjectCol.Click += (s, e) => { using (var cp = new CustomColorPickerForm(ObjectColor.Target)) if (cp.ShowDialog() == DialogResult.OK) { ObjectColor.Target = cp.SelectedColor; btnObjectCol.ButtonColor = ObjectColor.Target; } };

                CreateLabel("Custom Text Colors", 15, 165, pnlColors).ForeColor = textGray;
                var chkCName = CreateToggle("Custom Name", UseCustomNameColor, 15, 195, 130, pnlColors);
                chkCName.CheckedChanged += (s, e) => UseCustomNameColor = chkCName.Checked;
                var btnCName = CreateColorBtn("Pick", CustomNameColor.Target, 150, 193, 60, pnlColors);
                btnCName.Click += (s, e) => { using (var cp = new CustomColorPickerForm(CustomNameColor.Target)) if (cp.ShowDialog() == DialogResult.OK) { CustomNameColor.Target = cp.SelectedColor; btnCName.ButtonColor = CustomNameColor.Target; } };
                var chkCDist = CreateToggle("Custom Dist", UseCustomDistColor, 15, 225, 130, pnlColors);
                chkCDist.CheckedChanged += (s, e) => UseCustomDistColor = chkCDist.Checked;
                var btnCDist = CreateColorBtn("Pick", CustomDistColor.Target, 150, 223, 60, pnlColors);
                btnCDist.Click += (s, e) => { using (var cp = new CustomColorPickerForm(CustomDistColor.Target)) if (cp.ShowDialog() == DialogResult.OK) { CustomDistColor.Target = cp.SelectedColor; btnCDist.ButtonColor = CustomDistColor.Target; } };
                var chkCHp = CreateToggle("Custom HP", UseCustomHpColor, 15, 255, 130, pnlColors);
                chkCHp.CheckedChanged += (s, e) => UseCustomHpColor = chkCHp.Checked;
                var btnCHp = CreateColorBtn("Pick", CustomHpColor.Target, 150, 253, 60, pnlColors);
                btnCHp.Click += (s, e) => { using (var cp = new CustomColorPickerForm(CustomHpColor.Target)) if (cp.ShowDialog() == DialogResult.OK) { CustomHpColor.Target = cp.SelectedColor; btnCHp.ButtonColor = CustomHpColor.Target; } };
                pnlColors.Height = 310;

                var pnlMovement = CreateCard("Movement Settings", 10, 280, pnlTabMovement.Content);
                chkFlyInstance = CreateToggle("Enable Fly", FlyEnabled, 15, 35, 110, pnlMovement);
                chkFlyInstance.CheckedChanged += (s, e) => FlyEnabled = chkFlyInstance.Checked;
                var cbFlyMode = new CustomComboBox { Location = new Point(135, 33), Width = 110, Font = mainFont, ForeColor = textWhite, AccentColor = accent };
                cbFlyMode.Items.AddRange(new string[] { "Hover", "Vector" });
                cbFlyMode.SelectedIndex = FlyMode;
                cbFlyMode.SelectedIndexChanged += (s, e) => FlyMode = cbFlyMode.SelectedIndex;
                pnlMovement.Controls.Add(cbFlyMode);

                CreateLabel("Fly Speed", 15, 65, pnlMovement);
                var tbFlySpeed = CreateSlider(10, 300, FlySpeed, 90, 65, pnlMovement);
                var lblFlySpeedValue = CreateLabel(FlySpeed.ToString(), 245, 65, pnlMovement);
                tbFlySpeed.Scroll += (s, e) => { FlySpeed = tbFlySpeed.Value; lblFlySpeedValue.Text = FlySpeed.ToString(); };

                CreateLabel("Fly Key", 15, 95, pnlMovement);
                var btnFlyKey = new Button { Text = FlyKey.ToString(), Location = new Point(70, 93), Size = new Size(60, 24), BackColor = Color.FromArgb(15, 15, 15), ForeColor = accent, FlatStyle = FlatStyle.Flat, Font = mainFont };
                btnFlyKey.FlatAppearance.BorderSize = 1; btnFlyKey.FlatAppearance.BorderColor = Color.FromArgb(50, 50, 50);
                Action<Keys> setFlyKey = (k) => { FlyKey = k; btnFlyKey.Text = k.ToString(); isFlyKeyBinding = false; tempFlyKey = Keys.None; };
                btnFlyKey.Click += (s, e) => { isFlyKeyBinding = true; tempFlyKey = FlyKey; btnFlyKey.Text = "..."; };
                btnFlyKey.MouseDown += (s, e) => { if (isFlyKeyBinding) { if (e.Button == MouseButtons.Left) setFlyKey(Keys.LButton); else if (e.Button == MouseButtons.Right) setFlyKey(Keys.RButton); else if (e.Button == MouseButtons.Middle) setFlyKey(Keys.MButton); else if (e.Button == MouseButtons.XButton1) setFlyKey(Keys.XButton1); else if (e.Button == MouseButtons.XButton2) setFlyKey(Keys.XButton2); } };
                pnlMovement.Controls.Add(btnFlyKey);

                CreateLabel("TP Key", 15, 135, pnlMovement);
                var btnTpKey = new Button { Text = TpKey.ToString(), Location = new Point(70, 133), Size = new Size(60, 24), BackColor = Color.FromArgb(15, 15, 15), ForeColor = accent, FlatStyle = FlatStyle.Flat, Font = mainFont };
                btnTpKey.FlatAppearance.BorderSize = 1; btnTpKey.FlatAppearance.BorderColor = Color.FromArgb(50, 50, 50);
                Action<Keys> setTpKey = (k) => { TpKey = k; btnTpKey.Text = k.ToString(); isTpKeyBinding = false; tempTpKey = Keys.None; };
                btnTpKey.Click += (s, e) => { isTpKeyBinding = true; tempTpKey = TpKey; btnTpKey.Text = "..."; };
                btnTpKey.MouseDown += (s, e) => { if (isTpKeyBinding) { if (e.Button == MouseButtons.Left) setTpKey(Keys.LButton); else if (e.Button == MouseButtons.Right) setTpKey(Keys.RButton); else if (e.Button == MouseButtons.Middle) setTpKey(Keys.MButton); else if (e.Button == MouseButtons.XButton1) setTpKey(Keys.XButton1); else if (e.Button == MouseButtons.XButton2) setTpKey(Keys.XButton2); } };
                pnlMovement.Controls.Add(btnTpKey);

                var cbTpMode = new CustomComboBox { Location = new Point(140, 133), Width = 100, Font = mainFont, ForeColor = textWhite, AccentColor = accent };
                cbTpMode.Items.AddRange(new string[] { "To Target", "Forward" });
                cbTpMode.SelectedIndex = TpMode;
                cbTpMode.SelectedIndexChanged += (s, e) => TpMode = cbTpMode.SelectedIndex;
                pnlMovement.Controls.Add(cbTpMode);

                CreateLabel("TP Dist", 15, 175, pnlMovement);
                var tbTpDist = CreateSlider(5, 500, TpDistance, 70, 175, pnlMovement);
                var lblTpDist = CreateLabel(TpDistance.ToString() + "m", 230, 175, pnlMovement);
                tbTpDist.Scroll += (s, e) => { TpDistance = tbTpDist.Value; lblTpDist.Text = TpDistance.ToString() + "m"; };

                chkNoclipInstance = CreateToggle("Enable Noclip", NoclipEnabled, 15, 215, 130, pnlMovement);
                chkNoclipInstance.CheckedChanged += (s, e) => NoclipEnabled = chkNoclipInstance.Checked;

                CreateLabel("Noclip Key", 15, 245, pnlMovement);
                var btnNoclipKey = new Button { Text = NoclipKey.ToString(), Location = new Point(90, 243), Size = new Size(60, 24), BackColor = Color.FromArgb(15, 15, 15), ForeColor = accent, FlatStyle = FlatStyle.Flat, Font = mainFont };
                btnNoclipKey.FlatAppearance.BorderSize = 1; btnNoclipKey.FlatAppearance.BorderColor = Color.FromArgb(50, 50, 50);
                Action<Keys> setNoclipKey = (k) => { NoclipKey = k; btnNoclipKey.Text = k.ToString(); isNoclipKeyBinding = false; tempNoclipKey = Keys.None; };
                btnNoclipKey.Click += (s, e) => { isNoclipKeyBinding = true; tempNoclipKey = NoclipKey; btnNoclipKey.Text = "..."; };
                btnNoclipKey.MouseDown += (s, e) => { if (isNoclipKeyBinding) { if (e.Button == MouseButtons.Left) setNoclipKey(Keys.LButton); else if (e.Button == MouseButtons.Right) setNoclipKey(Keys.RButton); else if (e.Button == MouseButtons.Middle) setNoclipKey(Keys.MButton); else if (e.Button == MouseButtons.XButton1) setNoclipKey(Keys.XButton1); else if (e.Button == MouseButtons.XButton2) setNoclipKey(Keys.XButton2); } };
                pnlMovement.Controls.Add(btnNoclipKey);

                MainForm.KeyDown += (s, e) => {
                    if (isAimKeyBinding) { if (e.KeyCode == Keys.Escape) { isAimKeyBinding = false; btnAimKey.Text = "Key: " + tempAimKey; } else setAimKey(e.KeyCode); }
                    else if (isFlyKeyBinding) { if (e.KeyCode == Keys.Escape) { isFlyKeyBinding = false; btnFlyKey.Text = "Key: " + tempFlyKey; } else setFlyKey(e.KeyCode); }
                    else if (isTpKeyBinding) { if (e.KeyCode == Keys.Escape) { isTpKeyBinding = false; btnTpKey.Text = "Key: " + tempTpKey; } else setTpKey(e.KeyCode); }
                    else if (isNoclipKeyBinding) { if (e.KeyCode == Keys.Escape) { isNoclipKeyBinding = false; btnNoclipKey.Text = "Key: " + tempNoclipKey; } else setNoclipKey(e.KeyCode); }
                    else if (isHitboxKeyBinding) { if (e.KeyCode == Keys.Escape) { isHitboxKeyBinding = false; btnHitboxKey.Text = tempHitboxKey.ToString(); } else setHitboxKey(e.KeyCode); }
                };
                btnAimKey.KeyDown += (s, e) => { if (isAimKeyBinding) setAimKey(e.KeyCode); };
                btnFlyKey.KeyDown += (s, e) => { if (isFlyKeyBinding) setFlyKey(e.KeyCode); };
                btnTpKey.KeyDown += (s, e) => { if (isTpKeyBinding) setTpKey(e.KeyCode); };
                btnNoclipKey.KeyDown += (s, e) => { if (isNoclipKeyBinding) setNoclipKey(e.KeyCode); };
                btnHitboxKey.KeyDown += (s, e) => { if (isHitboxKeyBinding) setHitboxKey(e.KeyCode); };

                MainForm.ShowDialog();
            });

            MainFormThread.SetApartmentState(ApartmentState.STA);
            MainFormThread.Start();

            var BlackBrush = new SolidBrush(Color.Black);
            var LimeBrush = new SolidBrush(Color.LimeGreen);
            var OrangeBrush = new SolidBrush(Color.Orange);
            var RedBrush = new SolidBrush(Color.Red);

            OverlayForm.DrawAction = (g) =>
            {
                EnemyColor.Update(); TeamColor.Update(); NeutralColor.Update(); FovColor.Update(); CustomNameColor.Update(); CustomDistColor.Update(); CustomHpColor.Update(); ObjectColor.Update();
                if (!EspEnabled && !AimbotEnabled && !TriggerbotEnabled && !FlyEnabled && !ObjectEspEnabled && !NoclipEnabled && TpMode == -1 && !HitboxExpanderEnabled) return;

                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                int ScreenWidth = Screen.PrimaryScreen.Bounds.Width;
                int ScreenHeight = Screen.PrimaryScreen.Bounds.Height;
                var CenterScreen = new Point(ScreenWidth / 2, ScreenHeight / 2);
                var BottomCenter = new Point(ScreenWidth / 2, ScreenHeight);

                var ViewMatrix = Memory.ReadViewMatrix();
                long DataModel = Memory.GetDataModel();
                long PlayersObject = Memory.FindFirstClass(DataModel, "Players");
                long LocalPlayerPointer = 0;
                long LocalTeamId = 0;

                IntPtr fw = GetForegroundWindow();
                GetWindowThreadProcessId(fw, out uint activePid);
                bool isRobloxActive = (activePid == Memory.ProcessId);

                if (DataModel == 0 || PlayersObject == 0) return;

                float localX = 0, localY = 0, localZ = 0;
                bool hasLocalPos = false;
                long LocalPrimRoot = 0;
                long LocalCharacter = 0;

                if (PlayersObject != 0)
                {
                    LocalPlayerPointer = Memory.ReadPointer(new IntPtr(PlayersObject + Convert.ToInt32(Memory.Offsets["LocalPlayer"], 16)));
                    if (LocalPlayerPointer == 0)
                    {
                        var PlayerList = Memory.GetChildren(PlayersObject);
                        if (PlayerList.Count > 0) LocalPlayerPointer = PlayerList[0];
                    }

                    if (LocalPlayerPointer != 0)
                    {
                        LocalTeamId = Memory.ReadPointer(new IntPtr(LocalTeamId != 0 ? LocalTeamId : 0));
                        LocalTeamId = Memory.ReadPointer(new IntPtr(LocalPlayerPointer + Convert.ToInt32(Memory.Offsets["Team"], 16)));
                        LocalCharacter = Memory.ReadPointer(new IntPtr(LocalPlayerPointer + Convert.ToInt32(Memory.Offsets["ModelInstance"], 16)));

                        if (LocalCharacter != 0)
                        {
                            long LocalHumanoid = Memory.FindFirstClass(LocalCharacter, "Humanoid");
                            long LocalRoot = Memory.FindFirstChildByName(LocalCharacter, "HumanoidRootPart");

                            if (LocalRoot != 0)
                            {
                                LocalPrimRoot = Memory.ReadPointer(new IntPtr(LocalRoot + Convert.ToInt32(Memory.Offsets["Primitive"], 16)));
                                if (LocalPrimRoot != 0)
                                {
                                    localX = Memory.ReadFloat(new IntPtr(LocalPrimRoot + Convert.ToInt32(Memory.Offsets["Position"], 16)));
                                    localY = Memory.ReadFloat(new IntPtr(LocalPrimRoot + Convert.ToInt32(Memory.Offsets["Position"], 16) + 4));
                                    localZ = Memory.ReadFloat(new IntPtr(LocalPrimRoot + Convert.ToInt32(Memory.Offsets["Position"], 16) + 8));
                                    hasLocalPos = true;
                                }
                            }

                            if (LocalHumanoid != 0 && LocalRoot != 0)
                            {
                                long HipHeightOffset = Convert.ToInt32(Memory.Offsets["HipHeight"], 16);
                                if (FlyEnabled)
                                {
                                    if (FlyMode == 0)
                                    {
                                        if (!WasHoverEnabled) { OriginalHipHeight = Memory.ReadFloat(new IntPtr(LocalHumanoid + HipHeightOffset)); WasHoverEnabled = true; }
                                        float currentHH = Memory.ReadFloat(new IntPtr(LocalHumanoid + HipHeightOffset));
                                        float moveHH = 0;
                                        if (isRobloxActive)
                                        {
                                            if ((GetAsyncKeyState(Keys.Space) & 0x8000) != 0) moveHH += (FlySpeed / 20f);
                                            if ((GetAsyncKeyState(Keys.LShiftKey) & 0x8000) != 0) moveHH -= (FlySpeed / 20f);
                                        }
                                        if (moveHH != 0) { float newHH = currentHH + moveHH; if (newHH < OriginalHipHeight) newHH = OriginalHipHeight; Memory.WriteFloat(new IntPtr(LocalHumanoid + HipHeightOffset), newHH); }
                                    }
                                    else if (FlyMode == 1 && LocalPrimRoot != 0)
                                    {
                                        if (!WasHoverEnabled) { OriginalHipHeight = Memory.ReadFloat(new IntPtr(LocalHumanoid + HipHeightOffset)); WasHoverEnabled = true; }
                                        Memory.WriteFloat(new IntPtr(LocalHumanoid + HipHeightOffset), OriginalHipHeight + 2.0f);
                                        long VelocityOffset = 0;
                                        try { VelocityOffset = Convert.ToInt32(Memory.Offsets["Velocity"], 16); } catch { }
                                        float velX = 0f, velY = 0f, velZ = 0f;
                                        if (isRobloxActive)
                                        {
                                            float rightX = ViewMatrix.M11, rightY = ViewMatrix.M12, rightZ = ViewMatrix.M13;
                                            float fwdX = ViewMatrix.M41, fwdY = ViewMatrix.M42, fwdZ = ViewMatrix.M43;
                                            float rLen = (float)Math.Sqrt(rightX * rightX + rightY * rightY + rightZ * rightZ);
                                            if (rLen > 0.001f) { rightX /= rLen; rightY /= rLen; rightZ /= rLen; }
                                            float fLen = (float)Math.Sqrt(fwdX * fwdX + fwdY * fwdY + fwdZ * fwdZ);
                                            if (fLen > 0.001f) { fwdX /= fLen; fwdY /= fLen; fwdZ /= fLen; }
                                            if ((GetAsyncKeyState(Keys.W) & 0x8000) != 0) { velX += fwdX; velY += fwdY; velZ += fwdZ; }
                                            if ((GetAsyncKeyState(Keys.S) & 0x8000) != 0) { velX -= fwdX; velY -= fwdY; velZ -= fwdZ; }
                                            if ((GetAsyncKeyState(Keys.D) & 0x8000) != 0) { velX += rightX; velY += rightY; velZ += rightZ; }
                                            if ((GetAsyncKeyState(Keys.A) & 0x8000) != 0) { velX -= rightX; velY -= rightY; velZ -= rightZ; }
                                            if ((GetAsyncKeyState(Keys.Space) & 0x8000) != 0) velY += 1.0f;
                                            if ((GetAsyncKeyState(Keys.LShiftKey) & 0x8000) != 0) velY -= 1.0f;
                                        }
                                        float length = (float)Math.Sqrt(velX * velX + velY * velY + velZ * velZ);
                                        if (length > 0) { velX = (velX / length) * FlySpeed; velY = (velY / length) * FlySpeed; velZ = (velZ / length) * FlySpeed; }
                                        Memory.Vector3 newVel = new Memory.Vector3 { X = velX, Y = velY, Z = velZ };
                                        if (VelocityOffset != 0) Memory.WriteVector3(new IntPtr(LocalPrimRoot + VelocityOffset), newVel);
                                    }
                                }
                                else if (WasHoverEnabled) { Memory.WriteFloat(new IntPtr(LocalHumanoid + HipHeightOffset), OriginalHipHeight); WasHoverEnabled = false; }
                            }
                        }
                    }
                }

                bool isFlyKeyPressed = (GetAsyncKeyState(FlyKey) & 0x8000) != 0;
                if (isFlyKeyPressed && !WasFlyKeyPressed) { FlyEnabled = !FlyEnabled; if (chkFlyInstance != null) try { chkFlyInstance.Invoke(new Action(() => { chkFlyInstance.Checked = FlyEnabled; })); } catch { } }
                WasFlyKeyPressed = isFlyKeyPressed;

                bool isNoclipKeyPressed = (GetAsyncKeyState(NoclipKey) & 0x8000) != 0;
                if (isNoclipKeyPressed && !WasNoclipKeyPressed) { NoclipEnabled = !NoclipEnabled; if (chkNoclipInstance != null) try { chkNoclipInstance.Invoke(new Action(() => { chkNoclipInstance.Checked = NoclipEnabled; })); } catch { } }
                WasNoclipKeyPressed = isNoclipKeyPressed;

                bool isHitboxKeyPressed = (GetAsyncKeyState(HitboxKey) & 0x8000) != 0;
                if (isHitboxKeyPressed && !WasHitboxKeyPressed)
                {
                    HitboxExpanderEnabled = !HitboxExpanderEnabled;
                    if (chkHitboxInstance != null) try { chkHitboxInstance.Invoke(new Action(() => { chkHitboxInstance.Checked = HitboxExpanderEnabled; })); } catch { }
                }
                WasHitboxKeyPressed = isHitboxKeyPressed;

                bool isAimKeyPressed = (GetAsyncKeyState(AimKey) & 0x8000) != 0;
                if (AimToggleMode) { if (isAimKeyPressed && !WasAimKeyPressed) IsAimbotLocked = !IsAimbotLocked; WasAimKeyPressed = isAimKeyPressed; IsAimKeyDown = IsAimbotLocked; }
                else IsAimKeyDown = isAimKeyPressed;

                Memory.Vector2 TargetScreenPos = new Memory.Vector2 { X = -1, Y = -1 };
                Memory.Vector3? BestTargetPos = null;
                bool HasTarget = false;
                List<long> PlayerListCurrent = PlayersObject != 0 ? Memory.GetChildren(PlayersObject) : new List<long>();
                HashSet<long> playerCharacterPointers = new HashSet<long>();

                if (AimbotEnabled || TriggerbotEnabled || TpMode == 0)
                {
                    if (AimbotEnabled) using (Pen FovPen = new Pen(IsAimbotLocked ? Color.Lime : FovColor.Current, 1f)) g.DrawEllipse(FovPen, CenterScreen.X - AimFov, CenterScreen.Y - AimFov, AimFov * 2, AimFov * 2);
                    if (PlayersObject != 0)
                    {
                        float bestTargetScore = float.MaxValue;
                        foreach (var PlayerPointer in PlayerListCurrent)
                        {
                            if (PlayerPointer == LocalPlayerPointer) continue;
                            long CurrentTeamId = Memory.ReadPointer(new IntPtr(PlayerPointer + Convert.ToInt32(Memory.Offsets["Team"], 16)));
                            bool isNeutral = CurrentTeamId == 0;
                            bool isTeam = CurrentTeamId != 0 && CurrentTeamId == LocalTeamId;
                            bool isEnemy = CurrentTeamId != 0 && CurrentTeamId != LocalTeamId;
                            if (isNeutral && !TargetNeutrals) continue;
                            if (isTeam && !TargetTeammates) continue;
                            if (isEnemy && !TargetEnemies) continue;

                            long CharacterPointer = Memory.ReadPointer(new IntPtr(PlayerPointer + Convert.ToInt32(Memory.Offsets["ModelInstance"], 16)));
                            if (CharacterPointer == 0) continue;
                            playerCharacterPointers.Add(CharacterPointer);

                            long HeadPart = Memory.FindFirstChildByName(CharacterPointer, "Head");
                            long RootPart = Memory.FindFirstChildByName(CharacterPointer, "HumanoidRootPart");
                            if (HeadPart == 0 || RootPart == 0) continue;

                            long PrimitiveHeadPointer = Memory.ReadPointer(new IntPtr(HeadPart + Convert.ToInt32(Memory.Offsets["Primitive"], 16)));
                            long PrimitiveRootPointer = Memory.ReadPointer(new IntPtr(RootPart + Convert.ToInt32(Memory.Offsets["Primitive"], 16)));
                            if (PrimitiveHeadPointer == 0 || PrimitiveRootPointer == 0) continue;

                            float HeadYOffset = 0.3f;
                            Memory.Vector3 PlayerHeadPosition = new Memory.Vector3 { X = Memory.ReadFloat(new IntPtr(PrimitiveHeadPointer + Convert.ToInt32(Memory.Offsets["Position"], 16))), Y = Memory.ReadFloat(new IntPtr(PrimitiveHeadPointer + Convert.ToInt32(Memory.Offsets["Position"], 16) + 4)) + HeadYOffset, Z = Memory.ReadFloat(new IntPtr(PrimitiveHeadPointer + Convert.ToInt32(Memory.Offsets["Position"], 16) + 8)) };
                            Memory.Vector3 PlayerRootPosition = new Memory.Vector3 { X = Memory.ReadFloat(new IntPtr(PrimitiveRootPointer + Convert.ToInt32(Memory.Offsets["Position"], 16))), Y = Memory.ReadFloat(new IntPtr(PrimitiveRootPointer + Convert.ToInt32(Memory.Offsets["Position"], 16) + 4)), Z = Memory.ReadFloat(new IntPtr(PrimitiveRootPointer + Convert.ToInt32(Memory.Offsets["Position"], 16) + 8)) };

                            float DistanceToPlayer = float.MaxValue;
                            if (hasLocalPos) DistanceToPlayer = (float)Math.Sqrt(Math.Pow(PlayerRootPosition.X - localX, 2) + Math.Pow(PlayerRootPosition.Y - localY, 2) + Math.Pow(PlayerRootPosition.Z - localZ, 2));

                            if (PredictMovement && DistanceToPlayer != float.MaxValue)
                            {
                                long velOffset = Convert.ToInt32(Memory.Offsets["Velocity"], 16);
                                float vx = Memory.ReadFloat(new IntPtr(PrimitiveRootPointer + velOffset));
                                float vy = Memory.ReadFloat(new IntPtr(PrimitiveRootPointer + velOffset + 4));
                                float vz = Memory.ReadFloat(new IntPtr(PrimitiveRootPointer + velOffset + 8));
                                float timeToTarget = DistanceToPlayer / 500f;
                                PlayerHeadPosition.X += vx * timeToTarget;
                                PlayerHeadPosition.Y += vy * timeToTarget;
                                PlayerHeadPosition.Z += vz * timeToTarget;
                                PlayerRootPosition.X += vx * timeToTarget;
                                PlayerRootPosition.Y += vy * timeToTarget;
                                PlayerRootPosition.Z += vz * timeToTarget;
                            }

                            if (Memory.WorldToScreen(AimAtHead ? PlayerHeadPosition : PlayerRootPosition, ViewMatrix, ScreenWidth, ScreenHeight, out Memory.Vector2 TargetScreenCoords))
                            {
                                if (TargetScreenCoords.X > 0 && TargetScreenCoords.X < ScreenWidth && TargetScreenCoords.Y > 0 && TargetScreenCoords.Y < ScreenHeight)
                                {
                                    float DistanceToCenter = (float)Math.Sqrt(Math.Pow(TargetScreenCoords.X - CenterScreen.X, 2) + Math.Pow(TargetScreenCoords.Y - CenterScreen.Y, 2));
                                    if (DistanceToCenter < AimFov)
                                    {
                                        float targetScore = DistanceToCenter + (DistanceToPlayer * 0.5f);
                                        if (targetScore < bestTargetScore)
                                        {
                                            bestTargetScore = targetScore;
                                            BestTargetPos = PlayerRootPosition;
                                            TargetScreenPos = TargetScreenCoords;
                                            HasTarget = true;
                                        }
                                    }
                                }
                            }
                        }

                        if (HasTarget && IsAimKeyDown && AimbotEnabled)
                        {
                            float TargetX = TargetScreenPos.X - CenterScreen.X; float TargetY = TargetScreenPos.Y - CenterScreen.Y;
                            if (Math.Abs(TargetX) < 3f) TargetX = 0; if (Math.Abs(TargetY) < 3f) TargetY = 0;
                            float SmoothedX = (TargetX / 2.5f) * AimSmoothing; float SmoothedY = (TargetY / 2.5f) * AimSmoothing;
                            int DeltaX = (int)Math.Round(SmoothedX); int DeltaY = (int)Math.Round(SmoothedY);
                            DeltaX = Math.Max(-MaxMove, Math.Min(MaxMove, DeltaX)); DeltaY = Math.Max(-MaxMove, Math.Min(MaxMove, DeltaY));
                            if (DeltaX != 0 || DeltaY != 0) mouse_event(MOUSEEVENTF_MOVE, (uint)DeltaX, (uint)DeltaY, 0, UIntPtr.Zero);
                        }
                    }
                }

                bool isTpKeyPressed = (GetAsyncKeyState(TpKey) & 0x8000) != 0;
                if (isTpKeyPressed && !WasTpKeyPressed && LocalPrimRoot != 0)
                {
                    long posOffset = Convert.ToInt32(Memory.Offsets["Position"], 16);
                    if (TpMode == 0) { if (BestTargetPos.HasValue) { Memory.Vector3 tpPos = BestTargetPos.Value; tpPos.Y += 2.0f; Memory.WriteVector3(new IntPtr(LocalPrimRoot + posOffset), tpPos); } }
                    else if (TpMode == 1)
                    {
                        long cframeOffset = Convert.ToInt32(Memory.Offsets["CFrame"], 16);
                        float r02 = Memory.ReadFloat(new IntPtr(LocalPrimRoot + cframeOffset + 20)); float r12 = Memory.ReadFloat(new IntPtr(LocalPrimRoot + cframeOffset + 32)); float r22 = Memory.ReadFloat(new IntPtr(LocalPrimRoot + cframeOffset + 44));
                        float lookX = -r02; float lookY = -r12; float lookZ = -r22;
                        float length = (float)Math.Sqrt(lookX * lookX + lookY * lookY + lookZ * lookZ);
                        if (length > 0)
                        {
                            lookX /= length; lookY /= length; lookZ /= length;
                            Memory.Vector3 tpPos = new Memory.Vector3 { X = localX + lookX * TpDistance, Y = localY + lookY * TpDistance, Z = localZ + lookZ * TpDistance };
                            Memory.WriteVector3(new IntPtr(LocalPrimRoot + posOffset), tpPos);
                        }
                    }
                }
                WasTpKeyPressed = isTpKeyPressed;

                if (ObjectEspEnabled && !string.IsNullOrEmpty(ObjectEspName))
                {
                    List<ObjectCacheItem> objectsToDraw;
                    lock (CacheLock)
                    {
                        objectsToDraw = new List<ObjectCacheItem>(CachedObjects);
                    }

                    foreach (var obj in objectsToDraw)
                    {
                        if (obj.Primitive != 0)
                        {
                            float ox = Memory.ReadFloat(new IntPtr(obj.Primitive + Convert.ToInt32(Memory.Offsets["Position"], 16)));
                            float oy = Memory.ReadFloat(new IntPtr(obj.Primitive + Convert.ToInt32(Memory.Offsets["Position"], 16) + 4));
                            float oz = Memory.ReadFloat(new IntPtr(obj.Primitive + Convert.ToInt32(Memory.Offsets["Position"], 16) + 8));

                            if (Memory.WorldToScreen(new Memory.Vector3 { X = ox, Y = oy, Z = oz }, ViewMatrix, ScreenWidth, ScreenHeight, out Memory.Vector2 objScn))
                            {
                                float objDist = 0;
                                if (hasLocalPos) objDist = (float)Math.Sqrt(Math.Pow(ox - localX, 2) + Math.Pow(oy - localY, 2) + Math.Pow(oz - localZ, 2));

                                if (objDist < 3000)
                                {
                                    using (Brush objBrush = new SolidBrush(ObjectColor.Current))
                                    {
                                        string text = $"{obj.Name} [{obj.Class}] ({objDist:F0}m)";
                                        var size = g.MeasureString(text, SystemFonts.DefaultFont);
                                        g.DrawString(text, SystemFonts.DefaultFont, objBrush, objScn.X - size.Width / 2, objScn.Y);
                                    }
                                }
                            }
                        }
                    }
                }

                if (!EspEnabled || PlayersObject == 0) return;

                foreach (var PlayerPointer in PlayerListCurrent)
                {
                    if (PlayerPointer == LocalPlayerPointer) continue;
                    long CurrentTeamId = Memory.ReadPointer(new IntPtr(PlayerPointer + Convert.ToInt32(Memory.Offsets["Team"], 16)));
                    bool isNeutral = CurrentTeamId == 0; bool isTeam = CurrentTeamId != 0 && CurrentTeamId == LocalTeamId; bool isEnemy = CurrentTeamId != 0 && CurrentTeamId != LocalTeamId;
                    if (isNeutral && !TargetNeutrals) continue; if (isTeam && !TargetTeammates) continue; if (isEnemy && !TargetEnemies) continue;

                    Color espColor = isNeutral ? NeutralColor.Current : (isTeam ? TeamColor.Current : EnemyColor.Current);
                    Pen PlayerPen = new Pen(espColor, 1.5f);
                    Brush NameBrush = new SolidBrush(UseCustomNameColor ? CustomNameColor.Current : espColor);
                    Brush DistBrush = new SolidBrush(UseCustomDistColor ? CustomDistColor.Current : espColor);
                    Brush hpTextBrush = new SolidBrush(UseCustomHpColor ? CustomHpColor.Current : espColor);

                    string PlayerName = Memory.GetName(PlayerPointer);
                    if (string.IsNullOrEmpty(PlayerName)) { PlayerPen.Dispose(); NameBrush.Dispose(); DistBrush.Dispose(); hpTextBrush.Dispose(); continue; }

                    long CharacterPointer = Memory.ReadPointer(new IntPtr(PlayerPointer + Convert.ToInt32(Memory.Offsets["ModelInstance"], 16)));
                    if (CharacterPointer == 0) { PlayerPen.Dispose(); NameBrush.Dispose(); DistBrush.Dispose(); hpTextBrush.Dispose(); continue; }

                    long RootPart = Memory.FindFirstChildByName(CharacterPointer, "HumanoidRootPart");
                    long HeadPart = Memory.FindFirstChildByName(CharacterPointer, "Head");
                    long HumanoidPointer = Memory.FindFirstClass(CharacterPointer, "Humanoid");
                    if (RootPart == 0 || HeadPart == 0) { PlayerPen.Dispose(); NameBrush.Dispose(); DistBrush.Dispose(); hpTextBrush.Dispose(); continue; }

                    long PrimitiveRootPointer = Memory.ReadPointer(new IntPtr(RootPart + Convert.ToInt32(Memory.Offsets["Primitive"], 16)));
                    long PrimitiveHeadPointer = Memory.ReadPointer(new IntPtr(HeadPart + Convert.ToInt32(Memory.Offsets["Primitive"], 16)));
                    if (PrimitiveRootPointer == 0 || PrimitiveHeadPointer == 0) { PlayerPen.Dispose(); NameBrush.Dispose(); DistBrush.Dispose(); hpTextBrush.Dispose(); continue; }

                    float hx = Memory.ReadFloat(new IntPtr(PrimitiveHeadPointer + Convert.ToInt32(Memory.Offsets["Position"], 16))); float hy = Memory.ReadFloat(new IntPtr(PrimitiveHeadPointer + Convert.ToInt32(Memory.Offsets["Position"], 16) + 4)); float hz = Memory.ReadFloat(new IntPtr(PrimitiveHeadPointer + Convert.ToInt32(Memory.Offsets["Position"], 16) + 8));
                    float rx = Memory.ReadFloat(new IntPtr(PrimitiveRootPointer + Convert.ToInt32(Memory.Offsets["Position"], 16))); float ry = Memory.ReadFloat(new IntPtr(PrimitiveRootPointer + Convert.ToInt32(Memory.Offsets["Position"], 16) + 4)); float rz = Memory.ReadFloat(new IntPtr(PrimitiveRootPointer + Convert.ToInt32(Memory.Offsets["Position"], 16) + 8));

                    float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
                    int partsFound = 0;
                    var charChildren = Memory.GetChildren(CharacterPointer);
                    foreach (var child in charChildren)
                    {
                        long prim = Memory.ReadPointer(new IntPtr(child + Convert.ToInt32(Memory.Offsets["Primitive"], 16)));
                        if (prim == 0) continue;
                        string partName = Memory.GetName(child);
                        if (!ValidBodyParts.Contains(partName)) continue;
                        float px = Memory.ReadFloat(new IntPtr(prim + Convert.ToInt32(Memory.Offsets["Position"], 16))); float py = Memory.ReadFloat(new IntPtr(prim + Convert.ToInt32(Memory.Offsets["Position"], 16) + 4)); float pz = Memory.ReadFloat(new IntPtr(prim + Convert.ToInt32(Memory.Offsets["Position"], 16) + 8));
                        if (Memory.WorldToScreen(new Memory.Vector3 { X = px, Y = py, Z = pz }, ViewMatrix, ScreenWidth, ScreenHeight, out Memory.Vector2 scn))
                        {
                            if (scn.X < minX) minX = scn.X; if (scn.X > maxX) maxX = scn.X; if (scn.Y < minY) minY = scn.Y; if (scn.Y > maxY) maxY = scn.Y;
                            partsFound++;
                        }
                    }

                    if (partsFound < 2) { PlayerPen.Dispose(); NameBrush.Dispose(); DistBrush.Dispose(); hpTextBrush.Dispose(); continue; }

                    Memory.WorldToScreen(new Memory.Vector3 { X = hx, Y = hy, Z = hz }, ViewMatrix, ScreenWidth, ScreenHeight, out Memory.Vector2 HeadScreen);
                    Memory.WorldToScreen(new Memory.Vector3 { X = hx, Y = hy + 1f, Z = hz }, ViewMatrix, ScreenWidth, ScreenHeight, out Memory.Vector2 TopHeadScreen);
                    float padding = Math.Abs(HeadScreen.Y - TopHeadScreen.Y);
                    minX -= padding * 0.7f; maxX += padding * 0.7f; minY -= padding; maxY += padding * 1.2f;

                    float BoxX = minX; float BoxY = minY; float BoxWidth = maxX - minX; float PlayerHeightOnScreen = maxY - minY;
                    bool CanDrawBox = (BoxWidth > 2 && PlayerHeightOnScreen > 5 && PlayerHeightOnScreen < ScreenHeight * 2.5f);
                    if (!CanDrawBox) { PlayerPen.Dispose(); NameBrush.Dispose(); DistBrush.Dispose(); hpTextBrush.Dispose(); continue; }

                    Memory.WorldToScreen(new Memory.Vector3 { X = rx, Y = ry - 3f, Z = rz }, ViewMatrix, ScreenWidth, ScreenHeight, out Memory.Vector2 FeetScreen);

                    if (TriggerbotEnabled && CanDrawBox)
                    {
                        RectangleF triggerBox = new RectangleF(BoxX + BoxWidth * 0.2f, BoxY, BoxWidth * 0.6f, PlayerHeightOnScreen);
                        if (triggerBox.Contains(CenterScreen))
                        {
                            if ((DateTime.Now - LastTriggerTime).TotalMilliseconds > 100)
                            {
                                LastTriggerTime = DateTime.Now;
                                Task.Run(() => { mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, UIntPtr.Zero); Thread.Sleep(20); mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero); });
                            }
                        }
                    }

                    float Distance = 0;
                    if (hasLocalPos) Distance = (float)Math.Sqrt(Math.Pow(rx - localX, 2) + Math.Pow(ry - localY, 2) + Math.Pow(rz - localZ, 2));

                    if (DrawBox && CanDrawBox) g.DrawRectangle(PlayerPen, BoxX, BoxY, BoxWidth, PlayerHeightOnScreen);
                    if (DrawLine) g.DrawLine(PlayerPen, BottomCenter.X, BottomCenter.Y, HeadScreen.X, HeadScreen.Y);
                    if (DrawSkeleton && CanDrawBox)
                    {
                        Memory.Vector2? GetPartScn(string Name)
                        {
                            long p = Memory.FindFirstChildByName(CharacterPointer, Name); if (p == 0) return null;
                            long prim = Memory.ReadPointer(new IntPtr(p + Convert.ToInt32(Memory.Offsets["Primitive"], 16))); if (prim == 0) return null;
                            float px = Memory.ReadFloat(new IntPtr(prim + Convert.ToInt32(Memory.Offsets["Position"], 16))); float py = Memory.ReadFloat(new IntPtr(prim + Convert.ToInt32(Memory.Offsets["Position"], 16) + 4)); float pz = Memory.ReadFloat(new IntPtr(prim + Convert.ToInt32(Memory.Offsets["Position"], 16) + 8));
                            if (Memory.WorldToScreen(new Memory.Vector3 { X = px, Y = py, Z = pz }, ViewMatrix, ScreenWidth, ScreenHeight, out Memory.Vector2 s)) return s;
                            return null;
                        }
                        Action<Memory.Vector2?, Memory.Vector2?> DrawBone = (p1, p2) => { if (p1.HasValue && p2.HasValue) g.DrawLine(PlayerPen, p1.Value.X, p1.Value.Y, p2.Value.X, p2.Value.Y); };
                        var sHead = GetPartScn("Head"); var uTorso = GetPartScn("UpperTorso");
                        if (uTorso.HasValue)
                        {
                            var lTorso = GetPartScn("LowerTorso"); var lUArm = GetPartScn("LeftUpperArm"); var lLArm = GetPartScn("LeftLowerArm"); var lHand = GetPartScn("LeftHand"); var rUArm = GetPartScn("RightUpperArm"); var rLArm = GetPartScn("RightLowerArm"); var rHand = GetPartScn("RightHand"); var lULeg = GetPartScn("LeftUpperLeg"); var lLLeg = GetPartScn("LeftLowerLeg"); var lFoot = GetPartScn("LeftFoot"); var rULeg = GetPartScn("RightUpperLeg"); var rLLeg = GetPartScn("RightLowerLeg"); var rFoot = GetPartScn("RightFoot");
                            DrawBone(sHead, uTorso); DrawBone(uTorso, lTorso); DrawBone(uTorso, lUArm); DrawBone(lUArm, lLArm); DrawBone(lLArm, lHand); DrawBone(uTorso, rUArm); DrawBone(rUArm, rLArm); DrawBone(rLArm, rHand); DrawBone(lTorso, lULeg); DrawBone(lULeg, lLLeg); DrawBone(lLLeg, lFoot); DrawBone(lTorso, rULeg); DrawBone(rULeg, rLLeg); DrawBone(rLLeg, rFoot);
                        }
                        else
                        {
                            var Torso = GetPartScn("Torso");
                            if (Torso.HasValue)
                            {
                                var lArm = GetPartScn("Left Arm"); var rArm = GetPartScn("Right Arm"); var lLeg = GetPartScn("Left Leg"); var rLeg = GetPartScn("Right Leg");
                                DrawBone(sHead, Torso); DrawBone(Torso, lArm); DrawBone(Torso, rArm); DrawBone(Torso, lLeg); DrawBone(Torso, rLeg);
                            }
                            else
                            {
                                float skelHeight = PlayerHeightOnScreen; float skelWidth = skelHeight / 2.5f;
                                Memory.Vector2 neck = new Memory.Vector2 { X = HeadScreen.X, Y = HeadScreen.Y + skelHeight * 0.1f }; Memory.Vector2 pelvis = new Memory.Vector2 { X = HeadScreen.X, Y = HeadScreen.Y + skelHeight * 0.5f }; Memory.Vector2 lShoulder = new Memory.Vector2 { X = HeadScreen.X - skelWidth / 2, Y = neck.Y }; Memory.Vector2 rShoulder = new Memory.Vector2 { X = HeadScreen.X + skelWidth / 2, Y = neck.Y }; Memory.Vector2 lHand = new Memory.Vector2 { X = HeadScreen.X - skelWidth * 0.8f, Y = pelvis.Y }; Memory.Vector2 rHand = new Memory.Vector2 { X = HeadScreen.X + skelWidth * 0.8f, Y = pelvis.Y }; Memory.Vector2 lKnee = new Memory.Vector2 { X = HeadScreen.X - skelWidth / 4, Y = FeetScreen.Y - skelHeight * 0.25f }; Memory.Vector2 rKnee = new Memory.Vector2 { X = HeadScreen.X + skelWidth / 4, Y = FeetScreen.Y - skelHeight * 0.25f }; Memory.Vector2 lFoot = new Memory.Vector2 { X = HeadScreen.X - skelWidth / 4, Y = FeetScreen.Y }; Memory.Vector2 rFoot = new Memory.Vector2 { X = HeadScreen.X + skelWidth / 4, Y = FeetScreen.Y };
                                DrawBone(HeadScreen, neck); DrawBone(neck, pelvis); DrawBone(neck, lShoulder); DrawBone(neck, rShoulder); DrawBone(lShoulder, lHand); DrawBone(rShoulder, rHand); DrawBone(pelvis, lKnee); DrawBone(pelvis, rKnee); DrawBone(lKnee, lFoot); DrawBone(rKnee, rFoot);
                            }
                        }
                    }

                    float TextY = CanDrawBox ? BoxY - 15 : HeadScreen.Y - 15;
                    float CurrentAvatarSize = 35f;
                    if (ShowAvatar)
                    {
                        long UserId = 0;
                        try { UserId = Memory.ReadLong(new IntPtr(PlayerPointer + Convert.ToInt32(Memory.Offsets["UserId"], 16))); } catch { }
                        if (UserId > 0 && UserId < 99999999999)
                        {
                            Image AvatarToDraw = null;
                            lock (AvatarCache) { if (AvatarCache.ContainsKey(UserId)) AvatarToDraw = AvatarCache[UserId]; else if (!PendingAvatars.Contains(UserId)) { PendingAvatars.Add(UserId); _ = Task.Run(() => FetchAvatarAsync(UserId)); } }
                            if (AvatarToDraw != null)
                            {
                                float imgX = 0; float imgY = 0;
                                switch (AvatarPosition)
                                {
                                    case 0: imgX = BoxX - CurrentAvatarSize - 5; imgY = BoxY; break;
                                    case 1: imgX = BoxX + BoxWidth + 5; imgY = BoxY; break;
                                    case 2: imgX = HeadScreen.X - CurrentAvatarSize / 2; imgY = TextY - CurrentAvatarSize - 5; TextY -= (CurrentAvatarSize + 5); break;
                                    case 3: imgX = HeadScreen.X - CurrentAvatarSize / 2; imgY = BoxY + PlayerHeightOnScreen + (CanDrawBox ? 10 : 2); break;
                                    case 4: imgX = HeadScreen.X - CurrentAvatarSize / 2; imgY = BoxY + PlayerHeightOnScreen / 2 - CurrentAvatarSize / 2; break;
                                }
                                try { g.DrawImage(AvatarToDraw, imgX, imgY, CurrentAvatarSize, CurrentAvatarSize); } catch { }
                            }
                        }
                    }

                    if (ShowNames)
                    {
                        var NameSize = g.MeasureString(PlayerName, SystemFonts.DefaultFont);
                        g.DrawString(PlayerName, SystemFonts.DefaultFont, NameBrush, HeadScreen.X - NameSize.Width / 2, TextY - NameSize.Height);
                        TextY -= NameSize.Height;
                    }

                    if (ShowDistance)
                    {
                        var DistText = $"{Distance:F0}m";
                        var DistSize = g.MeasureString(DistText, SystemFonts.DefaultFont);
                        g.DrawString(DistText, SystemFonts.DefaultFont, DistBrush, HeadScreen.X - DistSize.Width / 2, TextY - DistSize.Height);
                        TextY -= DistSize.Height;
                    }

                    if (ShowHealth && HumanoidPointer != 0)
                    {
                        float Health = Memory.ReadFloat(new IntPtr(HumanoidPointer + Convert.ToInt32(Memory.Offsets["Health"], 16)));
                        float MaxHealth = Memory.ReadFloat(new IntPtr(HumanoidPointer + Convert.ToInt32(Memory.Offsets["MaxHealth"], 16)));
                        if (MaxHealth > 0)
                        {
                            float HealthPercentage = Health / MaxHealth;
                            if (HealthTextMode)
                            {
                                string hpText = $"HP: {Math.Round(Health)}";
                                var hpSize = g.MeasureString(hpText, SystemFonts.DefaultFont);
                                g.DrawString(hpText, SystemFonts.DefaultFont, hpTextBrush, HeadScreen.X - hpSize.Width / 2, TextY - hpSize.Height);
                            }
                            else if (CanDrawBox)
                            {
                                g.FillRectangle(BlackBrush, BoxX, BoxY + PlayerHeightOnScreen + 2, BoxWidth, 4);
                                Brush hb = HealthPercentage >= 0.75f ? LimeBrush : (HealthPercentage >= 0.4f ? OrangeBrush : RedBrush);
                                g.FillRectangle(hb, BoxX, BoxY + PlayerHeightOnScreen + 2, BoxWidth * HealthPercentage, 4);
                            }
                        }
                    }

                    PlayerPen.Dispose(); NameBrush.Dispose(); DistBrush.Dispose(); hpTextBrush.Dispose();
                }
                OverlayForm.Invalidate();
            };

            Application.Run(OverlayForm);
        }
    }
}
