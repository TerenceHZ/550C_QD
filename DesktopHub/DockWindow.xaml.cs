using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DesktopHub;

public partial class DockWindow : Window
{
    private const int MaxItems = 20;
    private static readonly string DockConfig = @"D:\550c-project\desktop-hub\dock.json";
    private static readonly string HubDir = @"D:\550c-project\desktop-hub\items";

    private string? _dragSrc;
    private Point _dragStartPoint;
    private bool _isDragging;

    /// <summary>Screen point (DIP) of the LAUNCHPAD button centre, used by the open/close animation.</summary>
    public static Point? HubButtonScreen;

    public DockWindow()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            PlaceAboveTaskbar();
            LoadDock();
            DrawDockStars();
            StartGlow();
            StartScan();
            EnableAcrylic();
            UpdateHubButtonScreen();
            SetupDropTarget();
        };
        LayoutUpdated += (_, _) => UpdateHubButtonScreen();
        SystemParameters.StaticPropertyChanged += (_, _) => PlaceAboveTaskbar();
    }

    private void UpdateHubButtonScreen()
    {
        try
        {
            if (HubBtn == null || !HubBtn.IsVisible) return;
            var p = HubBtn.TranslatePoint(new Point(HubBtn.ActualWidth / 2, HubBtn.ActualHeight / 2), this);
            HubButtonScreen = PointToScreen(p);
        }
        catch { }
    }

    private bool _resizing;
    private double _resizeStartX;
    private double _resizeStartWidth;

    private void ResizeHandle_MouseDown(object sender, MouseButtonEventArgs e)
    {
        _resizing = true;
        _resizeStartX = e.GetPosition(this).X;
        _resizeStartWidth = Width;
        ResizeHandle.CaptureMouse();
        e.Handled = true;
    }

    private void ResizeHandle_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_resizing) return;
        var dx = e.GetPosition(this).X - _resizeStartX;
        Width = Math.Clamp(_resizeStartWidth + dx, 420, 3400);
        Left = (SystemParameters.WorkArea.Width - Width) / 2 + SystemParameters.WorkArea.Left;
    }

    private void ResizeHandle_MouseUp(object sender, MouseButtonEventArgs e)
    {
        _resizing = false;
        ResizeHandle.ReleaseMouseCapture();
        e.Handled = true;
    }

    private void LaunchBorder_MouseEnter(object sender, MouseEventArgs e)
    {
        LaunchBorder.Background = new SolidColorBrush(Color.FromArgb(0x3D, 0xE8, 0xB0, 0x4A));
        LaunchBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(0xCC, 0xE8, 0xB0, 0x4A));
        LaunchSweep.Opacity = 1;
        var anim = new System.Windows.Media.Animation.DoubleAnimation(
            -80, 150, TimeSpan.FromSeconds(1.1))
        {
            RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever
        };
        LaunchSweepT.BeginAnimation(TranslateTransform.XProperty, anim);
    }

    private void LaunchBorder_MouseLeave(object sender, MouseEventArgs e)
    {
        LaunchBorder.Background = new SolidColorBrush(Color.FromArgb(0x0F, 0xE8, 0xB0, 0x4A));
        LaunchBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(0x44, 0xE8, 0xB0, 0x4A));
        LaunchSweep.Opacity = 0;
        LaunchSweepT.BeginAnimation(TranslateTransform.XProperty, null);
    }

    private void RootGrid_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            try { DragMove(); } catch { }
        }
    }

    // ---------- macOS-style magnification ----------

    private void DockPanel_MouseMove(object sender, MouseEventArgs e)
    {
        var pos = e.GetPosition(DockPanel);
        foreach (var child in DockPanel.Children.OfType<Border>())
        {
            var center = child.TranslatePoint(new Point(child.ActualWidth / 2, 0), DockPanel).X;
            var dist = Math.Abs(pos.X - center);
            const double influence = 110;
            var t = Math.Clamp(1 - dist / influence, 0, 1);
            var eased = t * t * (3 - 2 * t);
            AnimateScaleTo(child, 1 + 0.55 * eased);
        }
    }

    private void ResetScales()
    {
        foreach (var child in DockPanel.Children.OfType<Border>())
            AnimateScaleTo(child, 1.0);
    }

    private static void AnimateScaleTo(Border b, double target)
    {
        if (b.RenderTransform is not ScaleTransform st) return;
        if (Math.Abs(st.ScaleX - target) < 0.01) return;
        var ease = new System.Windows.Media.Animation.CubicEase
        {
            EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut
        };
        var dur = TimeSpan.FromMilliseconds(110);
        var ax = new System.Windows.Media.Animation.DoubleAnimation(target, dur) { EasingFunction = ease };
        var ay = new System.Windows.Media.Animation.DoubleAnimation(target, dur) { EasingFunction = ease };
        st.BeginAnimation(ScaleTransform.ScaleXProperty, ax);
        st.BeginAnimation(ScaleTransform.ScaleYProperty, ay);
    }

    private void PlaceAboveTaskbar()
    {
        var wa = SystemParameters.WorkArea;
        Width = Math.Min(800, wa.Width * 0.44);
        Height = 74;
        Left = wa.Left + (wa.Width - Width) / 2;
        Top = wa.Top + wa.Height - Height - 64;
    }

    // ---------- config ----------

    private List<string> ReadConfig()
    {
        try
        {
            if (File.Exists(DockConfig))
            {
                var json = File.ReadAllText(DockConfig);
                var list = JsonSerializer.Deserialize<List<string>>(json);
                if (list != null) return list.Where(File.Exists).ToList();
            }
        }
        catch { }
        return new List<string>();
    }

    private void WriteConfig(List<string> items)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(DockConfig)!);
            File.WriteAllText(DockConfig, JsonSerializer.Serialize(items, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }

    // ---------- icon ----------

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string szTypeName;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes,
        ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool DestroyIcon(IntPtr handle);

    private const uint SHGFI_ICON = 0x100;
    private const uint SHGFI_LARGEICON = 0x0;

    private static ImageSource? GetIcon(string path)
    {
        try
        {
            var shfi = new SHFILEINFO();
            var ok = SHGetFileInfo(path, 0, ref shfi, (uint)Marshal.SizeOf(shfi), SHGFI_ICON | SHGFI_LARGEICON);
            if (ok != IntPtr.Zero && shfi.hIcon != IntPtr.Zero)
            {
                var bs = Imaging.CreateBitmapSourceFromHIcon(shfi.hIcon, Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
                DestroyIcon(shfi.hIcon);
                return bs;
            }
        }
        catch { }
        return null;
    }

    // ---------- render ----------

    private void LoadDock()
    {
        DockPanel.Children.Clear();
        var items = ReadConfig();

        foreach (var path in items.Take(MaxItems))
            DockPanel.Children.Add(BuildDockTile(path));

        BuildContextMenu();
    }

    private FrameworkElement BuildDockTile(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        var img = new Image
        {
            Width = 30, Height = 30, Source = GetIcon(path), Stretch = Stretch.Uniform
        };
        var border = new Border
        {
            Width = 40, Height = 40,
            Margin = new Thickness(10, 0, 10, 0),
            Background = Brushes.Transparent,
            CornerRadius = new CornerRadius(2),
            Child = img,
            ToolTip = name,
            Cursor = Cursors.Hand
        };
        border.RenderTransformOrigin = new Point(0.5, 1);
        border.RenderTransform = new ScaleTransform(1, 1);
        border.MouseEnter += (_, _) =>
        {
            border.Background = new SolidColorBrush(Color.FromArgb(0x33, 0xE8, 0xB0, 0x4A));
            border.Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = Color.FromRgb(0xE8, 0xB0, 0x4A),
                BlurRadius = 18,
                ShadowDepth = 0,
                Opacity = 0.9
            };
            Panel.SetZIndex(border, 10);
            AnimateScaleTo(border, 1.32);
        };
        border.MouseLeave += (_, _) =>
        {
            border.Background = Brushes.Transparent;
            border.Effect = null;
            Panel.SetZIndex(border, 0);
            AnimateScaleTo(border, 1.0);
        };
        border.MouseLeftButtonDown += (_, e) =>
        {
            _dragSrc = path;
            _dragStartPoint = e.GetPosition(DockPanel);
            _isDragging = false;
        };
        border.MouseMove += (_, e) =>
        {
            if (e.LeftButton != MouseButtonState.Pressed || _dragSrc == null) return;
            var d = e.GetPosition(DockPanel) - _dragStartPoint;
            if (Math.Abs(d.X) > 12) _isDragging = true;
        };
        border.MouseLeftButtonUp += (_, e) =>
        {
            if (_isDragging && _dragSrc != null)
            {
                var x = e.GetPosition(DockPanel).X;
                var src = _dragSrc;
                _isDragging = false;
                _dragSrc = null;
                if (ReorderDock(src, x)) return;
            }
            _isDragging = false;
            _dragSrc = null;
            Launch(path);
        };

        var menu = new ContextMenu();
        var remove = new MenuItem { Header = "从导航栏移除" };
        remove.Click += (_, _) =>
        {
            var items = ReadConfig();
            items.Remove(path);
            WriteConfig(items);
            LoadDock();
        };
        menu.Items.Add(remove);
        border.ContextMenu = menu;
        return border;
    }

    private void BuildContextMenu()
    {
        var menu = new ContextMenu();
        var add = new MenuItem { Header = "添加图标…" };
        add.Click += (_, _) => AddIcons();
        var manage = new MenuItem { Header = "打开收纳区" };
        manage.Click += (_, _) => OpenHub();
        var clear = new MenuItem { Header = "清空导航栏" };
        clear.Click += (_, _) =>
        {
            WriteConfig(new List<string>());
            LoadDock();
        };
        var exit = new MenuItem { Header = "退出 550C Hub" };
        exit.Click += (_, _) => Application.Current.Shutdown();
        menu.Items.Add(add);
        menu.Items.Add(manage);
        menu.Items.Add(new Separator());
        menu.Items.Add(clear);
        menu.Items.Add(new Separator());
        menu.Items.Add(exit);
        DockPanel.ContextMenu = menu;
    }

    private void AddIcons()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "选择要放进导航栏的程序",
            Filter = "快捷方式或程序|*.lnk;*.exe;*.url|所有文件|*.*",
            Multiselect = true
        };
        if (dlg.ShowDialog() != true) return;

        var items = ReadConfig();
        foreach (var f in dlg.FileNames)
        {
            if (!items.Contains(f, StringComparer.OrdinalIgnoreCase)) items.Add(f);
        }
        WriteConfig(items.Take(MaxItems).ToList());
        LoadDock();
    }

    // ---------- 550C HUD visuals ----------

    private static readonly string[] GLYPH5 =
    {
        "01111", "10000", "10000", "11110", "00001", "00001", "11110"
    };
    private static readonly string[] GLYPH0 =
    {
        "01110", "10001", "10001", "10001", "10001", "10001", "01110"
    };
    private static readonly string[] GLYPHC =
    {
        "01110", "10001", "10000", "10000", "10000", "10001", "01110"
    };

    private static Color Lerp(Color a, Color b, double t)
    {
        t = Math.Clamp(t, 0, 1);
        return Color.FromRgb(
            (byte)(a.R + (b.R - a.R) * t),
            (byte)(a.G + (b.G - a.G) * t),
            (byte)(a.B + (b.B - a.B) * t));
    }

    private void StartScan()
    {
        try
        {
            var sb = (System.Windows.Media.Animation.Storyboard)FindResource("ScanAnim");
            sb.Begin(this);
        }
        catch { }
    }

    // ---------- DWM acrylic ----------

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowCompositionAttributeData
    {
        public int Attribute;
        public IntPtr Data;
        public int SizeOfData;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AccentPolicy
    {
        public int AccentState;
        public int AccentFlags;
        public int GradientColor;
        public int AnimationId;
    }

    [DllImport("user32.dll")]
    private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

    private void EnableAcrylic()
    {
        try
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            ApplyAccent(hwnd, 4, unchecked((int)0x9906070A));
        }
        catch { }
    }

    /// <summary>Give the Windows taskbar the same translucent acrylic treatment.</summary>
    private static void ApplyTaskbarAcrylic()
    {
        try
        {
            var tray = FindWindow("Shell_TrayWnd", null);
            if (tray != IntPtr.Zero)
                ApplyAccent(tray, 4, unchecked((int)0x9908070A));
            var notify = FindWindow("NotifyIconOverflowWindow", null);
            if (notify != IntPtr.Zero)
                ApplyAccent(notify, 4, unchecked((int)0x9908070A));
        }
        catch { }
    }

    private static void ApplyAccent(IntPtr hwnd, int state, int color)
    {
        var accent = new AccentPolicy
        {
            AccentState = state,
            AccentFlags = 2,
            GradientColor = color,
            AnimationId = 0
        };
        var size = Marshal.SizeOf(accent);
        var ptr = Marshal.AllocHGlobal(size);
        Marshal.StructureToPtr(accent, ptr, false);
        var data = new WindowCompositionAttributeData
        {
            Attribute = 19,
            Data = ptr,
            SizeOfData = size
        };
        SetWindowCompositionAttribute(hwnd, ref data);
        Marshal.FreeHGlobal(ptr);
    }

    private void DrawDockStars()
    {
        DockStars.Children.Clear();
        var rng = new Random(77);
        var w = ActualWidth;
        var h = ActualHeight;
        if (w <= 0 || h <= 0) return;

        for (var i = 0; i < 90; i++)
        {
            var size = rng.NextDouble() < 0.8 ? 1.0 : 1.6;
            var alpha = (byte)rng.Next(12, 80);
            var dot = new System.Windows.Shapes.Rectangle
            {
                Width = size,
                Height = size,
                Fill = new SolidColorBrush(Color.FromArgb(alpha, 0xE8, 0xB0, 0x4A)),
                IsHitTestVisible = false
            };
            Canvas.SetLeft(dot, rng.NextDouble() * w);
            Canvas.SetTop(dot, rng.NextDouble() * h);
            DockStars.Children.Add(dot);
        }
    }

    private void StartGlow()
    {
        try
        {
            var sb = (System.Windows.Media.Animation.Storyboard)FindResource("GlowAnim");
            sb.Begin(this);
        }
        catch { }
    }

    // ---------- accept dropped shortcuts ----------

    private void SetupDropTarget()
    {
        RootGrid.AllowDrop = true;
        RootGrid.DragOver += (_, e) =>
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
                e.Handled = true;
            }
        };
        RootGrid.Drop += (_, e) =>
        {
            if (e.Data.GetData(DataFormats.FileDrop) is not string[] files) return;
            var items = ReadConfig();
            var added = 0;
            foreach (var f in files)
            {
                try
                {
                    if (Directory.Exists(f)) continue;
                    var ext = Path.GetExtension(f).ToLowerInvariant();
                    if (ext is not (".lnk" or ".url" or ".exe")) continue;

                    var target = f;
                    if (!f.StartsWith(HubDir, StringComparison.OrdinalIgnoreCase))
                    {
                        Directory.CreateDirectory(HubDir);
                        target = Path.Combine(HubDir, Path.GetFileName(f));
                        if (File.Exists(target))
                            target = Path.Combine(HubDir, Path.GetFileNameWithoutExtension(f) + "_" +
                                Guid.NewGuid().ToString("N")[..4] + Path.GetExtension(f));
                        File.Copy(f, target, false);
                    }
                    if (!items.Contains(target, StringComparer.OrdinalIgnoreCase))
                    {
                        items.Add(target);
                        added++;
                    }
                    // move: remove from the desktop after copying in
                    try
                    {
                        var desk = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                        var pub = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
                        if (f.StartsWith(desk, StringComparison.OrdinalIgnoreCase) ||
                            f.StartsWith(pub, StringComparison.OrdinalIgnoreCase))
                            File.Delete(f);
                    }
                    catch { }
                }
                catch { }
            }
            if (added > 0)
            {
                WriteConfig(items);
                LoadDock();
            }
            e.Handled = true;
        };
    }

    private static void AnimateScale(Border b, double target)
    {
        if (b.RenderTransform is not ScaleTransform st) return;
        var ease = new System.Windows.Media.Animation.CubicEase
        {
            EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut
        };
        var a = new System.Windows.Media.Animation.DoubleAnimation(target, TimeSpan.FromMilliseconds(150))
        {
            EasingFunction = ease
        };
        st.BeginAnimation(ScaleTransform.ScaleXProperty, a);
        st.BeginAnimation(ScaleTransform.ScaleYProperty, a);
    }

    private bool ReorderDock(string src, double dropX)
    {
        var items = ReadConfig();
        var from = items.IndexOf(src);
        if (from < 0) return false;

        var target = -1;
        for (var i = 0; i < DockPanel.Children.Count; i++)
        {
            if (DockPanel.Children[i] is not Border b) continue;
            var pt = b.TranslatePoint(new Point(0, 0), DockPanel);
            if (dropX >= pt.X && dropX <= pt.X + b.ActualWidth)
            {
                target = i;
                break;
            }
        }
        if (target < 0 || target >= items.Count || target == from) return false;

        var moving = items[from];
        items.RemoveAt(from);
        items.Insert(target, moving);
        WriteConfig(items);
        LoadDock();
        return true;
    }

    private static void Launch(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(path) ?? string.Empty
            });
        }
        catch { }
    }

    private void OpenHub()
    {
        if (Application.Current.MainWindow is MainWindow mw)
        {
            if (mw.IsVisible && mw.WindowState != WindowState.Minimized)
            {
                mw.Activate();
                return;
            }
            mw.WindowState = WindowState.Normal;
            mw.ShowWithAnimation();
        }
    }

    private void HubBtn_Click(object sender, RoutedEventArgs e) => OpenHub();

    private void HubBtn_MouseEnter(object sender, MouseEventArgs e)
    {
        if (sender is Button b) b.Foreground = new SolidColorBrush(Color.FromRgb(0xE8, 0xB0, 0x4A));
    }

    private void HubBtn_MouseLeave(object sender, MouseEventArgs e)
    {
        if (sender is Button b) b.Foreground = new SolidColorBrush(Color.FromRgb(0x8A, 0x6E, 0x3C));
    }
}
