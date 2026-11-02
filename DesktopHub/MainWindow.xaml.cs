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
using System.Windows.Threading;

namespace DesktopHub;

public partial class MainWindow : Window
{
    private static readonly string HubDir = @"D:\550c-project\desktop-hub\items";
    private static readonly string StateFile = @"D:\550c-project\desktop-hub\archive-state.json";
    private static readonly string LayoutFile = @"D:\550c-project\desktop-hub\archive-layout.json";

    private static readonly string[] GameWords = { "剑侠", "英雄联盟", "无畏契约", "WeGame", "Steam", "虎牙", "游戏" };
    private static readonly string[] ToolWords = { "WPS", "NVIDIA", "Lively", "Wallpaper", "UU", "Clash", "百度", "迅雷", "电脑管家", "DeepSeek", "KANALI", "ASUS", "Ave", "Binance", "AiCoin", "X" };
    private static readonly string[] SocialWords = { "QQ", "微信", "Telegram", "抖音", "爱奇艺", "腾讯视频", "优酷" };

    private static readonly string[] IconExts = { ".lnk", ".url", ".exe" };
    private static readonly string[] DocExts =
    {
        ".xlsx", ".xls", ".xlsm", ".csv", ".docx", ".doc", ".pdf", ".txt", ".md",
        ".pptx", ".ppt", ".png", ".jpg", ".jpeg", ".zip", ".rar", ".7z", ".json", ".xml"
    };

