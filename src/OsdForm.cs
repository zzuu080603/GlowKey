using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace GlowKey
{
    /// <summary>
    /// 热键调节亮度时在屏幕底部居中显示的 OSD 提示（仿系统音量提示），
    /// 显示约 0.9 秒后自动隐藏。点击穿透、永不抢焦点。
    /// 注意：刻意不使用 Opacity 半透明——DPI 缩放（如 150%）下
    /// 无 DPI 感知的进程使用分层窗口会出现整窗空白，纯色绘制最稳。
    /// </summary>
    public class OsdForm : Form
    {
        private const int HoldMs = 900;

        private readonly Timer _holdTimer;
        private int _value;

        public OsdForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            Size = new Size(260, 100);
            BackColor = Color.FromArgb(20, 20, 20);

            using (GraphicsPath path = new GraphicsPath())
            {
                int r = 18;
                Rectangle b = new Rectangle(0, 0, Width, Height);
                path.AddArc(b.X, b.Y, r * 2, r * 2, 180, 90);
                path.AddArc(b.Right - r * 2, b.Y, r * 2, r * 2, 270, 90);
                path.AddArc(b.Right - r * 2, b.Bottom - r * 2, r * 2, r * 2, 0, 90);
                path.AddArc(b.X, b.Bottom - r * 2, r * 2, r * 2, 90, 90);
                path.CloseFigure();
                Region = new Region(path);
            }

            _holdTimer = new Timer();
            _holdTimer.Interval = HoldMs;
            _holdTimer.Tick += delegate { _holdTimer.Stop(); Hide(); };
        }

        /// <summary>显示/刷新当前亮度百分比（须在 UI 线程调用）。</summary>
        public void ShowValue(int percent)
        {
            _value = percent;
            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(wa.Left + (wa.Width - Width) / 2, wa.Bottom - Height - 48);

#if DEBUG_OSD
            try
            {
                System.IO.File.AppendAllText(
                    System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData), "GlowKey", "debug.log"),
                    "ShowValue " + percent + "% workingArea=" + wa.ToString() + " loc=" + Location.ToString() + " size=" + Size.ToString() + " screen=" + Screen.FromControl(this).Bounds.ToString() + "\r\n");
            }
            catch { }
#endif

            _holdTimer.Stop();
            _holdTimer.Start();

            if (!Visible) Show();
            Invalidate();
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x08000000   // WS_EX_NOACTIVATE：不抢焦点
                            | 0x00000080;  // WS_EX_TOOLWINDOW：不出现在 Alt+Tab
                return cp;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            DrawSun(g, new Rectangle(24, (Height - 52) / 2, 52, 52));

            using (Brush text = new SolidBrush(Color.White))
            {
                using (Font font = new Font("Segoe UI", 34f, FontStyle.Bold, GraphicsUnit.Pixel))
                {
                    string label = _value.ToString() + "%";
                    using (StringFormat sf = new StringFormat())
                    {
                        sf.Alignment = StringAlignment.Center;
                        sf.LineAlignment = StringAlignment.Center;
                        g.DrawString(label, font, text, new RectangleF(90, 0, Width - 100, Height), sf);
                    }
                }
            }
        }

        private static void DrawSun(Graphics g, Rectangle bounds)
        {
            Color gold = Color.FromArgb(255, 214, 10);
            Point center = new Point(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
            int core = bounds.Width / 4;

            using (Pen ray = new Pen(gold, bounds.Width / 13f))
            {
                ray.StartCap = LineCap.Round;
                ray.EndCap = LineCap.Round;
                for (int i = 0; i < 8; i++)
                {
                    double angle = i * Math.PI / 4.0;
                    int r1 = (int)(core * 1.5);
                    int r2 = (int)(bounds.Width / 2.0);
                    g.DrawLine(
                        ray,
                        center.X + (int)Math.Round(Math.Cos(angle) * r1),
                        center.Y + (int)Math.Round(Math.Sin(angle) * r1),
                        center.X + (int)Math.Round(Math.Cos(angle) * r2),
                        center.Y + (int)Math.Round(Math.Sin(angle) * r2));
                }
            }

            using (Brush fill = new SolidBrush(gold))
            {
                g.FillEllipse(fill, center.X - core, center.Y - core, core * 2, core * 2);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _holdTimer.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
