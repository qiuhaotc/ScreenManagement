using Microsoft.Extensions.Logging;
using ScreenManagement.Business.Interfaces;
using ScreenManagement.Business.Models;
using ScreenManagement.Business.Native;
using System.Runtime.InteropServices;
using System.Collections.Concurrent;

namespace ScreenManagement.Business.Services;

/// <summary>全局快捷键管理服务</summary>
public class HotkeyService : IHotkeyService, IDisposable
{
    private readonly ILogger<HotkeyService> _logger;
    private readonly ConcurrentDictionary<int, HotkeyBinding> _registeredHotkeys = new();
    private int _nextId = 1;
    private IntPtr _hwnd;
    private bool _disposed;

    public HotkeyService(ILogger<HotkeyService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public event EventHandler<HotkeyTriggeredEventArgs>? HotkeyTriggered;

    /// <inheritdoc />
    public void Initialize(IntPtr hwnd)
    {
        _hwnd = hwnd;
        _logger.LogInformation("HotkeyService initialized with hwnd={Hwnd}", hwnd);
    }

    /// <inheritdoc />
    public string? RegisterHotkey(HotkeyBinding binding, IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
        {
            _logger.LogWarning("Cannot register hotkey: no window handle");
            return "窗口句柄未初始化，无法注册快捷键";
        }

        if (!binding.IsEnabled)
            return null;

        int id = Interlocked.Increment(ref _nextId);
        uint modifiers = (uint)binding.Modifiers;

        bool success = NativeMethods.RegisterHotKey(hwnd, id, modifiers, binding.Key);

        if (!success)
        {
            int error = Marshal.GetLastWin32Error();
            string message = error switch
            {
                NativeTypes.ERROR_HOTKEY_ALREADY_REGISTERED => "快捷键已被其他程序占用",
                _ => $"快捷键注册失败 (错误代码: {error})"
            };

            _logger.LogWarning("RegisterHotKey failed: {Message} (id={Id}, mod={Mod}, key={Key})",
                message, id, modifiers, binding.Key);

            Interlocked.Decrement(ref _nextId);
            return message;
        }

        _registeredHotkeys[id] = binding;
        _logger.LogInformation("Hotkey registered: id={Id}, actionType={ActionType}", id, binding.ActionType);
        return null; // 成功
    }

    /// <inheritdoc />
    public async Task RegisterAllAsync(IEnumerable<HotkeyBinding> bindings, IntPtr hwnd)
    {
        UnregisterAll();

        foreach (var binding in bindings.Where(b => b.IsEnabled))
        {
            string? error = RegisterHotkey(binding, hwnd);
            if (error != null)
            {
                _logger.LogWarning("Failed to register hotkey {Id}: {Error}", binding.Id, error);
            }
        }

        _logger.LogInformation("Registered {Count} hotkeys", _registeredHotkeys.Count);
        await Task.CompletedTask;
    }

    /// <inheritdoc />
    public void UnregisterAll()
    {
        foreach (var kvp in _registeredHotkeys)
        {
            if (_hwnd != IntPtr.Zero)
            {
                NativeMethods.UnregisterHotKey(_hwnd, kvp.Key);
            }
        }
        _registeredHotkeys.Clear();
        _logger.LogInformation("All hotkeys unregistered");
    }

    /// <inheritdoc />
    public bool IsHotkeyAvailable(ModifierKeys modifiers, uint key)
    {
        // 尝试临时注册来检测是否可用
        if (_hwnd == IntPtr.Zero)
            return false;

        int tempId = -1;
        bool success = NativeMethods.RegisterHotKey(_hwnd, tempId, (uint)modifiers, key);

        if (success)
            NativeMethods.UnregisterHotKey(_hwnd, tempId);

        return success;
    }

    /// <summary>处理 WM_HOTKEY 窗口消息（由 UI 层调用）</summary>
    public void HandleHotkeyMessage(int hotkeyId)
    {
        if (_registeredHotkeys.TryGetValue(hotkeyId, out var binding))
        {
            _logger.LogInformation("Hotkey triggered: {ActionType} (id={Id})", binding.ActionType, hotkeyId);
            HotkeyTriggered?.Invoke(this, new HotkeyTriggeredEventArgs { Binding = binding });
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        UnregisterAll();
        GC.SuppressFinalize(this);
    }
}
