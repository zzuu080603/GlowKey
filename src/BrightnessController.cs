using System;
using System.Collections.Generic;
using System.Management;
using System.Threading;

namespace GlowKey
{
    /// <summary>
    /// 亮度控制双通道：
    ///   1. WMI（笔记本内置屏，root\wmi 的 WmiMonitorBrightnessMethods）
    ///   2. DDC/CI（外接显示器，dxva2.dll，需显示器 OSD 中开启 DDC/CI，一般默认开启）
    /// 两条通道都无需管理员权限。设置请求在后台线程排队执行，UI 永不卡顿。
    /// </summary>
    public class BrightnessController : IDisposable
    {
        private class DdcMonitor
        {
            public IntPtr Handle;
            public string Name;
            public uint Min;
            public uint Max;

            public int ToPercent(uint native)
            {
                if (Max <= Min) return 0;
                int p = (int)Math.Round((double)(native - Min) * 100.0 / (Max - Min));
                return Clamp(p);
            }

            public int ToNative(int percent)
            {
                if (Max <= Min) return (int)Min;
                return (int)(Min + Math.Round((double)(Max - Min) * Clamp(percent) / 100.0));
            }
        }

        private readonly object _sync = new object();
        private List<DdcMonitor> _ddc = new List<DdcMonitor>();
        private bool _wmiAvailable;
        private int _current = -1;          // 最近一次已知亮度（0..100）
        private int _pending = -1;          // 待应用的亮度（由后台线程取走）
        private ManualResetEvent _wake;
        private Thread _worker;
        private volatile bool _disposed;

        /// <summary>是否检测到任何可调亮度的通道。</summary>
        public bool SupportsBrightness
        {
            get { lock (_sync) { return _wmiAvailable || _ddc.Count > 0; } }
        }

        /// <summary>当前亮度（0..100）。不触碰硬件，直接读缓存。</summary>
        public int Current
        {
            get { lock (_sync) { return _current; } }
        }

        /// <summary>状态描述，用于托盘菜单展示检测到了哪些通道。</summary>
        public string StatusDescription
        {
            get
            {
                lock (_sync)
                {
                    if (!_wmiAvailable && _ddc.Count == 0)
                        return "未检测到支持亮度调节的显示器";
                    List<string> parts = new List<string>();
                    if (_wmiAvailable) parts.Add("内置屏(WMI)");
                    if (_ddc.Count > 0) parts.Add("外接屏(DDC/CI)×" + _ddc.Count);
                    return string.Join(" + ", parts.ToArray());
                }
            }
        }

        public BrightnessController()
        {
            Scan();
            _current = ReadActual();
            if (_current < 0) _current = 50;

            _wake = new ManualResetEvent(false);
            _worker = new Thread(WorkerLoop);
            _worker.IsBackground = true;
            _worker.Name = "GlowKeyBrightnessWorker";
            _worker.Start();
        }

        /// <summary>重新枚举显示器（显示器热插拔后调用）。</summary>
        public void Scan()
        {
            lock (_sync)
            {
                foreach (DdcMonitor m in _ddc)
                {
                    try { NativeMethods.DestroyPhysicalMonitor(m.Handle); }
                    catch { }
                }
                _ddc = new List<DdcMonitor>();
            }

            _wmiAvailable = ProbeWmi();

            NativeMethods.MonitorEnumProc callback = delegate(IntPtr hMonitor, IntPtr hdc, ref NativeMethods.RECT rect, IntPtr data)
            {
                uint count;
                if (!NativeMethods.GetNumberOfPhysicalMonitorsFromHMONITOR(hMonitor, out count) || count == 0)
                    return true;

                NativeMethods.PHYSICAL_MONITOR[] physical = new NativeMethods.PHYSICAL_MONITOR[count];
                if (!NativeMethods.GetPhysicalMonitorsFromHMONITOR(hMonitor, count, physical))
                    return true;

                foreach (NativeMethods.PHYSICAL_MONITOR pm in physical)
                {
                    DdcMonitor m = new DdcMonitor();
                    m.Handle = pm.Handle;
                    m.Name = pm.Description;
                    m.Min = 0;
                    m.Max = 255;
                    uint mn, cur, mx;
                    if (NativeMethods.GetMonitorBrightness(pm.Handle, out mn, out cur, out mx) && mx > mn)
                    {
                        m.Min = mn;
                        m.Max = mx;
                    }
                    lock (_sync) { _ddc.Add(m); }
                }
                return true;
            };
            try { NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, IntPtr.Zero); }
            catch { }
        }

