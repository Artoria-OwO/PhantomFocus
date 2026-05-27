using System.Windows.Forms;

namespace PhantomFocus;

public enum FocusMode
{
    FakeFocus,
    ForceForeground
}

internal sealed class FocusKeeper : IDisposable
{
    private readonly System.Windows.Forms.Timer _fakeTimer = new() { Interval = 250 };
    private NativeMethods.WinEventProc? _winEventDelegate;
    private IntPtr _winEventHook = IntPtr.Zero;
    private IntPtr _target;
    private FocusMode _mode;
    private bool _running;

    public event Action<string>? Log;

    public bool IsRunning => _running;
    public IntPtr Target => _target;
    public FocusMode Mode => _mode;

    public FocusKeeper()
    {
        _fakeTimer.Tick += (_, _) => PumpFakeFocus();
    }

    public void Start(IntPtr hWnd, FocusMode mode)
    {
        Stop();
        if (!NativeMethods.IsWindow(hWnd))
        {
            Log?.Invoke("Target window is no longer valid.");
            return;
        }

        _target = hWnd;
        _mode = mode;
        _running = true;

        if (NativeMethods.IsIconic(hWnd))
        {
            NativeMethods.ShowWindow(hWnd, NativeMethods.SW_RESTORE);
        }

        // Initial real activation so the game starts in a good state
        BringToForeground(hWnd);

        if (mode == FocusMode.FakeFocus)
        {
            // First burst of fake-activation messages, then keep nudging on a timer
            PumpFakeFocus();
            _fakeTimer.Start();
            Log?.Invoke($"[FakeFocus] Started. The target receives synthetic activation messages every {_fakeTimer.Interval} ms — switch to other apps freely.");
        }
        else
        {
            _winEventDelegate = OnForegroundChanged;
            _winEventHook = NativeMethods.SetWinEventHook(
                NativeMethods.EVENT_SYSTEM_FOREGROUND,
                NativeMethods.EVENT_SYSTEM_FOREGROUND,
                IntPtr.Zero,
                _winEventDelegate,
                0, 0,
                NativeMethods.WINEVENT_OUTOFCONTEXT | NativeMethods.WINEVENT_SKIPOWNPROCESS);
            Log?.Invoke("[ForceForeground] Started. Any other window that grabs focus will be pushed back instantly.");
        }
    }

    public void Stop()
    {
        if (!_running) return;
        _running = false;
        _fakeTimer.Stop();
        if (_winEventHook != IntPtr.Zero)
        {
            NativeMethods.UnhookWinEvent(_winEventHook);
            _winEventHook = IntPtr.Zero;
        }
        _winEventDelegate = null;
        _target = IntPtr.Zero;
        Log?.Invoke("Stopped.");
    }

    private void PumpFakeFocus()
    {
        if (_target == IntPtr.Zero) return;
        if (!NativeMethods.IsWindow(_target))
        {
            Log?.Invoke("Target window closed — stopping.");
            Stop();
            return;
        }

        // wParam packing for WM_ACTIVATE: LOWORD=state, HIWORD=minimized
        IntPtr wActive = (IntPtr)NativeMethods.WA_ACTIVE;
        IntPtr wTrue = (IntPtr)1;

        // Tell the target "you are active" — it has no idea another window owns the real foreground
        NativeMethods.PostMessage(_target, NativeMethods.WM_ACTIVATEAPP, wTrue, IntPtr.Zero);
        NativeMethods.PostMessage(_target, NativeMethods.WM_NCACTIVATE, wTrue, IntPtr.Zero);
        NativeMethods.PostMessage(_target, NativeMethods.WM_ACTIVATE, wActive, IntPtr.Zero);
        NativeMethods.PostMessage(_target, NativeMethods.WM_SETFOCUS, IntPtr.Zero, IntPtr.Zero);
    }

    private void OnForegroundChanged(IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
        int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        if (!_running || _target == IntPtr.Zero) return;
        if (hwnd == _target) return;
        if (hwnd == IntPtr.Zero) return;

        // The new foreground window might be a child/owned popup of the target — leave it alone
        // (e.g. game's own dialogs). Cheap check: same process.
        NativeMethods.GetWindowThreadProcessId(hwnd, out uint newPid);
        NativeMethods.GetWindowThreadProcessId(_target, out uint targetPid);
        if (newPid == targetPid) return;

        BringToForeground(_target);
    }

    /// <summary>
    /// SetForegroundWindow alone is blocked by Windows' foreground lock unless our thread
    /// "owns" the foreground. The standard workaround is to attach our input thread to the
    /// current foreground thread, or to inject an Alt key tap which Windows treats as user
    /// activity and resets the lock.
    /// </summary>
    private static void BringToForeground(IntPtr hWnd)
    {
        IntPtr fg = NativeMethods.GetForegroundWindow();
        uint fgThread = NativeMethods.GetWindowThreadProcessId(fg, out _);
        uint thisThread = NativeMethods.GetCurrentThreadId();

        // Alt-key tap to defeat the foreground lock
        NativeMethods.keybd_event(NativeMethods.VK_MENU, 0, 0, UIntPtr.Zero);
        NativeMethods.keybd_event(NativeMethods.VK_MENU, 0, NativeMethods.KEYEVENTF_KEYUP, UIntPtr.Zero);

        bool attached = false;
        if (fgThread != 0 && fgThread != thisThread)
        {
            attached = NativeMethods.AttachThreadInput(thisThread, fgThread, true);
        }

        NativeMethods.BringWindowToTop(hWnd);
        NativeMethods.SetForegroundWindow(hWnd);

        if (attached)
        {
            NativeMethods.AttachThreadInput(thisThread, fgThread, false);
        }
    }

    public void Dispose()
    {
        Stop();
        _fakeTimer.Dispose();
    }
}
