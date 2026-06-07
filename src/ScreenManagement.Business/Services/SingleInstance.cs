namespace ScreenManagement.Business.Services;

/// <summary>单实例检测</summary>
public static class SingleInstance
{
    private static Mutex? _mutex;
    private const string MutexName = @"Global\ScreenManagement_SingleInstance_9A8B7C6D";

    public static bool TryAcquire()
    {
        _mutex = new Mutex(true, MutexName, out bool createdNew);
        return createdNew;
    }

    public static void Release()
    {
        _mutex?.ReleaseMutex();
        _mutex?.Dispose();
        _mutex = null;
    }

    public static IntPtr FindExistingWindow()
    {
        // 通过窗口标题查找已有实例
        return Native.NativeMethods.FindWindow(null, "Screen Management");
    }
}
