using System;
using System.Drawing;
using System.Windows.Forms;

namespace GlowKey
{
    /// <summary>
    /// 左键点击托盘图标时在任务栏上方弹出的竖直亮度滑条浮窗。
    /// 不抢焦点，4 秒无操作自动关闭。
    /// </summary>
    public class SliderFlyout : Form
    {
        private readonly BrightnessController _controller;
        private readonly TrackBar _slider;
        private readonly Label _valueLabel;
        private readonly Timer _applyTimer;   // 拖动过程中的去抖
        private readonly Timer _closeTimer;

        public SliderFlyout(BrightnessController controller)
        {
            _controller = controller;

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            BackColor = Color.FromArgb(32, 32, 32);
            Size = new Size(84, 240);

            _valueLabel = new Label();
            _valueLabel.ForeColor = Color.White;
            _valueLabel.Font = new Font("Segoe UI", 12f, FontStyle.Bold);
            _valueLabel.TextAlign = ContentAlignment.MiddleCenter;
            _valueLabel.Dock = DockStyle.Top;
            _valueLabel.Height = 34;
            _valueLabel.Text = "--";

            _slider = new TrackBar();
            _slider.Orientation = Orientation.Vertical;
            _slider.TickStyle = TickStyle.None;
            _slider.Minimum = 0;
            _slider.Maximum = 100;
            _slider.Dock = DockStyle.Fill;
            _slider.BackColor = Color.FromArgb(32, 32, 32);
            _slider.Scroll += OnSliderScroll;
            _slider.MouseUp += delegate { ApplyNow(); };

            Controls.Add(_slider);
            Controls.Add(_valueLabel);

            _applyTimer = new Timer();
            _applyTimer.Interval = 120;
            _applyTimer.Tick += delegate { _applyTimer.Stop(); ApplyNow(); };

            _closeTimer = new Timer();
            _closeTimer.Interval = 4000;
            _closeTimer.Tick += delegate { _closeTimer.Stop(); Hide(); };
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x08000000   // WS_EX_NOACTIVATE
                            | 0x00000080;  // WS_EX_TOOLWINDOW
                return cp;
            }
        }

        /// <summary>在屏幕右下角（托盘附近）显示浮窗（须在 UI 线程调用）。</summary>
        public void ShowNearTray()
        {
            _slider.Value = FitValue(_controller.Current);

            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            Point p = Cursor.Position;
            Screen screen = Screen.FromPoint(p);
            Rectangle area = screen.WorkingArea;
            if (area.Width <= 0 || area.Height <= 0) area = wa;

            int x = area.Right - Width - 12;
            int y = area.Bottom - Height - 8;
            Location = new Point(x, y);

            UpdateLabel();
            Show();
            _closeTimer.Stop();
            _closeTimer.Start();
        }

        private void OnSliderScroll(object sender, EventArgs e)
        {
            UpdateLabel();
            KickTimers();
            _applyTimer.Stop();
            _applyTimer.Start();
        }

        private void ApplyNow()
        {
            _controller.RequestSet(_slider.Value);
            UpdateLabel();
            KickTimers();
        }

        private void KickTimers()
        {
            _closeTimer.Stop();
            _closeTimer.Start();
        }

        private void UpdateLabel()
        {
            _valueLabel.Text = _slider.Value.ToString() + "%";
        }

        private static int FitValue(int v)
        {
            if (v < 0) return 0;
            if (v > 100) return 100;
            return v;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _applyTimer.Dispose();
                _closeTimer.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
