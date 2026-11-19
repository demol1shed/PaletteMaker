using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using PaletteMaker.Core;

namespace PaletteMaker.Controls;

/// <summary>
/// RYB renk çarkı: tonlar çevrede (0° üstte, saat yönünde), doygunluk merkezden dışa, parlaklık <see cref="Value"/>.
/// Çark bitmap değil fırçalarla çizilir (konik gradyan + beyaz radyal örtü + siyah örtü), bu yüzden parlaklık değişimi ucuzdur.
/// Beş uyum rengi işaretçi olarak gösterilir; sürüklenince <see cref="MarkerDragged"/> (sıra, ton, doygunluk) tetiklenir.
/// Klavye: ←/→ ton ±5° (Shift ±15°), ↑/↓ doygunluk ±%5 — taban işaretçisini oynatır.
/// </summary>
public sealed class ColorWheel : Control
{
    public const double MaxSize = 360;
    private const double EdgeMargin = 14, HitRadius = 24, BaseMarkerRadius = 13, MarkerRadius = 9;
    private const int GradientStops = 72;

    public static readonly StyledProperty<IReadOnlyList<Hsv>?> MarkersProperty =
        AvaloniaProperty.Register<ColorWheel, IReadOnlyList<Hsv>?>(nameof(Markers));

    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<ColorWheel, double>(nameof(Value), 1.0);

