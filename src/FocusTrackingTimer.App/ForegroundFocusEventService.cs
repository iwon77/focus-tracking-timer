using System.Runtime.InteropServices;

namespace FocusTrackingTimer.App;

internal sealed class ForegroundFocusEventService
{
    private const uint EventSystemForeground = 0x0003;
    private const uint WinEventOutOfContext = 0x0000;

    private readonly WinEventProc _callback;
    private IntPtr _hook;

    public ForegroundFocusEventService()
    {
        _callback = OnWinEvent;
    }

    public event Action<IntPtr>? ForegroundChanged;

    public void Start()
    {
        if (_hook != IntPtr.Zero)
        {
            return;
        }

        _hook = SetWinEventHook(
            EventSystemForeground,
            EventSystemForeground,
            IntPtr.Zero,
            _callback,
            0,
            0,
            WinEventOutOfContext);
    }

    public void Stop()
    {
        if (_hook == IntPtr.Zero)
        {
            return;
        }

        _ = UnhookWinEvent(_hook);
        _hook = IntPtr.Zero;
    }

    private void OnWinEvent(
        IntPtr hook,
        uint eventType,
        IntPtr windowHandle,
        int objectId,
        int childId,
        uint eventThread,
        uint eventTime)
    {
        if (eventType != EventSystemForeground || windowHandle == IntPtr.Zero)
        {
            return;
        }

        ForegroundChanged?.Invoke(windowHandle);
    }

    private delegate void WinEventProc(
        IntPtr hook,
        uint eventType,
        IntPtr windowHandle,
        int objectId,
        int childId,
        uint eventThread,
        uint eventTime);

    [DllImport("user32.dll")]
    private static extern IntPtr SetWinEventHook(
        uint eventMin,
        uint eventMax,
        IntPtr eventHookAssemblyHandle,
        WinEventProc eventProc,
        uint processId,
        uint threadId,
        uint flags);

    [DllImport("user32.dll")]
    private static extern bool UnhookWinEvent(IntPtr hook);
}
