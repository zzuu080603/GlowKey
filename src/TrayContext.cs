using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Microsoft.Win32;

namespace GlowKey
{
    /// <summary>
    /// 应用主体：托盘图标 + 右键菜单（内嵌滑块）+ 全局热键 + OSD + 设置窗口。
    /// 内部持有一个隐藏窗口承载 WM_HOTKEY 消息循环。
    /// </summary>
    public class TrayContext : IDisposable
    {
        private const int HotkeyIdUp = 1;
        private const int HotkeyIdDown = 2;
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string RunValueName = "GlowKey";

        private class HiddenMessageForm : Form
        {
            public TrayContext Context;

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == NativeMethods.WM_HOTKEY && Context != null)
                {
                    Context.HandleHotkey(ref m);
                }
                base.WndProc(ref m);
            }

            // 标准“不可见消息窗口”模式：Application.Run(form) 会尝试 Show 它，
            // 这里强制其永远不可见，但句柄正常创建（RegisterHotKey 需要）。
            protected override void SetVisibleCore(bool value)
            {
                if (!IsHandleCreated)
                {
                    CreateHandle();
                }
                base.SetVisibleCore(false);
            }
        }

        private readonly HiddenMessageForm _form;
        private readonly NotifyIcon _tray;
        private readonly BrightnessController _brightness;
        private readonly HotkeyManager _hotkeys;
        private readonly AppSettings _settings;
        private readonly OsdForm _osd;
        private readonly SliderFlyout _flyout;
        private readonly Bitmap _iconBitmap;

        private TrackBar _menuSlider;
        private ToolStripLabel _menuValue;
        private ToolStripLabel _menuStatus;
        private ToolStripMenuItem _autoStartItem;

        /// <summary>隐藏消息窗口，作为 Application.Run 的宿主。</summary>
        public Form MessageForm
        {
            get { return _form; }
        }

        public TrayContext()
        {
            _settings = AppSettings.Load();
            _brightness = new BrightnessController();
            _osd = new OsdForm();
            _flyout = new SliderFlyout(_brightness);

            _form = new HiddenMessageForm();
            _form.Context = this;
            _form.ShowInTaskbar = false;
            IntPtr dummy = _form.Handle; // 强制创建句柄，供 RegisterHotKey 使用

            _hotkeys = new HotkeyManager(_form.Handle);

            _iconBitmap = DrawSunIcon(32, 32);
            _tray = new NotifyIcon();
            _tray.Icon = Icon.FromHandle(_iconBitmap.GetHicon());
            _tray.Text = "GlowKey 屏幕亮度";
            _tray.Visible = true;
            _tray.ContextMenuStrip = BuildMenu();
            _tray.MouseClick += OnTrayMouseClick;

            ApplyHotkeys(true);
        }

        private ContextMenuStrip BuildMenu()
        {
            ContextMenuStrip menu = new ContextMenuStrip();

            ToolStripLabel title = new ToolStripLabel("屏幕亮度");
            title.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);

            _menuValue = new ToolStripLabel("--%");
            _menuValue.Alignment = ToolStripItemAlignment.Right;

            _menuStatus = new ToolStripLabel(_brightness.StatusDescription);
            _menuStatus.ForeColor = Color.DimGray;

            _menuSlider = new TrackBar();
            _menuSlider.Minimum = 0;
            _menuSlider.Maximum = 100;
            _menuSlider.TickFrequency = 10;
            _menuSlider.Size = new Size(190, 28);
            _menuSlider.Scroll += OnMenuSliderScroll;

            ToolStripControlHost sliderHost = new ToolStripControlHost(_menuSlider);
            sliderHost.AutoSize = false;
            sliderHost.Height = 30;
            sliderHost.Margin = new Padding(8, 2, 8, 4);

            ToolStripSeparator sep1 = new ToolStripSeparator();
            ToolStripSeparator sep2 = new ToolStripSeparator();

            ToolStripMenuItem settingsItem = new ToolStripMenuItem("设置快捷键(&S)...");
            settingsItem.Click += delegate { ShowSettings(); };

            _autoStartItem = new ToolStripMenuItem("开机自动启动(&A)");
            _autoStartItem.Click += OnToggleAutoStart;

            ToolStripMenuItem rescanItem = new ToolStripMenuItem("重新扫描显示器(&R)");
            rescanItem.Click += delegate
            {
                _brightness.Scan();
                _menuStatus.Text = _brightness.StatusDescription;
                SyncMenuFromCurrent();
            };

            ToolStripMenuItem exitItem = new ToolStripMenuItem("退出(&X)");
            exitItem.Click += OnExit;

            menu.Items.Add(title);
            menu.Items.Add(_menuValue);
            menu.Items.Add(_menuStatus);
            menu.Items.Add(sliderHost);
            menu.Items.Add(sep1);
            menu.Items.Add(settingsItem);
            menu.Items.Add(_autoStartItem);
            menu.Items.Add(rescanItem);
            menu.Items.Add(sep2);
            menu.Items.Add(exitItem);

            menu.Opening += delegate
            {
                _menuStatus.Text = _brightness.StatusDescription;
                _autoStartItem.Checked = GetAutoStart();
                _menuSlider.Enabled = _brightness.SupportsBrightness;
                SyncMenuFromCurrent();
            };

            return menu;
        }

        private void SyncMenuFromCurrent()
        {
            int v = _brightness.Current;
            if (v >= 0 && v <= 100 && _menuSlider.Value != v) _menuSlider.Value = v;
            _menuValue.Text = v.ToString() + "%";
        }

        private void OnMenuSliderScroll(object sender, EventArgs e)
        {
            _menuValue.Text = _menuSlider.Value.ToString() + "%";
            _brightness.RequestSet(_menuSlider.Value);
        }

        private void OnTrayMouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && _brightness.SupportsBrightness)
            {
                _flyout.ShowNearTray();
            }
        }

        private void HandleHotkey(ref Message m)
        {
            _hotkeys.Handle(m);
        }

        private void ApplyHotkeys(bool silent)
        {
            string upName = HotkeyUtil.Describe(_settings.UpModifiers, _settings.UpKey);
            string downName = HotkeyUtil.Describe(_settings.DownModifiers, _settings.DownKey);

            bool okUp = _hotkeys.Register(HotkeyIdUp, _settings.UpModifiers, _settings.UpKey,
                delegate { AdjustBrightness(_settings.Step); });
            bool okDown = _hotkeys.Register(HotkeyIdDown, _settings.DownModifiers, _settings.DownKey,
                delegate { AdjustBrightness(-_settings.Step); });

            if (!silent && (!okUp || !okDown))
            {
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                if (!okUp) sb.AppendLine("「" + upName + "」注册失败，可能已被其他软件占用。");
                if (!okDown) sb.AppendLine("「" + downName + "」注册失败，可能已被其他软件占用。");
                sb.Append("请在设置中换一个组合键。");
                MessageBox.Show(_form, sb.ToString(), "GlowKey 快捷键", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void AdjustBrightness(int delta)
        {
            if (!_brightness.SupportsBrightness) return;
            _brightness.Step(delta);
            if (_settings.ShowOsd) _osd.ShowValue(_brightness.Current);
        }

        private void ShowSettings()
        {
            using (SettingsForm form = new SettingsForm(_settings))
            {
                if (form.ShowDialog(_form) == DialogResult.OK)
                {
                    _settings.Save();
                    ApplyHotkeys(false);
                }
            }
        }

        private static bool GetAutoStart()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath))
                {
                    if (key == null) return false;
                    return key.GetValue(RunValueName) != null;
                }
            }
            catch { return false; }
        }

        private void OnToggleAutoStart(object sender, EventArgs e)
        {
            try
            {
                bool enable = !GetAutoStart();
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKeyPath))
                {
                    if (key == null) return;
                    if (enable)
                    {
                        key.SetValue(RunValueName, "\"" + Application.ExecutablePath + "\"");
                    }
                    else
                    {
                        key.DeleteValue(RunValueName, false);
                    }
                }
                _autoStartItem.Checked = enable;
            }
            catch (Exception ex)
            {
                MessageBox.Show(_form, "设置开机自启失败：" + ex.Message, "GlowKey",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnExit(object sender, EventArgs e)
        {
            // 延迟到菜单关闭之后再退出，避免在 ContextMenuStrip 事件内销毁自身
            _form.BeginInvoke((MethodInvoker)delegate
            {
                _form.Close();
            });
        }

        /// <summary>用 GDI+ 画一个金色太阳图标，免去携带 .ico 资源文件。</summary>
        private static Bitmap DrawSunIcon(int width, int height)
        {
            Bitmap bmp = new Bitmap(width, height);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                Color gold = Color.FromArgb(255, 185, 0);

                Point center = new Point(width / 2, height / 2);
                int core = width / 5;

                using (Pen ray = new Pen(gold, Math.Max(2f, width / 10f)))
                {
                    ray.StartCap = LineCap.Round;
                    ray.EndCap = LineCap.Round;
                    for (int i = 0; i < 8; i++)
                    {
                        double angle = i * Math.PI / 4.0 + Math.PI / 8.0;
                        int r1 = (int)(core * 1.45);
                        int r2 = width / 2 - 1;
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
            return bmp;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _hotkeys.UnregisterAll();
                _tray.Visible = false;
                _tray.Dispose();
                _osd.Dispose();
                _flyout.Dispose();
                _brightness.Dispose();
                if (_iconBitmap != null) _iconBitmap.Dispose();
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
