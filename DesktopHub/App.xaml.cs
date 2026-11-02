using System;
using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace DesktopHub;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        RegisterAutoStart();

        var dock = new DockWindow();
        var hub = new MainWindow();
        MainWindow = hub;

        dock.Show();
        // hub stays hidden until LAUNCHPAD is pressed
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