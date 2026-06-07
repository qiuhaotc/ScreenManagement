namespace ScreenManagement.Business.Models;

/// <summary>修饰键标志（对应 Windows ModifierKeys）</summary>
[Flags]
public enum ModifierKeys : uint
{
    None = 0,
    Alt = 0x0001,
    Control = 0x0002,
    Shift = 0x0004,
    Windows = 0x0008,
    NoRepeat = 0x4000
}