    private readonly List<(string Path, string Name)> _all = new();
    private List<string> _order = new();
    private string _category = "ALL";
    private double _iconSize = 40;
    private readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromSeconds(1) };
    private DispatcherTimer? _layoutSave;

    private void SaveLayoutSoon()
    {
        if (_layoutSave == null) return;
        _layoutSave.Stop();
        _layoutSave.Start();
    }

    private Point _dragStart;
    private string? _dragPath;
    private bool _droppedInternally;

    public MainWindow()
    {
        InitializeComponent();
        LoadState();
        BuildCategoryChips();
        LoadIcons();
        RestoreLayout();
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) HideWithAnimation();
        };
        Loaded += (_, _) =>
        {
            DrawStars();
            StartClock();
        };
        _clock.Tick += (_, _) => ClockText.Text = DateTime.Now.ToString("HH:mm:ss");

        // remember size / position whenever the user drags or resizes
        _layoutSave = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
        _layoutSave.Tick += (_, _) =>
        {
            _layoutSave.Stop();
            SaveLayout();
        };
        SizeChanged += (_, _) => SaveLayoutSoon();
        LocationChanged += (_, _) => SaveLayoutSoon();

        // accept shortcuts dragged in from Explorer / desktop
        IconScroll.AllowDrop = true;
        IconScroll.DragOver += (_, e) =>
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
                e.Handled = true;
            }
        };
        IconScroll.Drop += (_, e) =>
        {
            if (e.Data.GetData(DataFormats.FileDrop) is not string[] files) return;
            Directory.CreateDirectory(HubDir);
            var added = 0;
            foreach (var f in files)
            {
                try
                {
                    var name = Path.GetFileName(f.TrimEnd('\\'));
                    var target = Path.Combine(HubDir, name);
                    if (Directory.Exists(f))
                    {
                        if (Directory.Exists(target))
                            target = Path.Combine(HubDir, name + "_" + Guid.NewGuid().ToString("N")[..4]);
                        CopyDirectory(f, target);
                        added++;
                    }
                    else
                    {
                        if (File.Exists(target))
                            target = Path.Combine(HubDir, Path.GetFileNameWithoutExtension(f) + "_" +
                                Guid.NewGuid().ToString("N")[..4] + Path.GetExtension(f));
                        File.Copy(f, target, false);
                        added++;
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
                }
                catch { }
            }
            if (added > 0) LoadIcons();
            e.Handled = true;
        };
    }

    // ---------- state ----------

    private sealed class ArchiveState
    {
        public double IconSize { get; set; } = 40;
        public List<string> Order { get; set; } = new();
    }

    private void LoadState()
    {
        try
        {
            if (!File.Exists(StateFile)) return;
            var st = JsonSerializer.Deserialize<ArchiveState>(File.ReadAllText(StateFile));
            if (st == null) return;
            _iconSize = Math.Clamp(st.IconSize, 28, 64);
            _order = st.Order ?? new List<string>();
        }
        catch { }
    }

    private void SaveState()
    {
        try
        {
            var st = new ArchiveState { IconSize = _iconSize, Order = _order };
            File.WriteAllText(StateFile, JsonSerializer.Serialize(st, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }

    private sealed class LayoutState
    {
        public double Left { get; set; }
        public double Top { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
    }

    private void RestoreLayout()
    {
        try
        {
            if (!File.Exists(LayoutFile)) return;
            var l = JsonSerializer.Deserialize<LayoutState>(File.ReadAllText(LayoutFile));
            if (l == null || l.Width < 400 || l.Height < 200) return;
            Left = l.Left;
            Top = l.Top;
            Width = l.Width;
            Height = l.Height;
            WindowStartupLocation = WindowStartupLocation.Manual;
        }
        catch { }
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        try
        {
            var l = new LayoutState { Left = Left, Top = Top, Width = Width, Height = Height };
            File.WriteAllText(LayoutFile, JsonSerializer.Serialize(l, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
        // no e.Cancel: closing the window must shut the process down
    }

    private void StartClock()
    {
        ClockText.Text = DateTime.Now.ToString("HH:mm:ss");
        _clock.Start();
    }

    // ---------- category chips ----------

    private void BuildCategoryChips()
    {
        CategoryBar.Children.Clear();
        foreach (var name in new[] { "ALL", "磁盘", "文档", "图标" })
        {
            var b = new Button
            {
                Content = name,
                Tag = name,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                Padding = new Thickness(10, 3, 10, 3),
                Margin = new Thickness(0, 0, 7, 0),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x44, 0xE8, 0xB0, 0x4A)),
                Foreground = new SolidColorBrush(Color.FromArgb(0x8C, 0xE8, 0xB0, 0x4A)),
                Cursor = Cursors.Hand
            };
            b.Click += (_, _) =>
            {
                _category = (string)b.Tag;
                CategoryText.Text = _category;
                ApplyFilter(SearchBox.Text);
                RefreshChipStyles();
            };
            CategoryBar.Children.Add(b);
        }
        RefreshChipStyles();
    }

    private void RefreshChipStyles()
    {
        foreach (var b in CategoryBar.Children.OfType<Button>())
        {
            var active = (string)b.Tag == _category;
            b.Background = active
                ? new SolidColorBrush(Color.FromArgb(0x33, 0xE8, 0xB0, 0x4A))
                : Brushes.Transparent;
            b.Foreground = active
                ? new SolidColorBrush(Color.FromRgb(0xFF, 0xE8, 0xB0))
                : new SolidColorBrush(Color.FromArgb(0x8C, 0xE8, 0xB0, 0x4A));
        }
    }

    private static bool IsDrive(string path) =>
        path.Length == 3 && char.IsLetter(path[0]) && path[1] == ':' && path[2] == '\\';

    private static string Classify(string path, string name)
    {
        if (IsDrive(path)) return "磁盘";
        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (IconExts.Contains(ext)) return "图标";
        return "文档";
    }

    // ---------- icon extraction ----------

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

    private static readonly string DiskIconPath = @"D:\550c-project\desktop-hub\assets\disk-550c.png";
    private static ImageSource? _diskIcon;
    private static bool _diskIconTried;

    private static ImageSource? DiskIcon()
    {
        if (_diskIconTried) return _diskIcon;
        _diskIconTried = true;
        try
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.UriSource = new Uri(DiskIconPath);
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.EndInit();
            bmp.Freeze();
            _diskIcon = bmp;
        }
        catch { }
        return _diskIcon;
    }

    private static ImageSource? GetIcon(string path)
    {
        if (IsDrive(path))
        {
            var di = DiskIcon();
            if (di != null) return di;
        }
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

    // ---------- data ----------

    private string[] SourceDirs()
    {
        var list = new List<string>();
        if (Directory.Exists(HubDir)) list.Add(HubDir);
        var desk = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        if (Directory.Exists(desk)) list.Add(desk);
        var pub = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
        if (Directory.Exists(pub) && !string.Equals(pub, desk, StringComparison.OrdinalIgnoreCase))
            list.Add(pub);
        return list.ToArray();
    }

    private void LoadIcons()
    {
        _all.Clear();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "desktop.ini" };

        // 1) every mounted drive
        try
        {
            foreach (var d in DriveInfo.GetDrives())
            {
                try
                {
                    if (!d.IsReady) continue;
                    var letter = d.Name.TrimEnd('\\');
                    var label = string.IsNullOrWhiteSpace(d.VolumeLabel) ? "本地磁盘" : d.VolumeLabel;
                    _all.Add((d.Name, $"{letter}  {label}"));
                }
                catch { }
            }
        }
        catch { }

        // 2) shortcuts / programs / documents from items + desktop
        foreach (var dir in SourceDirs())
        {
            foreach (var d in Directory.EnumerateDirectories(dir))
            {
                var dn = Path.GetFileName(d);
                if (!seen.Add(dn)) continue;
                _all.Add((d, dn));
            }
            foreach (var f in Directory.EnumerateFiles(dir))
            {
                if (Path.GetFileName(f).Equals("desktop.ini", StringComparison.OrdinalIgnoreCase)) continue;
                var name = Path.GetFileNameWithoutExtension(f);
                if (!seen.Add(name)) continue;
                _all.Add((f, name));
            }
        }

        // apply saved order, then append newcomers
        var byPath = _all.DistinctBy(x => x.Path).OrderBy(x =>
        {
            var idx = _order.IndexOf(x.Path);
            return idx < 0 ? int.MaxValue : idx;
        }).ToList();
        _all.Clear();
        _all.AddRange(byPath);
        _order = _all.Select(x => x.Path).ToList();
        SaveState();

        ApplyFilter(SearchBox.Text);
    }

    private void ApplyFilter(string query)
    {
        SectionsPanel.Children.Clear();
        var q = (query ?? "").Trim();
        var parts = q.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        bool Match((string Path, string Name) x) =>
            parts.Length == 0 || parts.All(p => x.Name.Contains(p, StringComparison.OrdinalIgnoreCase));

        var groups = new (string Title, List<(string Path, string Name)> Items)[]
        {
            ("磁盘", _all.Where(x => Classify(x.Path, x.Name) == "磁盘").Where(Match).ToList()),
            ("文档", _all.Where(x => Classify(x.Path, x.Name) == "文档").Where(Match).ToList()),
            ("图标", _all.Where(x => Classify(x.Path, x.Name) == "图标").Where(Match).ToList())
        };

        var total = 0;
        foreach (var g in groups)
        {
            if (g.Items.Count == 0) continue;
            if (_category != "ALL" && _category != g.Title) continue;

            total += g.Items.Count;

            var header = new Border
            {
                Margin = new Thickness(4, 8, 4, 2),
                Padding = new Thickness(10, 4, 10, 4),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x40, 0xE8, 0xB0, 0x4A)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Child = new TextBlock
                {
                    Text = $"▸ {g.Title}  ·  {g.Items.Count}",
                    Foreground = new SolidColorBrush(Color.FromRgb(0xC8, 0x96, 0x44)),
                    FontFamily = new FontFamily("Bahnschrift"),
                    FontWeight = FontWeights.SemiBold,
                    FontSize = 12
                }
            };
            SectionsPanel.Children.Add(header);

            var wrap = new WrapPanel { Orientation = Orientation.Horizontal };
            foreach (var it in g.Items)
                wrap.Children.Add(BuildTile(it.Path, it.Name));
            SectionsPanel.Children.Add(wrap);
        }

        EmptyHint.Visibility = total == 0 ? Visibility.Visible : Visibility.Collapsed;
        CountText.Text = $"{total} 个项目";
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilter(SearchBox.Text);

    private void IconScroll_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        var scroll = (ScrollViewer)sender;
        var canScroll = scroll.ScrollableHeight > 4;
        var atBottom = scroll.VerticalOffset >= scroll.ScrollableHeight - 4;
        ScrollHint.Visibility = canScroll && !atBottom ? Visibility.Visible : Visibility.Collapsed;
    }

    // ---------- tile ----------

    private FrameworkElement BuildTile(string path, string name)
    {
        var icon = GetIcon(path);
        var isDisk = IsDrive(path);
        var size = isDisk ? Math.Round(_iconSize * 1.45) : _iconSize;

        var img = new Image
        {
            Width = size,
            Height = size,
            Source = icon,
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        var label = new TextBlock
        {
            Text = name,
            Foreground = new SolidColorBrush(Color.FromRgb(0xEF, 0xE0, 0xC0)),
            FontFamily = new FontFamily("Consolas"),
            FontSize = Math.Max(9, size * 0.26),
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(1, 7, 1, 0),
            MaxWidth = size + 48
        };

        var imgWrap = new Border
        {
            Width = size,
            Height = size,
            HorizontalAlignment = HorizontalAlignment.Center,
            Child = img,
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = new ScaleTransform(1, 1)
        };

        var stack = new StackPanel();
        stack.Children.Add(imgWrap);
        stack.Children.Add(label);

        var tileW = size + 60;
        var tileH = size + 56;

        var border = new Border
        {
            Width = tileW,
            Height = tileH,
            Margin = new Thickness(3),
            Background = Brushes.Transparent,
            Child = stack,
            ToolTip = name,
            Cursor = Cursors.Hand,
            CornerRadius = new CornerRadius(3),
            Tag = path,
            AllowDrop = true
        };

        border.RenderTransformOrigin = new Point(0.5, 0.5);
        border.RenderTransform = new ScaleTransform(1, 1);
        border.MouseEnter += (_, _) =>
        {
            imgWrap.Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = Color.FromRgb(0xE8, 0xB0, 0x4A),
                BlurRadius = 15,
                ShadowDepth = 0,
                Direction = 0,
                Opacity = 0.85
            };
            Panel.SetZIndex(border, 10);
            AnimateScaleTo(imgWrap, 1.25);
        };
        border.MouseLeave += (_, _) =>
        {
            imgWrap.Effect = null;
            Panel.SetZIndex(border, 0);
            AnimateScaleTo(imgWrap, 1.0);
        };
        border.MouseLeftButtonDown += (_, e) =>
        {
            _dragStart = e.GetPosition(this);
            _dragPath = path;
            if (e.ClickCount == 2)
            {
                Launch(path);
                _dragPath = null;
            }
        };
        border.MouseMove += async (_, e) =>
        {
            if (_dragPath == null || e.LeftButton != MouseButtonState.Pressed) return;
            var d = e.GetPosition(this) - _dragStart;
            if (Math.Abs(d.X) < 8 && Math.Abs(d.Y) < 8) return;
            var src = _dragPath;
            _dragPath = null;
            var data = new DataObject();
            data.SetData("550c.path", src);
            data.SetData(DataFormats.FileDrop, new[] { src });
            _droppedInternally = false;
            var result = DragDrop.DoDragDrop(border, data, DragDropEffects.Copy | DragDropEffects.Move);

            // dragged out: only remove the source once the copy actually landed on the desktop
            if (result != DragDropEffects.None && !_droppedInternally &&
                src != null && src.StartsWith(HubDir, StringComparison.OrdinalIgnoreCase))
            {
                await System.Threading.Tasks.Task.Delay(600);
                var name = Path.GetFileName(src);
                var desk = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                var landed = Path.Combine(desk, name);
                if (File.Exists(landed) || Directory.Exists(landed))
                {
                    var removed = false;
                    for (var attempt = 0; attempt < 4 && !removed; attempt++)
                    {
                        try
                        {
                            if (Directory.Exists(src)) Directory.Delete(src, true);
                            else if (File.Exists(src)) File.Delete(src);
                            removed = true;
                        }
                        catch
                        {
                            await System.Threading.Tasks.Task.Delay(250);
                        }
                    }
                    if (removed)
                    {
                        _order.Remove(src);
                        SaveState();
                        LoadIcons();
                    }
                }
            }
        };
        border.MouseLeftButtonUp += (_, _) => _dragPath = null;
        border.Drop += (_, e) =>
        {
            _dragPath = null;
            if (!e.Data.GetDataPresent("550c.path"))
            {
                e.Handled = false;
                return;
            }
            if (e.Data.GetData("550c.path") is not string src || src == path)
            {
                e.Handled = true;
                return;
            }
            var from = _order.IndexOf(src);
            var to = _order.IndexOf(path);
            if (from < 0 || to < 0)
            {
                e.Handled = true;
                return;
            }
            (_order[from], _order[to]) = (_order[to], _order[from]);
            SaveState();
            _droppedInternally = true;
            RebuildInOrder();
            e.Handled = true;
        };
        border.DragOver += (_, e) =>
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Move;
                e.Handled = true;
            }
        };

        var menu = new ContextMenu();
        var open = new MenuItem { Header = "打开" };
        open.Click += (_, _) => Launch(path);
        var pin = new MenuItem { Header = "固定到导航栏" };
        pin.Click += (_, _) => PinToDock(path);
        var remove = new MenuItem { Header = "从收纳区移除" };
        remove.Click += (_, _) => RemoveFromArchive(path);
        var sizeBig = new MenuItem { Header = "大图标" };
        sizeBig.Click += (_, _) => SetIconSize(56);
        var sizeMid = new MenuItem { Header = "中图标" };
        sizeMid.Click += (_, _) => SetIconSize(40);
        var sizeSmall = new MenuItem { Header = "小图标" };
        sizeSmall.Click += (_, _) => SetIconSize(30);
        var sizes = new MenuItem { Header = "图标大小" };
        sizes.Items.Add(sizeBig);
        sizes.Items.Add(sizeMid);
        sizes.Items.Add(sizeSmall);

        menu.Items.Add(open);
        menu.Items.Add(pin);
        menu.Items.Add(sizes);
        menu.Items.Add(new Separator());
        menu.Items.Add(remove);
        border.ContextMenu = menu;

        return border;
    }

    private static void AnimateScaleTo(Border b, double target)
    {
        if (b.RenderTransform is not ScaleTransform st) return;
        if (Math.Abs(st.ScaleX - target) < 0.01) return;
        var ease = new System.Windows.Media.Animation.CubicEase
        {
            EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut
        };
        var dur = TimeSpan.FromMilliseconds(120);
        var ax = new System.Windows.Media.Animation.DoubleAnimation(target, dur) { EasingFunction = ease };
        var ay = new System.Windows.Media.Animation.DoubleAnimation(target, dur) { EasingFunction = ease };
        st.BeginAnimation(ScaleTransform.ScaleXProperty, ax);
        st.BeginAnimation(ScaleTransform.ScaleYProperty, ay);
    }

    private static void CopyDirectory(string src, string dst)
    {
        Directory.CreateDirectory(dst);
        foreach (var dir in Directory.GetDirectories(src))
            CopyDirectory(dir, Path.Combine(dst, Path.GetFileName(dir)));
        foreach (var file in Directory.GetFiles(src))
            File.Copy(file, Path.Combine(dst, Path.GetFileName(file)), true);
    }

    private void RebuildInOrder()
    {
        var map = _all.ToDictionary(x => x.Path, x => x.Name, StringComparer.OrdinalIgnoreCase);
        _all.Clear();
        foreach (var p in _order)
            if (map.TryGetValue(p, out var n))
                _all.Add((p, n));
        ApplyFilter(SearchBox.Text);
    }

    private void SetIconSize(double size)
    {
        _iconSize = size;
        SaveState();
        ApplyFilter(SearchBox.Text);
    }

    private void PinToDock(string path)
    {
        try
        {
            var dockFile = @"D:\550c-project\desktop-hub\dock.json";
            var items = new List<string>();
            if (File.Exists(dockFile))
                items = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(dockFile)) ?? new();
            if (!items.Contains(path, StringComparer.OrdinalIgnoreCase))
            {
                items.Add(path);
                File.WriteAllText(dockFile, JsonSerializer.Serialize(items, new JsonSerializerOptions { WriteIndented = true }));
            }
        }
        catch { }
    }

    private void RemoveFromArchive(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch { }
        _order.Remove(path);
        SaveState();
        LoadIcons();
    }

    private void Launch(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(path) ?? string.Empty
            });
            Hide();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "启动失败");
        }
    }

    // ---------- header buttons ----------

    // ---------- launchpad open / close ----------

    public void ShowWithAnimation()
    {
        WindowState = WindowState.Normal;
        Show();
        Activate();
    }

    public void HideWithAnimation()
    {
        SaveLayout();
        Close();   // closing the window shuts the process down
    }

    private void SaveLayout()
    {
        try
        {
            if (WindowState != WindowState.Normal) return;
            var l = new LayoutState { Left = Left, Top = Top, Width = Width, Height = Height };
            File.WriteAllText(LayoutFile, JsonSerializer.Serialize(l, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e) => HideWithAnimation();

    private void BtnMin_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Header_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left && e.OriginalSource is not TextBox)
        {
            try { DragMove(); } catch { }
        }
    }

    // ---------- stars ----------

    private void DrawStars()
    {
        StarCanvas.Children.Clear();
        var rng = new Random(770);
        var w = ActualWidth;
        var h = ActualHeight;
        if (w <= 0 || h <= 0) return;

        for (var i = 0; i < 420; i++)
        {
            var size = rng.NextDouble() < 0.82 ? 1.0 : 1.8;
            var alpha = (byte)rng.Next(14, 96);
            var dot = new System.Windows.Shapes.Rectangle
            {
                Width = size,
                Height = size,
                Fill = new SolidColorBrush(Color.FromArgb(alpha, 0xE8, 0xB0, 0x4A)),
                IsHitTestVisible = false
            };
            Canvas.SetLeft(dot, rng.NextDouble() * w);
            Canvas.SetTop(dot, rng.NextDouble() * h);
            StarCanvas.Children.Add(dot);
        }
    }
}
