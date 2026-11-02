using System.Runtime.InteropServices;

namespace BreakReminder;

public sealed class KeyboardBlocker : IDisposable
{
    private const int LowLevelKeyboardHookType = 13;
    private const uint AltDownFlag = 0x20;
    private const int ControlVirtualKeyCode = 0x11;

    private delegate IntPtr LowLevelKeyboardCallback(int hookCode, IntPtr keyboardMessage, IntPtr keyboardHookInfoPointer);

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardHookInfo
    {
        public uint VirtualKeyCode;
        public uint ScanCode;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    private readonly LowLevelKeyboardCallback keyboardHookCallback;
    private IntPtr keyboardHookHandle = IntPtr.Zero;

    public KeyboardBlocker()
    {
        keyboardHookCallback = OnKeyboardEvent;
    }

    public void Start()
    {
        if (keyboardHookHandle != IntPtr.Zero)
        {
            return;
        }

        keyboardHookHandle = SetWindowsHookEx(LowLevelKeyboardHookType, keyboardHookCallback, GetModuleHandle(null), 0);
    }

    public void Stop()
    {
        if (keyboardHookHandle == IntPtr.Zero)
        {
            return;
        }

        UnhookWindowsHookEx(keyboardHookHandle);
        keyboardHookHandle = IntPtr.Zero;
    }

    public void Dispose()
    {
        Stop();
    }

    private IntPtr OnKeyboardEvent(int hookCode, IntPtr keyboardMessage, IntPtr keyboardHookInfoPointer)
    {
        if (hookCode >= 0 && IsBlockedKey(Marshal.PtrToStructure<KeyboardHookInfo>(keyboardHookInfoPointer)))
        {
            return new IntPtr(1);
        }

        return CallNextHookEx(keyboardHookHandle, hookCode, keyboardMessage, keyboardHookInfoPointer);
    }

    private static bool IsBlockedKey(KeyboardHookInfo keyboardHookInfo)
    {
        Keys pressedKey = (Keys)keyboardHookInfo.VirtualKeyCode;
        bool isAltDown = (keyboardHookInfo.Flags & AltDownFlag) != 0;
        bool isControlDown = (GetAsyncKeyState(ControlVirtualKeyCode) & 0x8000) != 0;

        return (pressedKey is Keys.LWin or Keys.RWin or Keys.Apps)
            || (isAltDown && pressedKey is Keys.Tab or Keys.Escape or Keys.F4)
            || (isControlDown && pressedKey is Keys.Escape);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int hookType, LowLevelKeyboardCallback hookCallback, IntPtr moduleHandle, uint threadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hookHandle);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hookHandle, int hookCode, IntPtr keyboardMessage, IntPtr keyboardHookInfoPointer);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKeyCode);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? moduleName);
}
