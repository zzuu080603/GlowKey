using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace GlowKey
{
    /// <summary>
    /// RegisterHotKey 全局热键的注册/分发封装。
    /// WM_HOTKEY 由宿主窗口的 WndProc 转发到 Handle(Message)。
    /// </summary>
    public class HotkeyManager
    {
        private readonly IntPtr _hwnd;
        private readonly Dictionary<int, Action> _actions = new Dictionary<int, Action>();

        public HotkeyManager(IntPtr hwnd)
        {
            _hwnd = hwnd;
        }

        /// <summary>注册热键。vk 小于等于 0 视为“未绑定”，直接成功返回。</summary>
        public bool Register(int id, uint modifiers, int vk, Action action)
        {
            Unregister(id);
            if (vk <= 0) return true;
            if (!NativeMethods.RegisterHotKey(_hwnd, id, modifiers | NativeMethods.MOD_NOREPEAT, (uint)vk))
                return false;
            lock (((System.Collections.ICollection)_actions).SyncRoot)
            {
                _actions[id] = action;
            }
            return true;
        }

        public void Unregister(int id)
        {
            lock (((System.Collections.ICollection)_actions).SyncRoot)
            {
                _actions.Remove(id);
            }
            NativeMethods.UnregisterHotKey(_hwnd, id);
        }

        public void UnregisterAll()
        {
            int[] ids;
            lock (((System.Collections.ICollection)_actions).SyncRoot)
            {
                ids = new int[_actions.Keys.Count];
                _actions.Keys.CopyTo(ids, 0);
            }
            foreach (int id in ids) Unregister(id);
        }

        /// <summary>由宿主窗口 WndProc 在收到 WM_HOTKEY 时调用。</summary>
        public void Handle(Message m)
        {
            if (m.Msg != NativeMethods.WM_HOTKEY) return;
            int id = m.WParam.ToInt32();
            Action action;
            lock (((System.Collections.ICollection)_actions).SyncRoot)
            {
                if (!_actions.TryGetValue(id, out action)) return;
            }
            if (action != null) action();
        }
    }
}
