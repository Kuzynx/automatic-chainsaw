using System.Windows;
using System.Windows.Media;

namespace SakuraMusic;

/// <summary>Deterministic SplitMix64 so the branch is identical across launches.</summary>
public sealed class SeededRandom
{
    private ulong _state;
    public SeededRandom(ulong seed) => _state = seed + 0x9E3779B97F4A7C15UL;

    public ulong NextULong()
    {
        _state += 0x9E3779B97F4A7C15UL;
        ulong z = _state;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    public double NextDouble() => (NextULong() >> 11) * (1.0 / 9007199254740992.0);
    public double Range(double min, double max) => min + (max - min) * NextDouble();
    public bool Chance(double p) => NextDouble() < p;
    public T Pick<T>(IReadOnlyList<T> items) => items[(int)(NextULong() % (ulong)items.Count)];
}

public static class SakuraPalette
{
    public static Color Hex(uint v, double alpha = 1) =>
        Color.FromArgb((byte)Math.Round(alpha * 255), (byte)(v >> 16), (byte)(v >> 8), (byte)v);

    public static readonly Color PetalBlush = Hex(0xFFD6E4);
    public static readonly Color PetalPink = Hex(0xFFB7CE);
    public static readonly Color PetalRose = Hex(0xF799B8);
    public static readonly Color PetalWhite = Hex(0xFFF1F5);

    public static readonly Color[] FlowerFills = { Hex(0xFFC5D8), Hex(0xFFB3CB), Hex(0xF9A2BE), Hex(0xFFD9E6) };
    public static readonly Color FlowerEdge = Hex(0xE9779C, 0.55);
    public static readonly Color FlowerCenter = Hex(0xF7DE7A);
    public static readonly Color Bud = Hex(0xE8749A);
    public static readonly Color BranchWood = Hex(0x3D2523, 0.92);
}

public static class SakuraArt
{
    /// <summary>
    /// Petal outline in a unit box: base at (0.5, 1), notched tip at the top
    /// (WPF's y axis points down, so the tip is at y = 0).
    /// </summary>
    public static PathGeometry PetalGeometry(double width, double height, double offsetX = 0, double offsetY = 0)
    {
        Point P(double x, double y) => new(offsetX + x * width, offsetY + (1 - y) * height);

        var figure = new PathFigure { StartPoint = P(0.5, 0.0), IsClosed = true, IsFilled = true };
        figure.Segments.Add(new BezierSegment(P(0.02, 0.22), P(-0.02, 0.86), P(0.30, 0.95), true));
        figure.Segments.Add(new BezierSegment(P(0.41, 1.02), P(0.47, 0.86), P(0.5, 0.78), true));
        figure.Segments.Add(new BezierSegment(P(0.53, 0.86), P(0.59, 1.02), P(0.70, 0.95), true));
        figure.Segments.Add(new BezierSegment(P(1.02, 0.86), P(0.98, 0.22), P(0.5, 0.0), true));

        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        return geometry;
    }

    /// <summary>Five-petal blossom centred on the origin.</summary>
    public static Geometry FlowerGeometry(double radius)
    {
        var group = new GeometryGroup { FillRule = FillRule.Nonzero };
        for (int i = 0; i < 5; i++)
        {
            var petal = PetalGeometry(radius * 0.84, radius, -radius * 0.42, -radius);
            petal.Transform = new RotateTransform(i * 72);
            group.Children.Add(petal);
        }
        return group;
    }

