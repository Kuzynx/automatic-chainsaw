using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace SakuraMusic;

/// <summary>Static blossom branch, generated once and cached as a bitmap.</summary>
public sealed class BranchElement : FrameworkElement
{
    private static readonly DrawingGroup Drawing = SakuraArt.MakeBranch(new Size(470, 300));

    public BranchElement()
    {
        IsHitTestVisible = false;
        CacheMode = new BitmapCache { RenderAtScale = 2, SnapsToDevicePixels = false };
        Effect = new DropShadowEffect
        {
            Color = Color.FromRgb(64, 13, 26),
            Opacity = 0.28,
            BlurRadius = 10,
            ShadowDepth = 2,
            Direction = 270,
        };
    }

    protected override void OnRender(DrawingContext dc) => dc.DrawDrawing(Drawing);
}
