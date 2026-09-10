using System.Diagnostics;
using System.Windows.Threading;

namespace SakuraMusic;

public readonly record struct TrackedWindow(IntPtr Handle, Native.RECT Frame, bool Occluded, bool FillsScreen);

/// <summary>
/// Polls the top-level window list for Apple Music windows. EnumWindows reports
/// windows in z-order (front to back), which is enough to know whether another
/// app is sitting on top of Music.
/// </summary>
public sealed class MusicWindowTracker
{
    private static readonly string[] ProcessNames = { "AppleMusic", "Apple Music" };
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

    public MusicWindowTracker()
    {
        _timer.Interval = Idle;
        _timer.Tick += (_, _) => Tick();
    }

    public void Start() => _timer.Start();
    public void Stop() => _timer.Stop();

    private void Tick()
    {
        var windows = Poll();
        Updated?.Invoke(windows);
        _timer.Interval = windows.Any(w => !w.Occluded) ? Active : Idle;
    }

    private HashSet<uint> MusicPids()
    {
        var pids = new HashSet<uint>();
        foreach (var name in ProcessNames)
        {
            foreach (var p in Process.GetProcessesByName(name))
            {
                pids.Add((uint)p.Id);
                p.Dispose();
            }
        }
        return pids;
    }

    private List<TrackedWindow> Poll()
    {
        var pids = MusicPids();
        var result = new List<TrackedWindow>();
        var foreign = new List<Native.RECT>();

        Native.EnumWindows((hWnd, _) =>
        {
            if (!Native.IsWindowVisible(hWnd) || Native.IsIconic(hWnd) || Native.IsCloaked(hWnd)) return true;
            Native.GetWindowThreadProcessId(hWnd, out uint pid);
            if (pid == _ownPid) return true;
            if (!Native.TryGetFrameBounds(hWnd, out var rect) || rect.Width < 2 || rect.Height < 2) return true;

            var cls = Native.ClassName(hWnd);
            if (IgnoredClasses.Contains(cls)) return true;

            bool isMusic = pids.Contains(pid) || Native.WindowText(hWnd) == "Apple Music";
            if (isMusic)
            {
                if (rect.Width < 200 || rect.Height < 150) return true;
                bool occluded = foreign.Any(f => f.Intersects(rect));
                result.Add(new TrackedWindow(hWnd, rect, occluded, Native.FillsMonitor(rect)));
            }
            else
            {
                foreign.Add(rect);
            }
            return true;
        }, IntPtr.Zero);

        MusicIsRunning = pids.Count > 0 || result.Count > 0;
        return result;
    }
}
