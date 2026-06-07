# Screen Management 🖥

> A lightweight Windows display management utility for quick switching display modes and HDR states via global hotkeys and system tray.

## ✨ Features

- 🖥 **One-click display mode switching**: PC screen only / Duplicate / Extend / Second screen only
- 🎨 **HDR management**: Toggle HDR on/off for HDR-capable displays
- ⌨ **Global hotkeys**: Customizable keyboard shortcuts
- 🔗 **Composite actions**: Execute multiple operations with one shortcut
- 📌 **System tray**: Real-time status icon with context menu
- 🚀 **Auto-start**: Start minimized to system tray

## 📋 Requirements

- Windows 10 2004+ / Windows 11
- .NET 10 Desktop Runtime (framework-dependent version)
- x64 architecture

## 📥 Installation

Download the latest version from [Releases](https://github.com/qiuhaotc/ScreenManagement/releases).

## ⌨ Default Hotkeys

| Shortcut | Action |
|----------|--------|
| Ctrl+Shift+F1 | PC screen only |
| Ctrl+Shift+F2 | Extend mode |
| Ctrl+Shift+F3 | Second screen only |

Hotkeys can be customized in the settings page.

## 🛠 Development

### Prerequisites

- .NET 10 SDK
- MAUI workload: `dotnet workload install maui-windows`

### Build

```bash
dotnet restore
dotnet build
dotnet run --project src/ScreenManagement.UI
dotnet test
```

## 📄 License

MIT License