        /// <summary>请求把亮度设为指定百分比（0..100）。异步执行，立即返回。</summary>
        public void RequestSet(int percent)
        {
            percent = Clamp(percent);
            lock (_sync)
            {
                _pending = percent;
                _current = percent; // 乐观更新，UI 立即可见
            }
            _wake.Set();
        }

        /// <summary>在当前亮度基础上增减 delta 个百分点。</summary>
        public void Step(int delta)
        {
            int baseValue;
            lock (_sync) { baseValue = _current; }
            RequestSet(baseValue + delta);
        }

        /// <summary>实际读取硬件当前亮度（优先 WMI，其次第一台 DDC 显示器）；失败返回 -1。</summary>
        public int ReadActual()
        {
            int? v = TryReadWmi();
            if (v.HasValue) return v.Value;

            lock (_sync)
            {
                if (_ddc.Count > 0)
                {
                    uint mn, cur, mx;
                    if (NativeMethods.GetMonitorBrightness(_ddc[0].Handle, out mn, out cur, out mx) && mx > mn)
                        return _ddc[0].ToPercent(cur);
                }
            }
            return -1;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _wake.Set();
            _worker.Join(1000);
            lock (_sync)
            {
                foreach (DdcMonitor m in _ddc)
                {
                    try { NativeMethods.DestroyPhysicalMonitor(m.Handle); }
                    catch { }
                }
                _ddc.Clear();
            }
        }

        private void WorkerLoop()
        {
            while (!_disposed)
            {
                _wake.WaitOne();
                _wake.Reset();
                while (!_disposed)
                {
                    int v;
                    lock (_sync)
                    {
                        v = _pending;
                        _pending = -1;
                    }
                    if (v < 0) break;
                    ApplyValue(v);
                    lock (_sync) { _current = v; }
                }
            }
        }

        private void ApplyValue(int percent)
        {
            percent = Clamp(percent);

            if (_wmiAvailable)
            {
                try
                {
                    using (ManagementObjectSearcher searcher =
                        new ManagementObjectSearcher(@"root\wmi", "SELECT * FROM WmiMonitorBrightnessMethods"))
                    {
                        foreach (ManagementObject mo in searcher.Get())
                        {
                            try { mo.InvokeMethod("WmiSetBrightness", new object[] { UInt32.MaxValue, (uint)percent }); }
                            finally { ((IDisposable)mo).Dispose(); }
                        }
                    }
                }
                catch
                {
                    // 单次失败不打断：下次设置仍会尝试
                }
            }

            List<DdcMonitor> monitors;
            lock (_sync) { monitors = new List<DdcMonitor>(_ddc); }
            foreach (DdcMonitor m in monitors)
            {
                try { NativeMethods.SetMonitorBrightness(m.Handle, (uint)m.ToNative(percent)); }
                catch { }
            }
        }

        private static bool ProbeWmi()
        {
            try
            {
                using (ManagementObjectSearcher searcher =
                    new ManagementObjectSearcher(@"root\wmi", "SELECT * FROM WmiMonitorBrightnessMethods"))
                {
                    foreach (ManagementObject mo in searcher.Get())
                    {
                        ((IDisposable)mo).Dispose();
                        return true;
                    }
                }
            }
            catch { }
            return false;
        }

        private static int? TryReadWmi()
        {
            try
            {
                using (ManagementObjectSearcher searcher =
                    new ManagementObjectSearcher(@"root\wmi", "SELECT CurrentBrightness FROM WmiMonitorBrightness"))
                {
                    foreach (ManagementObject mo in searcher.Get())
                    {
                        try
                        {
                            object val = mo["CurrentBrightness"];
                            if (val != null) return Convert.ToInt32(val);
                        }
                        finally { ((IDisposable)mo).Dispose(); }
                    }
                }
            }
            catch { }
            return null;
        }

        private static int Clamp(int v)
        {
            if (v < 0) return 0;
            if (v > 100) return 100;
            return v;
        }
    }
}
