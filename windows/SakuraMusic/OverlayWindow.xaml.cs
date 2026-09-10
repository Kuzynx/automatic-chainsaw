using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace SakuraMusic;

/// <summary>
/// Transparent, click-through, always-on-top window that is positioned exactly
/// over one Apple Music window. Never receives focus or input.
/// </summary>
public partial class OverlayWindow : Window
{
    private readonly Settings _settings;
    private IntPtr _hwnd;
    private Native.RECT _frame;
    private double _cornerRadius = 8;

    public bool IsShown { get; private set; }

    public OverlayWindow(Settings settings)
    {
        _settings = settings;
        InitializeComponent();
        SizeChanged += (_, _) => UpdateClip();
        // Create the HWND now so it can be placed before it is first shown.
        _hwnd = new WindowInteropHelper(this).EnsureHandle();
        ApplySettings();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _hwnd = new WindowInteropHelper(this).Handle;
        Native.MakeClickThrough(_hwnd);
    }

    public double CornerRadius
    {
        get => _cornerRadius;
        set
        {
            if (Math.Abs(_cornerRadius - value) < 0.01) return;
            _cornerRadius = value;
            UpdateClip();
        }
    }

    /// <summary>Move/resize to a rectangle in physical pixels.</summary>
    public void MoveTo(in Native.RECT rect)
    {
        // Compare against the live HWND rect: WPF may resize the window itself on a
        // DPI change, and the cached frame alone would miss that.
        if (rect.Equals(_frame) && Native.GetWindowRect(_hwnd, out var live) && live.Equals(rect)) return;
        _frame = rect;
        Native.SetWindowPos(_hwnd, IntPtr.Zero, rect.Left, rect.Top, rect.Width, rect.Height,
            Native.SWP_NOACTIVATE | Native.SWP_NOZORDER | Native.SWP_NOOWNERZORDER);
    }

    public void ShowOverlay()
    {
        if (IsShown) return;
        IsShown = true;
        Show();
        Petals.Start();
    }

    public void HideOverlay()
    {
        if (!IsShown) return;
        IsShown = false;
        Petals.Stop();
        Hide();
    }

    public void BringToTop()
    {
        Native.SetWindowPos(_hwnd, Native.HWND_TOPMOST, 0, 0, 0, 0,
            Native.SWP_NOACTIVATE | Native.SWP_NOMOVE | Native.SWP_NOSIZE);
    }

    public void ApplySettings()
    {
        Tint.Opacity = _settings.BlushOpacity;
        Petals.Density = _settings.PetalMultiplier;
        UpdateBranchVisibility();
    }

    private void UpdateClip()
    {
        double w = ActualWidth, h = ActualHeight;
        if (w <= 0 || h <= 0) return;
        Root.Clip = new RectangleGeometry(new Rect(0, 0, w, h), _cornerRadius, _cornerRadius);
        UpdateBranchVisibility();
    }

    private void UpdateBranchVisibility()
    {
        bool show = _settings.ShowBranch && ActualWidth >= 640 && ActualHeight >= 420;
        Branch.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
    }
}
