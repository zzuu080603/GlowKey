using System;
using System.ComponentModel;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace GlowKey
{
    /// <summary>把 修饰键位标志 + 虚拟键码 显示成 "Ctrl + Alt + G" 这样的文字。</summary>
    internal static class HotkeyUtil
    {
        public static string Describe(uint modifiers, int vk)
        {
            if (vk <= 0) return "（未设置）";
            StringBuilder sb = new StringBuilder();
            if ((modifiers & NativeMethods.MOD_CONTROL) != 0) sb.Append("Ctrl + ");
            if ((modifiers & NativeMethods.MOD_ALT) != 0) sb.Append("Alt + ");
            if ((modifiers & NativeMethods.MOD_SHIFT) != 0) sb.Append("Shift + ");
            if ((modifiers & NativeMethods.MOD_WIN) != 0) sb.Append("Win + ");
            sb.Append(KeyName((Keys)vk));
            return sb.ToString();
        }

        public static string KeyName(Keys key)
        {
            switch (key)
            {
                case Keys.Up: return "↑";
                case Keys.Down: return "↓";
                case Keys.Left: return "←";
                case Keys.Right: return "→";
                default:
                    try { return new KeysConverter().ConvertToString(key) ?? key.ToString(); }
                    catch { return key.ToString(); }
            }
        }

        public static bool IsWinPressed()
        {
            return (NativeMethods.GetKeyState(NativeMethods.VK_LWIN) & 0x8000) != 0
                || (NativeMethods.GetKeyState(NativeMethods.VK_RWIN) & 0x8000) != 0;
        }
    }

    /// <summary>捕获用户按下组合键的只读文本框：点击后按 “修饰键 + 字母/箭头/F键” 即录入。</summary>
    public class HotkeyBox : TextBox
    {
        public uint ModifierFlags = 0;
        public int KeyValue = 0;

        public HotkeyBox()
        {
            ReadOnly = true;
            ShortcutsEnabled = false;
            BackColor = Color.White;
            TextAlign = HorizontalAlignment.Center;
            Font = new Font("Segoe UI", 10f);
            Height = 28;
            Text = "点击后按组合键";
        }

        public void SetCombo(uint mods, int vk)
        {
            ModifierFlags = mods;
            KeyValue = vk;
            RefreshText();
        }

        private void RefreshText()
        {
            Text = HotkeyUtil.Describe(ModifierFlags, KeyValue);
        }

        protected override bool IsInputKey(Keys keyData)
        {
            // 让方向键进入 KeyDown 而不是移动焦点
            Keys k = keyData & Keys.KeyCode;
            if (k == Keys.Up || k == Keys.Down || k == Keys.Left || k == Keys.Right) return true;
            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;

            Keys code = e.KeyCode;
            if (code == Keys.Escape || code == Keys.Back)
            {
                SetCombo(0, 0);
                return;
            }

            // 纯修饰键按下不算组合
            if (code == Keys.ControlKey || code == Keys.ShiftKey || code == Keys.Menu
                || code == Keys.LWin || code == Keys.RWin
                || code == Keys.LControlKey || code == Keys.RControlKey
                || code == Keys.LShiftKey || code == Keys.RShiftKey
                || code == Keys.LMenu || code == Keys.RMenu)
            {
                return;
            }

            uint mods = 0;
            if (e.Control) mods |= NativeMethods.MOD_CONTROL;
            if (e.Alt) mods |= NativeMethods.MOD_ALT;
            if (e.Shift) mods |= NativeMethods.MOD_SHIFT;
            if (HotkeyUtil.IsWinPressed()) mods |= NativeMethods.MOD_WIN;

            if (mods == 0)
            {
                Text = "请至少搭配 Ctrl / Alt / Shift / Win";
                return;
            }

            SetCombo(mods, (int)code);
        }
    }

    /// <summary>设置窗口：自定义两个热键、调节步长、OSD 开关。</summary>
    public class SettingsForm : Form
    {
        private readonly AppSettings _settings;
        private HotkeyBox _upBox;
        private HotkeyBox _downBox;
        private NumericUpDown _stepBox;
        private CheckBox _osdCheck;

        public SettingsForm(AppSettings settings)
        {
            _settings = settings;

            Text = "GlowKey 设置";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(400, 286);
            Font = new Font("Segoe UI", 9f);

            Label hint = new Label();
            hint.Text = "点击输入框后，直接按下想要的组合键（如 Ctrl + Alt + G）；Esc 清除。";
            hint.Dock = DockStyle.Top;
            hint.Height = 34;
            hint.ForeColor = Color.DimGray;

            GroupBox upGroup = new GroupBox();
            upGroup.Text = "调高亮度";
            upGroup.SetBounds(14, 40, 180, 62);

            _upBox = new HotkeyBox();
            _upBox.SetBounds(12, 22, 156, 28);
            _upBox.SetCombo(settings.UpModifiers, settings.UpKey);
            upGroup.Controls.Add(_upBox);

            GroupBox downGroup = new GroupBox();
            downGroup.Text = "调低亮度";
            downGroup.SetBounds(206, 40, 180, 62);

            _downBox = new HotkeyBox();
            _downBox.SetBounds(12, 22, 156, 28);
            _downBox.SetCombo(settings.DownModifiers, settings.DownKey);
            downGroup.Controls.Add(_downBox);

            Label stepLabel = new Label();
            stepLabel.Text = "每次调节步长：";
            stepLabel.SetBounds(20, 118, 96, 22);

            _stepBox = new NumericUpDown();
            _stepBox.Minimum = 1;
            _stepBox.Maximum = 50;
            _stepBox.Value = settings.Step;
            _stepBox.SetBounds(116, 115, 56, 24);

            Label stepUnit = new Label();
            stepUnit.Text = "%";
            stepUnit.SetBounds(176, 118, 24, 22);

            _osdCheck = new CheckBox();
            _osdCheck.Text = "调节时在屏幕上显示亮度提示（OSD）";
            _osdCheck.Checked = settings.ShowOsd;
            _osdCheck.SetBounds(18, 150, 340, 24);

            Button reset = new Button();
            reset.Text = "恢复默认";
            reset.SetBounds(16, 208, 96, 30);
            reset.Click += OnReset;

            Button cancel = new Button();
            cancel.Text = "取消";
            cancel.SetBounds(206, 208, 84, 30);
            cancel.DialogResult = DialogResult.Cancel;

            Button ok = new Button();
            ok.Text = "保存";
            ok.SetBounds(300, 208, 84, 30);
            ok.DialogResult = DialogResult.OK;

            AcceptButton = ok;
            CancelButton = cancel;

            Controls.AddRange(new Control[] { hint, upGroup, downGroup, stepLabel, _stepBox, stepUnit, _osdCheck, reset, cancel, ok });
        }

        private void OnReset(object sender, EventArgs e)
        {
            _upBox.SetCombo(NativeMethods.MOD_ALT, (int)Keys.Up);
            _downBox.SetCombo(NativeMethods.MOD_ALT, (int)Keys.Down);
            _stepBox.Value = 10;
            _osdCheck.Checked = true;
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (DialogResult == DialogResult.OK)
            {
                _settings.UpModifiers = _upBox.ModifierFlags;
                _settings.UpKey = _upBox.KeyValue;
                _settings.DownModifiers = _downBox.ModifierFlags;
                _settings.DownKey = _downBox.KeyValue;
                _settings.Step = (int)_stepBox.Value;
                _settings.ShowOsd = _osdCheck.Checked;
            }
            base.OnClosing(e);
        }
    }
}