    /// <summary>Builds the corner branch as a frozen drawing, top-right aligned within <paramref name="size"/>.</summary>
    public static DrawingGroup MakeBranch(Size size, ulong seed = 20240401)
    {
        var rng = new SeededRandom(seed);
        var wood = new List<(PathGeometry path, double width)>();
        var flowers = new List<(Point center, double radius, Color fill, double rotation)>();
        var buds = new List<(Point center, double radius)>();

        // Work in a y-up space and flip at the end so the algorithm matches the macOS build.
        void Grow(Point start, double angle, double length, double width, int depth)
        {
            var end = new Point(start.X + Math.Cos(angle) * length, start.Y + Math.Sin(angle) * length);
            var normal = new Vector(-Math.Sin(angle), Math.Cos(angle));
            double bend = rng.Range(-0.28, 0.28) * length;
            var c1 = new Point(start.X + Math.Cos(angle) * length * 0.33 + normal.X * bend,
                               start.Y + Math.Sin(angle) * length * 0.33 + normal.Y * bend);
            var c2 = new Point(start.X + Math.Cos(angle) * length * 0.66 + normal.X * bend * 0.6,
                               start.Y + Math.Sin(angle) * length * 0.66 + normal.Y * bend * 0.6);

            var figure = new PathFigure { StartPoint = start, IsClosed = false, IsFilled = false };
            figure.Segments.Add(new BezierSegment(c1, c2, end, true));
            var path = new PathGeometry();
            path.Figures.Add(figure);
            wood.Add((path, width));

            if (depth >= 1)
            {
                int count = (int)(length / 22) + 1;
                for (int i = 0; i < count; i++)
                {
                    double t = rng.Range(0.15, 1.0);
                    var along = new Point(start.X + (end.X - start.X) * t, start.Y + (end.Y - start.Y) * t);
                    double side = rng.Range(-9, 9);
                    var center = new Point(along.X + normal.X * side, along.Y + normal.Y * side);
                    if (rng.Chance(0.22))
                        buds.Add((center, rng.Range(3.2, 4.6)));
                    else
                        flowers.Add((center, rng.Range(7.5, 14.5), rng.Pick(SakuraPalette.FlowerFills), rng.Range(0, 360)));
                }
            }

            if (depth >= 4 || length <= 18)
            {
                flowers.Add((end, rng.Range(9, 13), rng.Pick(SakuraPalette.FlowerFills), rng.Range(0, 360)));
                return;
            }

            Grow(end, angle + rng.Range(-0.32, 0.22), length * 0.74, width * 0.68, depth + 1);

            int shoots = depth == 0 ? 2 : (rng.Chance(0.75) ? 1 : 0);
            for (int i = 0; i < shoots; i++)
            {
                double sign = rng.Chance(0.5) ? 1 : -1;
                double t = rng.Range(0.35, 0.85);
                var origin = new Point(start.X + (end.X - start.X) * t, start.Y + (end.Y - start.Y) * t);
                Grow(origin, angle + sign * rng.Range(0.55, 1.05), length * 0.58, width * 0.5, depth + 1);
            }
        }

        var startPoint = new Point(size.Width + 14, size.Height - 26);
        Grow(startPoint, Math.PI + 0.16, size.Width * 0.34, 7.5, 0);

        var group = new DrawingGroup();
        // Flip y-up drawing space into WPF's y-down space.
        group.Transform = new ScaleTransform(1, -1, 0, size.Height / 2);

        var woodBrush = new SolidColorBrush(SakuraPalette.BranchWood);
        woodBrush.Freeze();
        foreach (var (path, width) in wood.OrderByDescending(w => w.width))
        {
            var pen = new Pen(woodBrush, Math.Max(1.2, width))
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round,
                LineJoin = PenLineJoin.Round,
            };
            pen.Freeze();
            group.Children.Add(new GeometryDrawing(null, pen, path));
        }

        var budBrush = new SolidColorBrush(SakuraPalette.Bud);
        budBrush.Freeze();
        foreach (var (center, radius) in buds)
            group.Children.Add(new GeometryDrawing(budBrush, null, new EllipseGeometry(center, radius, radius)));

        var edgePen = new Pen(new SolidColorBrush(SakuraPalette.FlowerEdge), 0.6);
        edgePen.Freeze();
        var centerBrush = new SolidColorBrush(SakuraPalette.FlowerCenter);
        centerBrush.Freeze();

        foreach (var (center, radius, fill, rotation) in flowers)
        {
            var geometry = FlowerGeometry(radius);
            var transform = new TransformGroup();
            transform.Children.Add(new RotateTransform(rotation));
            transform.Children.Add(new TranslateTransform(center.X, center.Y));
            geometry.Transform = transform;
            var brush = new SolidColorBrush(fill);
            brush.Freeze();
            group.Children.Add(new GeometryDrawing(brush, edgePen, geometry));

            double r = radius * 0.18;
            group.Children.Add(new GeometryDrawing(centerBrush, null, new EllipseGeometry(center, r, r)));
        }

        group.Freeze();
        return group;
    }
}
