using System.Diagnostics;
using System.Text;
using System.Windows.Threading;

namespace SakuraMusic;

public readonly record struct TrackedWindow(IntPtr Handle, Native.RECT Frame, bool Occluded, bool FillsScreen, string? CoveredBy);

/// <summary>
/// Polls the top-level window list for Apple Music windows. EnumWindows reports
/// windows in z-order (front to back), which is enough to know whether another
/// app is sitting on top of Music.
/// </summary>
public sealed class MusicWindowTracker
{
    private static readonly HashSet<string> IgnoredClasses = new(StringComparer.Ordinal)
    {
        "Shell_TrayWnd", "Shell_SecondaryTrayWnd", "Progman", "WorkerW",
        "Windows.UI.Core.CoreWindow", "XamlExplorerHostIslandWindow", "TopLevelWindowForOverflowXamlIsland",
    };

    public event Action<IReadOnlyList<TrackedWindow>>? Updated;
    public bool MusicIsRunning { get; private set; }

    private readonly DispatcherTimer _timer = new(DispatcherPriority.Render);
    private readonly uint _ownPid = (uint)Environment.ProcessId;
    private static readonly TimeSpan Active = TimeSpan.FromMilliseconds(33);
    private static readonly TimeSpan Idle = TimeSpan.FromMilliseconds(200);

    private HashSet<uint> _musicPids = new();
    private DateTime _pidsRefreshed = DateTime.MinValue;

    public MusicWindowTracker()
    {
        _timer.Interval = Idle;
        _timer.Tick += (_, _) => Tick();
    }

    public void Start() => _timer.Start();
    public void Stop() => _timer.Stop();

    private void Tick()
    {
        var windows = Poll(null);
        Updated?.Invoke(windows);
        _timer.Interval = windows.Any(w => !w.Occluded) ? Active : Idle;
    }

    /// <summary>Human-readable dump of what the tracker sees, for bug reports.</summary>
    public string Describe()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Sakura Music diagnostics {DateTime.Now:u}");
        sb.AppendLine($"Music process ids: {(MusicPids().Count == 0 ? "none" : string.Join(", ", MusicPids()))}");
        sb.AppendLine("Windows, front to back:");
        var windows = Poll(sb);
        sb.AppendLine();
        sb.AppendLine($"Tracked Music windows: {windows.Count}");
        foreach (var w in windows)
            sb.AppendLine($"  0x{w.Handle.ToInt64():X} {Rect(w.Frame)} occluded={w.Occluded} fillsScreen={w.FillsScreen}");
        return sb.ToString();
    }

    private static string Rect(in Native.RECT r) => $"[{r.Left},{r.Top} {r.Width}x{r.Height}]";

    /// <summary>
    /// Apple Music's process is "AppleMusic" from the Store; match loosely so a
    /// renamed or side-loaded build still counts. Refreshed once a second.
    /// </summary>
    private HashSet<uint> MusicPids()
    {
        var now = DateTime.UtcNow;
        if ((now - _pidsRefreshed).TotalSeconds < 1) return _musicPids;

        var pids = new HashSet<uint>();
        foreach (var p in Process.GetProcesses())
        {
            try
            {
                var name = p.ProcessName.Replace(" ", "").ToLowerInvariant();
                if (name.Contains("applemusic")) pids.Add((uint)p.Id);
            }
            catch
            {
                // Access denied on some system processes; not Music anyway.
            }
            finally
            {
                p.Dispose();
            }
        }
        _musicPids = pids;
        _pidsRefreshed = now;
        return pids;
    }

    private List<TrackedWindow> Poll(StringBuilder? log)
    {
        var pids = MusicPids();
        var foreground = Native.GetForegroundWindow();
        var result = new List<TrackedWindow>();
        var foreign = new List<(Native.RECT rect, string label)>();

        Native.EnumWindows((hWnd, _) =>
        {
            if (!Native.IsWindowVisible(hWnd) || Native.IsIconic(hWnd) || Native.IsCloaked(hWnd)) return true;
            Native.GetWindowThreadProcessId(hWnd, out uint pid);
            if (pid == _ownPid) return true;
            if (!Native.TryGetFrameBounds(hWnd, out var rect) || rect.Width < 2 || rect.Height < 2) return true;

            var cls = Native.ClassName(hWnd);
            var title = Native.WindowText(hWnd);
            string verdict;

            bool isMusic = pids.Contains(pid) || title == "Apple Music";
            if (IgnoredClasses.Contains(cls))
            {
                verdict = "ignored (shell)";
            }
            else if (isMusic)
            {
                if (rect.Width < 200 || rect.Height < 150)
                {
                    verdict = "music, too small";
                }
                else
                {
                    // The foreground window can't have anything but topmost overlays above it.
                    string? coveredBy = null;
                    if (hWnd != foreground)
                    {
                        var r = rect;
                        coveredBy = foreign.FirstOrDefault(f => f.rect.Intersects(r)).label;
                    }
                    result.Add(new TrackedWindow(hWnd, rect, coveredBy != null, Native.FillsMonitor(rect), coveredBy));
                    verdict = coveredBy != null ? $"MUSIC (covered by {coveredBy})" : "MUSIC";
                }
            }
            else if (!Native.CanOcclude(hWnd))
            {
                verdict = "ignored (transparent/tool)";
            }
            else
            {
                foreign.Add((rect, title.Length > 0 ? title : cls));
                verdict = "occluder";
            }

            log?.AppendLine($"  {verdict,-26} pid={pid,-6} {Rect(rect),-24} {cls} \"{title}\"");
            return true;
        }, IntPtr.Zero);

        MusicIsRunning = pids.Count > 0 || result.Count > 0;
        return result;
    }
}
