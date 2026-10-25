using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Osynt.Desktop.Controls;

/// <summary>Original, code-drawn pixel art that stays crisp on every desktop scale.</summary>
public sealed class PixelArt : Control
{
    public static readonly StyledProperty<string> KindProperty = AvaloniaProperty.Register<PixelArt, string>(nameof(Kind), "sprout");
    public string Kind
    {
        get => GetValue(KindProperty); set => SetValue(KindProperty, value);
    }
    static PixelArt() => AffectsRender<PixelArt>(KindProperty);
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (Kind == "landscape")
        {
            DrawLandscape(context);
            return;
        }
        var scale = Math.Min(Bounds.Width, Bounds.Height) / 20;
        using var transform = context.PushTransform(Matrix.CreateScale(scale, scale));
        void P(int x, int y, int w, int h, string color) => context.FillRectangle(Brush.Parse(color), new Rect(x, y, w, h));
        if (Kind == "sun")
        {
            P(7, 5, 6, 10, "#D9AE5C");
            P(5, 7, 10, 6, "#D9AE5C");
            P(7, 7, 6, 6, "#F1D38B");
            P(9, 0, 2, 3, "#D9AE5C");
            P(9, 17, 2, 3, "#D9AE5C");
            P(0, 9, 3, 2, "#D9AE5C");
            P(17, 9, 3, 2, "#D9AE5C");
            P(3, 3, 2, 2, "#D9AE5C");
            P(15, 3, 2, 2, "#D9AE5C");
            P(3, 15, 2, 2, "#D9AE5C");
            P(15, 15, 2, 2, "#D9AE5C");
        }
        else if (Kind == "mail")
        {
            P(2, 6, 16, 11, "#667D50");
            P(3, 7, 14, 8, "#DCE5CD");
            P(4, 8, 2, 2, "#8FA474");
            P(6, 10, 2, 2, "#8FA474");
            P(8, 12, 4, 2, "#8FA474");
            P(12, 10, 2, 2, "#8FA474");
            P(14, 8, 2, 2, "#8FA474");
            P(14, 1, 2, 4, "#D7A65C");
            P(13, 2, 4, 2, "#D7A65C");
        }
        else
        {
            P(9, 7, 2, 10, "#597847");
            P(8, 16, 5, 2, "#8F7953");
            P(6, 18, 9, 1, "#C9B48C");
            P(3, 3, 5, 2, "#5B7C49");
            P(2, 5, 8, 3, "#5B7C49");
            P(4, 8, 6, 2, "#5B7C49");
            P(3, 5, 4, 2, "#9CB97A");
            P(12, 1, 4, 2, "#5B7C49");
            P(10, 3, 8, 3, "#5B7C49");
            P(10, 6, 6, 2, "#5B7C49");
            P(12, 3, 4, 2, "#ADBF88");
        }
    }

    private void DrawLandscape(DrawingContext c)
    {
        using var transform = c.PushTransform(Matrix.CreateScale(Bounds.Width / 240, Bounds.Height / 128));
        void P(double x, double y, double w, double h, string color) => c.FillRectangle(Brush.Parse(color), new Rect(x, y, w, h));
        P(0, 0, 240, 128, "#E9EBDD");
        P(168, 14, 15, 15, "#E3C98C");
        P(171, 11, 9, 21, "#E3C98C");
        P(19, 24, 28, 5, "#F8F7EB");
        P(27, 20, 12, 4, "#F8F7EB");
        P(118, 38, 32, 4, "#F8F7EB");
        P(125, 34, 17, 4, "#F8F7EB");
        P(0, 71, 30, 24, "#CCD9B7");
        P(22, 62, 34, 33, "#CCD9B7");
        P(46, 55, 41, 40, "#CCD9B7");
        P(80, 65, 37, 30, "#CCD9B7");
        P(111, 74, 45, 21, "#CCD9B7");
        P(150, 61, 42, 34, "#CCD9B7");
        P(184, 68, 56, 27, "#CCD9B7");
        P(0, 91, 240, 37, "#B5C897");
        P(0, 99, 240, 29, "#A1B780");
        P(0, 117, 240, 11, "#93AA73");
        // Little cottage, with an amber window and a crooked chimney.
        P(95, 75, 51, 37, "#B7A279");
        P(99, 78, 43, 31, "#E4D1A4");
        P(91, 73, 59, 6, "#8E6F58");
        P(96, 67, 49, 6, "#9E7B5D");
        P(101, 61, 39, 6, "#A98666");
        P(107, 55, 27, 6, "#B29170");
        P(113, 49, 15, 6, "#BA9A76");
        P(133, 51, 7, 14, "#9C856B");
        P(132, 48, 9, 4, "#826D58");
        P(136, 38, 4, 5, "#D7D7C7");
        P(139, 29, 5, 5, "#DDDECF");
        P(116, 89, 12, 22, "#887553");
        P(118, 91, 8, 20, "#A58B61");
        P(123, 101, 2, 2, "#E8CE8E");
        P(103, 85, 9, 10, "#A18E66");
        P(105, 87, 5, 6, "#F1D891");
        P(132, 85, 8, 10, "#A18E66");
        P(134, 87, 4, 6, "#F1D891");
        P(113, 112, 18, 5, "#CDBA8F");
        P(108, 117, 27, 5, "#CDBA8F");
        P(104, 122, 34, 6, "#CDBA8F");
        void Tree(int x, int y, int scale)
        {
            P(x + 8 * scale, y + 20 * scale, 4 * scale, 13 * scale, "#8D8260");
            P(x + 5 * scale, y, 10 * scale, 4 * scale, "#6F8C57");
            P(x + 2 * scale, y + 4 * scale, 16 * scale, 6 * scale, "#6F8C57");
            P(x, y + 10 * scale, 20 * scale, 9 * scale, "#6F8C57");
            P(x + 3 * scale, y + 19 * scale, 14 * scale, 5 * scale, "#6F8C57");
            P(x + 5 * scale, y + 4 * scale, 7 * scale, 5 * scale, "#8EA56C");
            P(x + 3 * scale, y + 10 * scale, 8 * scale, 7 * scale, "#8EA56C");
        }
        Tree(14, 44, 2);
        Tree(195, 49, 2);
        Tree(59, 69, 1);
        // Fence, strawberry patch, a very small chicken.
        P(4, 109, 64, 3, "#E2D6B4");
        P(4, 117, 64, 3, "#E2D6B4");
        for (var x = 8; x < 70; x += 18)
            P(x, 105, 4, 20, "#F3E9CC");
        for (var x = 152; x < 192; x += 13)
        {
            P(x, 111, 9, 3, "#82955E");
            P(x + 2, 107, 5, 4, "#64864F");
            P(x + 3, 111, 4, 4, "#C3846D");
            P(x + 2, 122, 6, 2, "#6F8D53");
            P(x + 4, 119, 3, 3, "#63814A");
        }
        P(77, 112, 9, 7, "#FFF8DF");
        P(83, 109, 5, 5, "#FFF8DF");
        P(84, 107, 3, 2, "#B97E66");
        P(88, 112, 2, 2, "#D0A65B");
        P(86, 111, 1, 1, "#5B644B");
        P(79, 119, 1, 3, "#C29B58");
        P(83, 119, 1, 3, "#C29B58");
    }
}
