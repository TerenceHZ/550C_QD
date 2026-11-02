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

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        RegisterAutoStart();

        // one process only: a second double-click brings the existing window forward
        _mutex = new Mutex(true, "550C-Hub-Launchpad", out var createdNew);
        if (!createdNew)
        {
            var h = FindWindow(null, "550C Archive");
            if (h != IntPtr.Zero)
            {
                // do NOT call ShowWindow here: restoring an already visible window
                // makes DWM rebuild its surface and the content turns black.
                SetForegroundWindow(h);
            }
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
