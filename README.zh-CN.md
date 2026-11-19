# GlowKey

**用全局快捷键和托盘滑块调节屏幕亮度——不用再钻进系统设置里找亮度。**

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![Platform: Windows 10/11](https://img.shields.io/badge/Platform-Windows%2010%2F11-blue)]()
[![Dependencies: None](https://img.shields.io/badge/Dependencies-None-success)]()

[English](README.md) | **简体中文**

![OSD 效果预览](docs/osd-screenshot.png)

*按下快捷键时屏幕上弹出的亮度提示。*

## 为什么做这个

在 Windows 上调屏幕亮度，通常得：打开设置 → 系统 → 显示 → 找到滑块。GlowKey 把它变成一个按键的事——而且**台式机外接显示器也能调**，而那正是 Windows 往往完全不提供软件亮度调节的地方。

## 功能

| 功能 | 说明 |
|---|---|
| 🌒 全局快捷键 | 默认 `Alt+↑` 调高 / `Alt+↓` 调低，每次 10%。完全可自定义：任意 `Ctrl/Alt/Shift/Win` + 按键组合 |
| 🎚️ 托盘滑块 | 右键菜单内嵌滑块；左键点托盘图标弹出音量风格的竖直滑条浮窗 |
| 🔔 OSD 提示 | 深色圆角浮窗 + 太阳图标 + 当前百分比，自动消失（可关闭） |
| 🖥️ 双通道通用 | 笔记本内置屏（WMI）**和** 外接显示器 DDC/CI（HDMI/DP/DVI/VGA）都能调 |
| 🔃 开机自启 | 可选，写入当前用户注册表 Run 键——不需要管理员权限 |
| 📦 小巧便携 | 单个约 30 KB 的 `.exe`。免安装、免下载运行时、免管理员、不联网 |

适用于 Windows 10 / 11。所有功能完全本地运行——GlowKey 永远不碰网络。

## 安装

1. 从最新 [Release](../../releases) 下载 **`GlowKey.exe`**（约 30 KB）。
2. 双击运行。托盘出现金色太阳图标（如果没看到，点任务栏 `^` 展开隐藏图标，可把它拖到任务栏固定）。
3. 可选：托盘右键 → **开机自动启动**。

> 小贴士：开启自启前先把 exe 放到固定目录（如 `C:\Tools\GlowKey\`）——自启注册的是 exe 的当前路径。

## 使用

- **快捷键** —— `Alt+↑` / `Alt+↓` 调节亮度。想换组合（比如 `Ctrl+Alt+G` / `Ctrl+Alt+C`）：托盘右键 → **设置** → 点击输入框 → 直接按下组合键 → 保存。
- **托盘菜单** —— 右键托盘图标：顶部是亮度滑块，往下是设置、开机自启、重新扫描显示器、退出。
- **浮窗滑条** —— 左键点托盘图标。
- 设置保存在 `%APPDATA%\GlowKey\settings.json`，重启后依然生效。

## 工作原理

GlowKey 同时使用 Windows 提供的两条软件调亮度通道，既不需要提权也不需要装驱动：

1. **内置屏** —— `root\wmi` 命名空间的 `WmiSetBrightness`（`WmiMonitorBrightnessMethods`），和 Windows 设置在笔记本上用的是同一个接口。
2. **外接显示器** —— 通过 `dxva2.dll`（`SetMonitorBrightness`）走 DDC/CI 协议，几乎所有 HDMI/DisplayPort/DVI/VGA 显示器的标准能力。

启动时自动探测两条通道，调节时应用到检测到的每一台显示器。亮度写入在后台线程执行，界面永不被慢速 DDC 响应卡住。

## 从源码构建

不需要安装 SDK 或 Visual Studio——脚本直接用 Windows 内置的 C# 编译器（.NET Framework 4.x）：

```
build.cmd
```

产物：`dist\GlowKey.exe`。源码是纯 C# 5 / WinForms（`src/*.cs`，9 个文件），用 Visual Studio / .NET SDK 也能打开编译（目标 Framework 4.8）。

## 故障排查

| 症状 | 解决 |
|---|---|
| 托盘没有图标 | 点任务栏 `^` 隐藏图标按钮；把太阳图标拖出来固定 |
| 热键"注册失败" | 该组合被其他软件占用了——在设置里换一个 |
| 外接显示器调不动 | 在显示器自己的 OSD 菜单里开启 **DDC/CI**，然后托盘菜单 → 重新扫描显示器 |
| 完全没有亮度控制 | 远程桌面不提供 WMI 亮度接口（系统限制）；部分 KVM/转接链路会剥掉 DDC/CI |
| 想要 `U+G` 这种双字母组合？ | Windows 全局热键必须带修饰键；纯字母组合会在打字时误触发，因此刻意不提供 |

## 已知限制与路线图

- [x] v1.0 —— 快捷键、托盘滑块、OSD、设置、开机自启、WMI + DDC/CI
- [ ] 按显示器单独控制（当前应用到所有检测到的显示器）
- [ ] 多语言（当前界面中英文混合）
- [ ] 可选迁移到 .NET 8 单文件构建

## English

[Read this README in English](README.md).

## 许可

[MIT](LICENSE) © 2026 zzuu080603
