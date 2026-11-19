using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace GlowKey
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            bool createdNew;
            using (Mutex mutex = new Mutex(true, @"Local\GlowKey::4D1E9C72-6B7A-4E8D-9F0A-3C5B2A1E8D64", out createdNew))
            {
                if (!createdNew)
                {
                    MessageBox.Show("GlowKey 已经在运行了，请查看右下角的托盘图标。",
                        "GlowKey", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                try
                {
                    using (TrayContext ctx = new TrayContext())
                    {
                        Application.Run(ctx.MessageForm);
                    }
                }
                catch (Exception ex)
                {
                    LogError(ex);
                    MessageBox.Show("GlowKey 遇到未处理的错误，即将退出：\r\n" + ex.Message,
                        "GlowKey", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }

                GC.KeepAlive(mutex);
            }
        }

        private static void LogError(Exception ex)
        {
            try
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GlowKey");
                Directory.CreateDirectory(dir);
                File.AppendAllText(
                    Path.Combine(dir, "error.log"),
                    "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "] " + ex.ToString() + Environment.NewLine,
                    Encoding.UTF8);
            }
            catch
            {
                // 日志失败时保持沉默，不能因日志再抛异常
            }
        }
    }
}
