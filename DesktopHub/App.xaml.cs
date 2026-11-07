using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;

namespace DesktopHub;

public partial class App : Application
{
    private static Mutex? _mutex;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string? lpClassName, string lpWindowName);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll")]
    private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    private static extern bool BringWindowToTop(IntPtr hWnd);

    private static void ForceForeground(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero) return;

        // restore only when actually minimized (calling SW_RESTORE on a visible
        // window makes DWM rebuild the surface and the content goes black)
        if (IsIconic(hWnd)) ShowWindow(hWnd, 9);   // SW_RESTORE
        if (!IsWindowVisible(hWnd)) ShowWindow(hWnd, 5); // SW_SHOW

        SetForegroundWindow(hWnd);
        BringWindowToTop(hWnd);

        if (GetForegroundWindow() == hWnd) return;

        // Windows refuses SetForegroundWindow unless the caller owns the
        // foreground. Attaching to the current foreground thread lifts that
        // restriction for the duration of the call.
        var fg = GetForegroundWindow();
        var fgThread = fg == IntPtr.Zero ? 0 : GetWindowThreadProcessId(fg, out _);
        var self = GetCurrentThreadId();
        var attached = false;
        try
        {
            if (fgThread != 0 && fgThread != self)
                attached = AttachThreadInput(self, fgThread, true);

            SetForegroundWindow(hWnd);
            BringWindowToTop(hWnd);
        }
        finally
        {
            if (attached) AttachThreadInput(self, fgThread, false);
        }
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        RegisterAutoStart();

        // one process only
        _mutex = new Mutex(true, "550C-Hub-Launchpad", out var createdNew);
        if (!createdNew)
        {
            ForceForeground(FindWindow(null, "550C Archive"));
            Shutdown();
            return;
        }

        var hub = new MainWindow();
        MainWindow = hub;

        // let the visual tree finish building before the window is shown
        Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
        {
            hub.ShowWithAnimation();
        }));
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try { _mutex?.ReleaseMutex(); } catch { }
        _mutex?.Dispose();
        base.OnExit(e);
    }

    private static void RegisterAutoStart()
    {
        try
        {
            var exe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exe)) return;
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Run", writable: true);
            if (key == null) return;
            var current = key.GetValue("550C Hub") as string;
            var wanted = "\"" + exe + "\"";
            if (!string.Equals(current, wanted, StringComparison.OrdinalIgnoreCase))
                key.SetValue("550C Hub", wanted);
        }
        catch { }
    }
}
