namespace SakuraMusic;

/// <summary>Keeps one overlay per visible Apple Music window, aligned frame for frame.</summary>
public sealed class OverlayManager
{
    private readonly Settings _settings;
    private readonly Dictionary<IntPtr, OverlayWindow> _overlays = new();
    private List<IntPtr> _lastOrder = new();

    public OverlayManager(Settings settings) => _settings = settings;

    public int VisibleCount => _overlays.Values.Count(o => o.IsShown);

    public void Sync(IReadOnlyList<TrackedWindow> tracked)
    {
        var ids = tracked.Select(t => t.Handle).ToHashSet();

        foreach (var stale in _overlays.Keys.Where(k => !ids.Contains(k)).ToList())
        {
            _overlays[stale].HideOverlay();
            _overlays[stale].Close();
            _overlays.Remove(stale);
        }

        bool orderChanged = false;
        foreach (var window in tracked)
        {
            bool shouldShow = _settings.Enabled && !window.Occluded;
            if (!_overlays.TryGetValue(window.Handle, out var overlay))
            {
                overlay = new OverlayWindow(_settings);
                _overlays[window.Handle] = overlay;
                orderChanged = true;
            }

            overlay.MoveTo(window.Frame);
            overlay.CornerRadius = window.FillsScreen ? 0 : 8;

            if (shouldShow && !overlay.IsShown)
            {
                overlay.ShowOverlay();
                orderChanged = true;
            }
            else if (!shouldShow && overlay.IsShown)
            {
                overlay.HideOverlay();
            }
        }

        var order = tracked.Select(t => t.Handle).ToList();
        if (orderChanged || !order.SequenceEqual(_lastOrder))
        {
            for (int i = order.Count - 1; i >= 0; i--)
            {
                if (_overlays.TryGetValue(order[i], out var o) && o.IsShown) o.BringToTop();
            }
            _lastOrder = order;
        }
    }

    public void ApplySettings()
    {
        foreach (var overlay in _overlays.Values)
        {
            overlay.ApplySettings();
            if (!_settings.Enabled) overlay.HideOverlay();
        }
    }

    public void CloseAll()
    {
        foreach (var overlay in _overlays.Values) overlay.Close();
        _overlays.Clear();
    }
}
