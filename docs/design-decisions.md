# 设计决策记录

> 记录项目中的关键技术决策及其理由

---

## 1. 使用 .NET MAUI 而非 WinForms/WPF

**决策**: 选择 .NET MAUI 作为 UI 框架

**理由**:
- MAUI 是微软新一代跨平台 UI 框架，代表未来方向
- 内置 MVVM 支持，与 CommunityToolkit.Mvvm 集成良好
- Fluent Design 风格原生支持
- 后续可扩展至 macOS 平台（显示模式切换相关功能需适配）

**备选方案**: WinForms (更简单但技术陈旧)、WPF (仅 Windows)

---

## 2. CCD API vs ChangeDisplaySettings

**决策**: 使用 CCD (Connecting and Configuring Displays) API

**理由**:
- CCD API (`SetDisplayConfig` / `QueryDisplayConfig`) 是 Windows 7+ 的现代显示配置 API
- 比传统 `ChangeDisplaySettings` 更精确、更稳定
- 支持持久化显示拓扑（`SDC_USE_DATABASE_CURRENT`）
- 原生支持高级颜色管理（HDR）

---

## 3. HDR 切换使用 DisplayConfigSetDeviceInfo

**决策**: 通过 `DISPLAYCONFIG_DEVICE_INFO_SET_ADVANCED_COLOR_STATE` 设置 HDR

**理由**:
- 这是 Windows 官方提供的 HDR 切换 API（从 Windows 10 2004 开始支持）
- 比注册表修改方案更安全、更可靠
- 无需管理员权限

**风险**: 如果在用户态下不可用，则回退到注册表方案

---

## 4. 系统托盘使用 Windows Forms NotifyIcon

**决策**: 在 MAUI 中使用 WinForms `NotifyIcon` 实现系统托盘

**理由**:
- MAUI 原生不提供系统托盘 API
- WinForms `NotifyIcon` 稳定可靠，且有完整的 ContextMenu 支持
- 通过平台特定代码桥接 MAUI Window 和 WinForms 组件

---

## 5. 使用 CommunityToolkit.Mvvm

**决策**: 选择 CommunityToolkit.Mvvm 作为 MVVM 框架

**理由**:
- 源生成器自动生成 `INotifyPropertyChanged` 代码，减少样板
- `[RelayCommand]` 自动生成 ICommand 实现
- `[ObservableProperty]` 自动生成属性变更通知
- 微软官方维护，与 .NET MAUI 高度集成

---

## 6. 单实例检测使用命名 Mutex

**决策**: 使用全局命名 Mutex 检测单实例

**理由**:
- 跨进程互斥，Windows 原生支持
- 简单可靠，无需额外依赖
- 支持查找已有实例窗口并激活

---

## 7. 配置存储使用 JSON

**决策**: 使用 `System.Text.Json` 存储配置到 `%APPDATA%`

**理由**:
- JSON 可读性强，用户可直接编辑
- `System.Text.Json` 内置于 .NET，零依赖
- 存储在 `%APPDATA%` 符合 Windows 应用规范

---

## 8. 开机自启使用注册表 Run 键

**决策**: 使用 `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` 注册自启

**理由**:
- 用户级注册表键，无需管理员权限
- Windows 原生支持，兼容性好
- 比任务计划程序更简单、更轻量
