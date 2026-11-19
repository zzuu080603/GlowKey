# GlowKey

**Adjust screen brightness with global hotkeys and a tray slider — no digging through Windows Settings.**

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![Platform: Windows 10/11](https://img.shields.io/badge/Platform-Windows%2010%2F11-blue)]()
[![Dependencies: None](https://img.shields.io/badge/Dependencies-None-success)]()

![OSD preview](docs/osd-screenshot.png)

*The on-screen brightness indicator shown when you press a hotkey.*

## Why

Changing screen brightness on Windows usually means: open Settings → System → Display → find the slider. GlowKey makes it a keystroke — and works on desktop PCs with external monitors too, where Windows often offers no software brightness control at all.

## Features

| Feature | Description |
|---|---|
| 🌒 Global hotkeys | Default `Alt+↑` / `Alt+↓`, step 10%. Fully customizable: any `Ctrl/Alt/Shift/Win` + key combo |
| 🎚️ Tray slider | Slider embedded in the right-click menu; left-click the tray icon for a volume-style flyout slider |
| 🔔 OSD indicator | Dark rounded popup with a sun icon and the current percentage, auto-hides (can be disabled) |
| 🖥️ Dual-channel | Laptop internal panels (WMI) **and** external monitors over DDC/CI (HDMI/DP/DVI/VGA) |
| 🔃 Auto-start | Optional, per-user registry Run key — no admin rights needed |
| 📦 Tiny & portable | One ~30 KB `.exe`. No installer, no .NET download, no admin, no network access |

Works on Windows 10 and 11. Everything runs locally — GlowKey never touches the network.

## Install

1. Download **`GlowKey.exe`** from the latest [Release](../../releases) (~30 KB).
2. Double-click it. A gold sun icon appears in the tray (if you don't see it, click the `^` hidden-icons button and drag it onto the taskbar).
3. Optional: right-click the tray icon → **开机自动启动 / Start with Windows**.

> Tip: put the exe in a fixed folder (e.g. `C:\Tools\GlowKey\`) before enabling auto-start — the Run entry points to wherever the exe lives.

## Usage

- **Hotkeys** — press `Alt+↑` / `Alt+↓` to change brightness. To rebind (e.g. `Ctrl+Alt+G` / `Ctrl+Alt+C`): tray right-click → **Settings** → click a box → press your combo → Save.
- **Tray menu** — right-click the tray icon: a brightness slider at the top, settings, auto-start toggle, rescan monitors, exit.
- **Flyout slider** — left-click the tray icon.
- Settings persist in `%APPDATA%\GlowKey\settings.json`.

## How it works

GlowKey uses both software-brightness channels Windows offers, needs neither elevation nor vendor drivers:

1. **Internal panels** — `WmiSetBrightness` from the `root\wmi` namespace (`WmiMonitorBrightnessMethods`), the same API Windows Settings uses on laptops.
2. **External monitors** — the DDC/CI protocol via `dxva2.dll` (`SetMonitorBrightness`), a standard capability of virtually every HDMI/DisplayPort/DVI/VGA monitor.

On startup it probes both channels and applies changes to every monitor it finds. Brightness writes run on a background worker so the UI never stalls on slow DDC replies.

## Build from source

No SDK or Visual Studio required — the script uses the C# compiler that ships inside Windows (.NET Framework 4.x):

```
build.cmd
```

Output: `dist\GlowKey.exe`. Source is plain C# 5 / WinForms (`src/*.cs`, 9 files), so it also opens and builds anywhere Visual Studio / .NET SDK can reach Framework 4.8.

## Troubleshooting

| Symptom | Fix |
|---|---|
| No tray icon | Click the `^` hidden-icons button in the taskbar; drag the sun icon out to pin it |
| Hotkey "registration failed" | The combo is taken by another app — pick a different one in Settings |
| External monitor doesn't dim | Enable **DDC/CI** in the monitor's own OSD menu, then tray menu → Rescan monitors |
| No brightness control at all | RDP sessions don't expose WMI brightness (OS limitation); some KVM/adapter chains strip DDC/CI |
| Two-letter combos like `U+G`? | Windows global hotkeys require a modifier key; bare letter chords would fire while typing, so they're intentionally not offered |

## Limitations & roadmap

- [x] v1.0 — hotkeys, tray slider, OSD, settings, auto-start, WMI + DDC/CI
- [ ] Per-monitor control (currently applies to all detected monitors)
- [ ] Localization (UI is English/Chinese mixed today)
- [ ] Optional migration to .NET 8 single-file build

## 简体中文说明

**GlowKey：全局快捷键 + 托盘滑块调节屏幕亮度，不用再进系统设置。**

- 从 [Release](../../releases) 下载 `GlowKey.exe`（约 30 KB），双击即用：无需安装、无需管理员、无任何依赖，完全本地运行。
- 默认 `Alt+↑` 调高 / `Alt+↓` 调低；可在托盘右键 → 设置里改成任意组合（如 `Ctrl+Alt+G` / `Ctrl+Alt+C`）。
- 右键托盘图标：菜单内嵌滑块；左键托盘图标：弹出竖直滑条浮窗；调节时屏幕底部显示百分比提示。
- 笔记本内置屏（WMI）和外接显示器（DDC/CI）都能调，启动时自动探测，同时应用到所有显示器。
- 托盘图标默认在“隐藏图标”区（点任务栏 `^` 展开，可拖出固定）；外接屏不可调时请检查显示器 OSD 菜单里的 DDC/CI 是否开启。
- 编译：双击 `build.cmd`（用 Windows 自带编译器，无需安装任何 SDK）。

## License

[MIT](LICENSE) © 2026 zzuu080603
