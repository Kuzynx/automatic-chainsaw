using System.Diagnostics;
using System.Windows;
using System.Windows.Media;

namespace SakuraMusic;

/// <summary>
/// Falling, swaying petals drawn in a single visual each frame. Cheap enough
/// for a few hundred petals on a full-screen window.
/// </summary>
public sealed class PetalField : FrameworkElement
{
    private sealed class Petal
    {
        public double X, Y, Vy, Size, Angle, Spin, SwayAmp, SwayFreq, Phase, Alpha;
        public int Variant;
    }

    private static readonly Geometry PetalShape;
    private static readonly Brush[] PetalBrushes;

    private readonly List<Petal> _petals = new();
    private readonly Random _rng = new();
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private double _lastTime;
    private double _wind;
    private double _windTarget;
    private double _nextGust;
    private bool _running;

    /// <summary>Petals per 100k square DIPs at density 1.0.</summary>
    private const double BaseCoverage = 10;
    public double Density { get; set; } = 1.0;

    static PetalField()
    {
        var shape = SakuraArt.PetalGeometry(1, 1, -0.5, -0.5);
        shape.Freeze();
        PetalShape = shape;

        Brush Grad(Color a, Color b)
        {
            var brush = new LinearGradientBrush(a, b, new Point(0.5, 1), new Point(0.5, 0));
            brush.Freeze();
            return brush;
        }
        PetalBrushes = new[]
        {
            Grad(SakuraPalette.PetalRose, SakuraPalette.PetalBlush),
            Grad(SakuraPalette.PetalPink, SakuraPalette.PetalWhite),
            Grad(SakuraPalette.PetalRose, SakuraPalette.PetalPink),
            Grad(SakuraPalette.PetalBlush, SakuraPalette.PetalWhite),
        };
    }

    public PetalField()
    {
        IsHitTestVisible = false;
        Loaded += (_, _) => Start();
        Unloaded += (_, _) => Stop();
    }

    public void Start()
    {
        if (_running) return;
        _running = true;
        _lastTime = _clock.Elapsed.TotalSeconds;
        CompositionTarget.Rendering += OnFrame;
    }

    public void Stop()
    {
        if (!_running) return;
        _running = false;
        CompositionTarget.Rendering -= OnFrame;
    }

    private int TargetCount()
    {
        double area = ActualWidth * ActualHeight;
        return (int)Math.Round(area / 100_000.0 * BaseCoverage * Density);
    }

    private Petal Spawn(bool anywhere)
    {
        double size = 9 + _rng.NextDouble() * 13;
        return new Petal
        {
            X = _rng.NextDouble() * (ActualWidth + 120) - 60,
            Y = anywhere ? _rng.NextDouble() * ActualHeight : -size - _rng.NextDouble() * 80,
            Vy = 18 + _rng.NextDouble() * 22,
            Size = size,
            Angle = _rng.NextDouble() * 360,
            Spin = (_rng.NextDouble() - 0.5) * 140,
            SwayAmp = 10 + _rng.NextDouble() * 24,
            SwayFreq = 0.35 + _rng.NextDouble() * 0.5,
            Phase = _rng.NextDouble() * Math.PI * 2,
            Alpha = 0.72 + _rng.NextDouble() * 0.28,
            Variant = _rng.Next(PetalBrushes.Length),
        };
    }

    private void OnFrame(object? sender, EventArgs e)
    {
        double now = _clock.Elapsed.TotalSeconds;
        double dt = Math.Min(0.05, now - _lastTime);
        _lastTime = now;
        if (ActualWidth <= 0 || ActualHeight <= 0) return;

        // Occasional gusts that ease in and out.
        if (now >= _nextGust)
        {
            _windTarget = (_rng.NextDouble() - 0.5) * 50;
            _nextGust = now + 3 + _rng.NextDouble() * 4;
        }
        _wind += (_windTarget - _wind) * Math.Min(1, dt * 0.6);

        int target = TargetCount();
        while (_petals.Count < target) _petals.Add(Spawn(anywhere: _petals.Count == 0 || _rng.NextDouble() < 0.6));
        if (_petals.Count > target) _petals.RemoveRange(target, _petals.Count - target);

        for (int i = 0; i < _petals.Count; i++)
        {
            var p = _petals[i];
            p.Y += p.Vy * dt;
            p.Vy = Math.Min(p.Vy + 4 * dt, 75);
            p.X += (Math.Cos(now * p.SwayFreq * Math.PI * 2 + p.Phase) * p.SwayAmp + _wind) * dt;
            p.Angle += p.Spin * dt;

            if (p.Y > ActualHeight + p.Size || p.X < -140 || p.X > ActualWidth + 140)
                _petals[i] = Spawn(anywhere: false);
        }

        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        foreach (var p in _petals)
        {
            dc.PushOpacity(p.Alpha);
            dc.PushTransform(new TranslateTransform(p.X, p.Y));
            dc.PushTransform(new RotateTransform(p.Angle));
            dc.PushTransform(new ScaleTransform(p.Size, p.Size));
            dc.DrawGeometry(PetalBrushes[p.Variant], null, PetalShape);
            dc.Pop();
            dc.Pop();
            dc.Pop();
            dc.Pop();
        }
    }
}
