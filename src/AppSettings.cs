using System;
using System.IO;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace GlowKey
{
    /// <summary>
    /// 用户设置，持久化为 %APPDATA%\GlowKey\settings.json。
    /// 字段保持公共以便 JavaScriptSerializer 序列化。
    /// </summary>
    public class AppSettings
    {
        public uint UpModifiers = NativeMethods.MOD_ALT;
        public int UpKey = (int)Keys.Up;
        public uint DownModifiers = NativeMethods.MOD_ALT;
        public int DownKey = (int)Keys.Down;
        public int Step = 10;
        public bool ShowOsd = true;

        private static string SettingsDir
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GlowKey"); }
        }

        private static string SettingsPath
        {
            get { return Path.Combine(SettingsDir, "settings.json"); }
        }

        public static AppSettings Load()
        {
            AppSettings s = null;
            try
            {
                if (File.Exists(SettingsPath))
                {
                    JavaScriptSerializer js = new JavaScriptSerializer();
                    s = js.Deserialize<AppSettings>(File.ReadAllText(SettingsPath));
                }
            }
            catch
            {
                s = null;
            }
            if (s == null) s = new AppSettings();

            // 兜底：文件损坏/缺字段时回到安全默认值
            if (s.Step <= 0 || s.Step > 50) s.Step = 10;
            if (s.UpKey <= 0)
            {
                s.UpModifiers = NativeMethods.MOD_ALT;
                s.UpKey = (int)Keys.Up;
            }
            if (s.DownKey <= 0)
            {
                s.DownModifiers = NativeMethods.MOD_ALT;
                s.DownKey = (int)Keys.Down;
            }
            return s;
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(SettingsDir);
                JavaScriptSerializer js = new JavaScriptSerializer();
                File.WriteAllText(SettingsPath, js.Serialize(this));
            }
            catch
            {
                // 保存失败不致命：本次会话仍按内存中的设置工作
            }
        }
    }
}
