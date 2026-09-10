using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows;
using System.Windows.Forms;
using Microsoft.Win32;

namespace SakuraMusic;

public partial class App : System.Windows.Application
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "SakuraMusic";

    private readonly Settings _settings = Settings.Current;
    private readonly MusicWindowTracker _tracker = new();
    private readonly OverlayManager _overlays;
    private NotifyIcon? _tray;

    private ToolStripMenuItem? _statusItem;
    private ToolStripMenuItem? _enabledItem;
    private ToolStripMenuItem? _branchItem;
    private ToolStripMenuItem? _loginItem;
    private readonly List<ToolStripMenuItem> _petalItems = new();
    private readonly List<ToolStripMenuItem> _blushItems = new();

    public App()
    {
        _overlays = new OverlayManager(_settings);
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        BuildTray();

        _settings.Changed += () =>
        {
            _overlays.ApplySettings();
            RefreshMenuState();
        };

        _tracker.Updated += windows =>
        {
            _overlays.Sync(windows);
            UpdateStatusLine();
        };
        _tracker.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tracker.Stop();
        _overlays.CloseAll();
        if (_tray != null)
        {
            _tray.Visible = false;
            _tray.Dispose();
        }
        base.OnExit(e);
    }

    // MARK: Tray menu

    private void BuildTray()
    {
        var menu = new ContextMenuStrip();

        _statusItem = new ToolStripMenuItem("Looking for Apple Music…") { Enabled = false };
        menu.Items.Add(_statusItem);
        menu.Items.Add(new ToolStripSeparator());

        _enabledItem = new ToolStripMenuItem("Enabled", null, (_, _) => _settings.Enabled = !_settings.Enabled);
        menu.Items.Add(_enabledItem);

        var petals = new ToolStripMenuItem("Petals");
        foreach (PetalDensity d in Enum.GetValues<PetalDensity>())
        {
            var item = new ToolStripMenuItem(Settings.Title(d), null, (_, _) => _settings.Petals = d) { Tag = d };
            petals.DropDownItems.Add(item);
            _petalItems.Add(item);
        }
        menu.Items.Add(petals);

        var blush = new ToolStripMenuItem("Blush tint");
        foreach (Blush b in Enum.GetValues<Blush>())
        {
            var item = new ToolStripMenuItem(Settings.Title(b), null, (_, _) => _settings.Blush = b) { Tag = b };
            blush.DropDownItems.Add(item);
            _blushItems.Add(item);
        }
        menu.Items.Add(blush);

        _branchItem = new ToolStripMenuItem("Blossom branch", null, (_, _) => _settings.ShowBranch = !_settings.ShowBranch);
        menu.Items.Add(_branchItem);

        menu.Items.Add(new ToolStripSeparator());
        _loginItem = new ToolStripMenuItem("Launch at startup", null, (_, _) => ToggleLaunchAtStartup());
        menu.Items.Add(_loginItem);

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("Quit Sakura Music", null, (_, _) => Shutdown()));

        _tray = new NotifyIcon
        {
            Icon = MakeTrayIcon(),
            Text = "Sakura Music",
            ContextMenuStrip = menu,
            Visible = true,
        };
        RefreshMenuState();
    }

    private void RefreshMenuState()
    {
        if (_enabledItem != null) _enabledItem.Checked = _settings.Enabled;
        if (_branchItem != null) _branchItem.Checked = _settings.ShowBranch;
        foreach (var item in _petalItems) item.Checked = (PetalDensity)item.Tag! == _settings.Petals;
        foreach (var item in _blushItems) item.Checked = (Blush)item.Tag! == _settings.Blush;
        if (_loginItem != null) _loginItem.Checked = IsLaunchAtStartup();
    }

    private string _lastStatus = "";

    private void UpdateStatusLine()
    {
        string text;
        if (!_tracker.MusicIsRunning) text = "Apple Music is not running";
        else if (!_settings.Enabled) text = "Paused";
        else
        {
            int n = _overlays.VisibleCount;
            text = n switch
            {
                0 => "Apple Music is hidden or covered",
                1 => "Blooming over Apple Music",
                _ => $"Blooming over {n} Apple Music windows",
            };
        }
        if (text != _lastStatus && _statusItem != null)
        {
            _lastStatus = text;
            _statusItem.Text = text;
        }
    }

    // MARK: Startup registration

    private static bool IsLaunchAtStartup()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
        return key?.GetValue(RunValueName) is string;
    }

    private void ToggleLaunchAtStartup()
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
            if (IsLaunchAtStartup())
                key.DeleteValue(RunValueName, throwOnMissingValue: false);
            else
                key.SetValue(RunValueName, $"\"{Environment.ProcessPath}\"");
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(ex.Message, "Couldn't change startup setting",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        RefreshMenuState();
    }

    // MARK: Icon

    /// <summary>A small five-petal blossom, drawn at runtime so no icon asset is needed.</summary>
    private static Icon MakeTrayIcon()
    {
        const int size = 32;
        using var bmp = new Bitmap(size, size);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(System.Drawing.Color.Transparent);

        var center = new PointF(size / 2f, size / 2f);
        using var petal = new SolidBrush(System.Drawing.Color.FromArgb(255, 255, 183, 206));
        using var edge = new Pen(System.Drawing.Color.FromArgb(200, 233, 119, 156), 1f);
        for (int i = 0; i < 5; i++)
        {
            g.ResetTransform();
            g.TranslateTransform(center.X, center.Y);
            g.RotateTransform(i * 72);
            var rect = new RectangleF(-4.5f, -14f, 9f, 13f);
            g.FillEllipse(petal, rect);
            g.DrawEllipse(edge, rect);
        }
        g.ResetTransform();
        using var core = new SolidBrush(System.Drawing.Color.FromArgb(255, 247, 222, 122));
        g.FillEllipse(core, center.X - 3.5f, center.Y - 3.5f, 7f, 7f);

        return Icon.FromHandle(bmp.GetHicon());
    }
}
