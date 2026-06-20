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

| File | Description |
|------|-------------|
| `ScreenManagement-vX.Y.Z-win-x64-full.zip` | Self-contained, no .NET Runtime required, larger size |
| `ScreenManagement-vX.Y.Z-win-x64.zip` | Framework-dependent, smaller size, requires [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) |

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
- Windows 10/11
- Visual Studio 2026+ or VS Code with C# extension

### Build

```bash
dotnet restore
dotnet build
dotnet run --project src/ScreenManagement.UI
dotnet test src/ScreenManagement.Test/ScreenManagement.Test.csproj
dotnet publish src/ScreenManagement.UI/ScreenManagement.UI.csproj --configuration Release --runtime win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true
```

### CI/CD

- Pushes to `main` and pull requests targeting `main` run build and unit tests automatically.
- Pushing a `v*` tag (for example, `v1.0` or `v0.0.1`) triggers test, publish, and GitHub Release creation automatically.

## 📄 License

MIT License