    private static readonly IBrush HueBrush = BuildHueBrush();
    private static readonly IBrush SaturationBrush = new RadialGradientBrush
    {
        Center = new RelativePoint(0.5, 0.5, RelativeUnit.Relative),
        GradientOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative),
        RadiusX = RelativeScalar.Parse("50%"),
        RadiusY = RelativeScalar.Parse("50%"),
        GradientStops = [new GradientStop(Colors.White, 0), new GradientStop(Color.FromArgb(0, 255, 255, 255), 1)],
    };

    private int _active = -1;
    private bool _showFocus;

    static ColorWheel()
    {
        AffectsRender<ColorWheel>(MarkersProperty, ValueProperty);
        FocusableProperty.OverrideDefaultValue<ColorWheel>(true);
        CursorProperty.OverrideDefaultValue<ColorWheel>(new Cursor(StandardCursorType.Hand));
    }

    /// <summary>(işaretçi sırası, RYB tonu 0–360, doygunluk 0–1)</summary>
    public event Action<int, double, double>? MarkerDragged;

    public IReadOnlyList<Hsv>? Markers
    {
        get => GetValue(MarkersProperty);
        set => SetValue(MarkersProperty, value);
    }

    /// <summary>Parlaklık 0–1 (çarkın koyuluğu).</summary>
    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    private static IBrush BuildHueBrush()
    {
        // Konik gradyan 0° üstten başlar, saat yönünde ilerler. Her durak RYB tonunun gerçek sRGB karşılığıdır.
        var stops = new GradientStops();
        for (int i = 0; i <= GradientStops; i++)
        {
            double t = (double)i / GradientStops;
            var (r, g, b) = RybWheel.ToRgb(new Hsv(t * 360.0, 1, 1));
            stops.Add(new GradientStop(Color.FromRgb(r, g, b), t));
        }
        return new ConicGradientBrush
        {
            Center = new RelativePoint(0.5, 0.5, RelativeUnit.Relative),
            Angle = 0,
            GradientStops = stops,
        };
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double w = double.IsInfinity(availableSize.Width) ? MaxSize : availableSize.Width;
        double h = double.IsInfinity(availableSize.Height) ? MaxSize : availableSize.Height;
        double side = Math.Min(MaxSize, Math.Min(w, h));
        return new Size(side, side);
    }

    private (Point Center, double Radius) Geometry()
    {
        double side = Math.Min(Bounds.Width, Bounds.Height);
        return (new Point(Bounds.Width / 2, Bounds.Height / 2), Math.Max(0, side / 2 - EdgeMargin));
    }

    private static Point PositionOf(Hsv c, Point center, double radius)
    {
        double a = c.H * Math.PI / 180.0;
        double r = Math.Clamp(c.S, 0, 1) * radius;
        return new Point(center.X + r * Math.Sin(a), center.Y - r * Math.Cos(a));
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var (center, radius) = Geometry();
        if (radius <= 0) return;

        var disc = new Rect(center.X - radius, center.Y - radius, 2 * radius, 2 * radius);
        context.DrawEllipse(HueBrush, null, center, radius, radius);
        context.DrawEllipse(SaturationBrush, null, center, radius, radius);
        double dark = 1 - Math.Clamp(Value, 0, 1);
        if (dark > 0) context.DrawEllipse(new SolidColorBrush(Colors.Black, dark), null, center, radius, radius);
        context.DrawEllipse(null, new Pen(new SolidColorBrush(Color.FromArgb(70, 128, 128, 128)), 1), center, radius, radius);

        if (Markers is { Count: > 0 } markers)
        {
            var line = new Pen(new SolidColorBrush(Color.FromArgb(150, 255, 255, 255)), 1.5);
            var points = markers.Select(m => PositionOf(m, center, radius)).ToArray();
            foreach (var p in points) context.DrawLine(line, center, p);
            // Taban işaretçisi en üstte çizilsin.
            foreach (int i in Enumerable.Range(0, markers.Count).OrderBy(i => i == ColorHarmony.BaseIndex ? 1 : 0))
            {
                var (r, g, b) = RybWheel.ToRgb(markers[i]);
                bool isBase = i == ColorHarmony.BaseIndex;
                double mr = isBase ? BaseMarkerRadius : MarkerRadius;
                context.DrawEllipse(new SolidColorBrush(Color.FromRgb(r, g, b)), new Pen(Brushes.White, 3), points[i], mr, mr);
                context.DrawEllipse(null, new Pen(new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)), 1), points[i], mr + 1.5, mr + 1.5);
            }
        }

        if (_showFocus)
        {
            var ring = new Pen(new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD4)), 3);
            context.DrawEllipse(null, ring, center, radius + 6, radius + 6);
        }
        _ = disc;
    }

    // ---- Fare / dokunma -------------------------------------------------------------------

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        var (center, radius) = Geometry();
        var pos = e.GetPosition(this);

        // En yakın işaretçiyi (hedef içinde) tut; yoksa tabanı oraya taşı.
        int hit = -1;
        double best = HitRadius;
        if (Markers is { } markers)
            for (int i = 0; i < markers.Count; i++)
            {
                double d = Distance(PositionOf(markers[i], center, radius), pos);
                // Eşit mesafede taban tercih edilir (üst üste binenlerde taban sürüklenebilsin).
                if (d < best || (d <= best && i == ColorHarmony.BaseIndex)) { best = d; hit = i; }
            }
        _active = hit >= 0 ? hit : ColorHarmony.BaseIndex;

        e.Pointer.Capture(this);
        // Dokunmatikte bu bir sürükleme: ScrollViewer kaydırma hareketi pointer'ı çalmasın.
        e.PreventGestureRecognition();
        e.Handled = true;
        Focus(NavigationMethod.Pointer);
        if (hit < 0) Raise(pos, center, radius);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_active < 0 || !ReferenceEquals(e.Pointer.Captured, this)) return;
        var (center, radius) = Geometry();
        Raise(e.GetPosition(this), center, radius);
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_active < 0) return;
        _active = -1;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _active = -1;
    }

    private static double Distance(Point a, Point b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    private void Raise(Point pos, Point center, double radius)
    {
        double dx = pos.X - center.X, dy = pos.Y - center.Y;
        double hue = RybWheel.Normalize(Math.Atan2(dx, -dy) * 180.0 / Math.PI);
        double sat = radius <= 0 ? 0 : Math.Clamp(Math.Sqrt(dx * dx + dy * dy) / radius, 0, 1);
        MarkerDragged?.Invoke(_active, hue, sat);
    }

    // ---- Klavye ---------------------------------------------------------------------------

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        _showFocus = e.NavigationMethod is NavigationMethod.Tab or NavigationMethod.Directional;
        InvalidateVisual();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        _showFocus = false;
        InvalidateVisual();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (Markers is not { Count: > ColorHarmony.BaseIndex } markers) return;
        double hueStep = e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? 15 : 5;
        var b = markers[ColorHarmony.BaseIndex];
        (double dh, double ds) = e.Key switch
        {
            Key.Left => (-hueStep, 0.0),
            Key.Right => (hueStep, 0.0),
            Key.Up => (0.0, 0.05),
            Key.Down => (0.0, -0.05),
            _ => (double.NaN, 0.0),
        };
        if (double.IsNaN(dh)) return;
        e.Handled = true;
        MarkerDragged?.Invoke(ColorHarmony.BaseIndex, RybWheel.Normalize(b.H + dh), Math.Clamp(b.S + ds, 0, 1));
    }
}
