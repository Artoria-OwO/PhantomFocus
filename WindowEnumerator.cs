using System.Diagnostics;
using System.Text;

namespace PhantomFocus;

public sealed record WindowInfo(IntPtr Handle, string Title, string ProcessName, uint ProcessId)
{
    public string Display => $"{Title}  —  [{ProcessName}]";
}

internal static class WindowEnumerator
{
    public static List<WindowInfo> EnumerateUserWindows()
    {
        var result = new List<WindowInfo>();
        var self = Environment.ProcessId;

        NativeMethods.EnumWindows((hWnd, _) =>
        {
            if (!NativeMethods.IsWindowVisible(hWnd)) return true;

            int len = NativeMethods.GetWindowTextLength(hWnd);
            if (len <= 0) return true;

            // Skip cloaked windows (hidden UWP / virtual desktop ghosts)
            if (NativeMethods.DwmGetWindowAttribute(hWnd, NativeMethods.DWMWA_CLOAKED, out int cloaked, sizeof(int)) == 0
                && cloaked != 0)
            {
                return true;
            }

            int ex = NativeMethods.GetWindowLong(hWnd, NativeMethods.GWL_EXSTYLE);
            bool isToolWindow = (ex & NativeMethods.WS_EX_TOOLWINDOW) != 0;
            bool isAppWindow = (ex & NativeMethods.WS_EX_APPWINDOW) != 0;
            if (isToolWindow && !isAppWindow) return true;

            var sb = new StringBuilder(len + 1);
            NativeMethods.GetWindowText(hWnd, sb, sb.Capacity);
            string title = sb.ToString();
            if (string.IsNullOrWhiteSpace(title)) return true;

            NativeMethods.GetWindowThreadProcessId(hWnd, out uint pid);
            if (pid == (uint)self) return true; // exclude ourselves

            string procName = "?";
            try
            {
                using var p = Process.GetProcessById((int)pid);
                procName = p.ProcessName;
            }
            catch { /* process exited / access denied */ }

            // Common shell ghost classes
            var cls = new StringBuilder(64);
            NativeMethods.GetClassName(hWnd, cls, cls.Capacity);
            string className = cls.ToString();
            if (className is "Windows.UI.Core.CoreWindow" or "ApplicationFrameWindow" && procName.Equals("ApplicationFrameHost", StringComparison.OrdinalIgnoreCase))
            {
                // ApplicationFrameHost hosts UWP — keep it; the title is the real app's title.
            }

            result.Add(new WindowInfo(hWnd, title, procName, pid));
            return true;
        }, IntPtr.Zero);

        return result.OrderBy(w => w.ProcessName).ThenBy(w => w.Title).ToList();
    }
}
