using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using SuperDictate.Storage;

namespace SuperDictate.Ui;

/// <summary>How the capsule looks; everything the Capsule settings page changes except where it sits.</summary>
/// <param name="MaxWidth">How wide it may grow as words arrive, at size 1×.</param>
public sealed record CapsuleStyle(string Skin, string Accent, double Scale, double Opacity, string Meter, bool Timer,
    double MaxWidth = CapsuleView.DefaultWidest)
{
    public static CapsuleStyle From(Settings settings) => new(
        settings.CapsuleSkin, settings.CapsuleAccent, settings.CapsuleScale, settings.CapsuleOpacity,
        settings.CapsuleMeter, settings.CapsuleTimer, settings.CapsuleMaxWidth);
}

/// <summary>The look, where it sits, on which screen, and whether words show: what the overlay needs.</summary>
/// <param name="LiveGlass">Liquid Glass shows the live screen behind it.</param>
public sealed record CapsuleLook(CapsuleStyle Style, CapsulePlacement Placement, bool PrimaryScreen, bool LiveText = true,
    bool LiveGlass = false)
{
    public static CapsuleLook From(Settings settings) => new(
        CapsuleStyle.From(settings),
        new CapsulePlacement(Parse(settings.CapsuleHorizontal), settings.CapsuleX, Parse(settings.CapsuleVertical), settings.CapsuleY),
        settings.CapsuleScreen == "primary",
        settings.CapsuleLiveText,
        settings.CapsuleLiveGlass);

    public static string Name(CapsuleEdge edge) => edge.ToString().ToLowerInvariant();

    private static CapsuleEdge Parse(string? value) =>
        Enum.TryParse<CapsuleEdge>(value, ignoreCase: true, out var edge) ? edge : CapsuleEdge.Center;
}

/// <summary>
/// The capsule itself, as the overlay, the position editor, the skin gallery and
/// the settings preview all draw it: a voice meter over a caption, or over the
/// live words once they arrive, with an optional recording time. Its owner calls
/// <see cref="Render"/> once per frame while the meter should move.
/// </summary>
internal sealed class CapsuleView : Grid
{
    public const double BaseWidth = 168;
    public const double DefaultWidest = 460;
    public const double LeastWidest = 240;
    public const double MostWidest = 900;
    public const double BaseHeight = 48;

    /// <summary>The voice meters, in the order the settings offer them.</summary>
    public static readonly string[] Meters = { "bars", "wave", "dots", "scope" };

    private const double ScopeWidth = 46;

    private readonly Grid _frame = new();
    private readonly Border _shadow = new();
    private readonly Border _body = new();
    // An art skin's moving picture, under the words.
    private readonly Border _backdrop = new() { IsHitTestVisible = false };
    private readonly StackPanel _content = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly Border _sheen = new() { IsHitTestVisible = false, VerticalAlignment = VerticalAlignment.Top };
    private readonly Grid _meter = new() { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    private readonly List<FrameworkElement> _marks = new();
    private readonly TextBlock _timer = new() { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _caption = new() { HorizontalAlignment = HorizontalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly LiveCaption _live = new() { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };

    private static readonly Brush Sheen = Frozen(new LinearGradientBrush(
        Color.FromArgb(70, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), 90));

    private CapsuleStyle _style = new("midnight", "#5B8DEF", 1, 1, "bars", false);
    private double _level;
    private double _shownLevel;
    private bool _processing;
    private bool _meterShown = true;
    private TimeSpan? _elapsed;
    private bool? _glassLight;
    private CapsuleArt? _art;
    private string? _artId;
    private PolyLineSegment? _scope;

    public CapsuleView()
    {
        Typography.SetNumeralAlignment(_timer, FontNumeralAlignment.Tabular);
        _content.Children.Add(new Grid { Children = { _meter, _timer } });
        _content.Children.Add(new Grid { Children = { _caption, _live } });
        _body.Child = _content;
        // The shadow is its own layer so the effect never re-renders the meter or blurs the text.
        _shadow.Effect = new DropShadowEffect { BlurRadius = 16, ShadowDepth = 3, Direction = 270, Opacity = 0.35 };
        _frame.Children.Add(_shadow);
        _frame.Children.Add(_backdrop);
        _frame.Children.Add(_body);
        _frame.Children.Add(_sheen);
        Children.Add(_frame);
        Apply(_style);
    }

    public CapsuleStyle Current => _style;

    public double CapsuleHeight => BaseHeight * _style.Scale;

    public double NarrowestWidth => BaseWidth * _style.Scale;

    public double Widest => WidestAtOne * _style.Scale;

    private double WidestAtOne => Math.Clamp(_style.MaxWidth, LeastWidest, MostWidest);

    /// <summary>The capsule's own shape inside the view, for the live glass behind it.</summary>
    public FrameworkElement Body => _body;

    /// <summary>Liquid Glass is a true capsule, as on Apple's devices; the other skins have rounded corners.</summary>
    public double Rounding => CapsuleSkin.Find(_style.Skin).Glass ? CapsuleHeight / 2 : 14 * _style.Scale;

    /// <summary>
    /// With live glass under it (<see cref="LiveGlass"/>), Liquid Glass draws only its
    /// words, dark on light glass or white on dark glass; null paints the glass itself.
    /// </summary>
    public void GlassUnder(bool? light)
    {
        if (light == _glassLight) return;
        _glassLight = light;
        Apply(_style);
    }

    public void Apply(CapsuleStyle style)
    {
        _style = style;
        var skin = CapsuleSkin.Find(style.Skin);
        var scale = style.Scale;
        var accent = Accent(style.Accent);
        // The live glass draws the fill, the rim and the light; this keeps to the words.
        var live = skin.Glass && _glassLight is not null;
        var fill = live ? Brushes.Transparent : skin.FillBrush();
        var muted = new SolidColorBrush(!live ? skin.Muted : _glassLight == true ? Color.FromRgb(0x3C, 0x40, 0x48) : Color.FromRgb(0xE6, 0xE8, 0xEE));
        muted.Freeze();

        _frame.Opacity = Math.Clamp(style.Opacity, 0.5, 1);
        _frame.MinWidth = BaseWidth * scale;
        _frame.Height = BaseHeight * scale;
        var corner = new CornerRadius(Rounding);
        _shadow.CornerRadius = corner;
        _shadow.Background = fill;
        // A see-through skin would show its own shadow through itself; it floats on its rim instead.
        _shadow.Visibility = skin.Fill.A == 255 ? Visibility.Visible : Visibility.Collapsed;
        _body.CornerRadius = corner;
        _body.Background = fill;
        _body.BorderBrush = live ? Brushes.Transparent : skin.BorderBrush(accent.Color);
        _body.BorderThickness = new Thickness(skin.BorderWidth);
        _body.Padding = new Thickness(14 * scale, 5 * scale, 14 * scale, 5 * scale);

        // An art skin paints its own fill: its picture, cut to the capsule, with the rim on top.
        if (skin.Art != _artId)
        {
            _artId = skin.Art;
            _art = CapsuleArt.For(skin.Art);
            _backdrop.Child = _art;
        }

        _backdrop.Visibility = _art is null ? Visibility.Collapsed : Visibility.Visible;
        if (_art is not null)
        {
            _art.Radius = Rounding;
            _body.Background = Brushes.Transparent;
        }

        // Over a picture, the words get a soft halo in the opposite tone, so every letter stays crisp.
        var darkWords = skin.Text.R + skin.Text.G + skin.Text.B < 384;
        _content.Effect = _art is null ? null : new DropShadowEffect
        {
            Color = darkWords ? Colors.White : Colors.Black,
            BlurRadius = 6 * scale,
            ShadowDepth = 0,
            Opacity = darkWords ? 0.85 : 0.75,
        };

        // Glass catches the light: a soft sheen over its top half.
        _sheen.Visibility = skin.Glass && !live ? Visibility.Visible : Visibility.Collapsed;
        _sheen.Margin = new Thickness(skin.BorderWidth);
        _sheen.Height = BaseHeight * scale * 0.55;
        _sheen.CornerRadius = new CornerRadius(Rounding - skin.BorderWidth, Rounding - skin.BorderWidth, 0, 0);
        _sheen.Background = Sheen;

        _caption.Foreground = muted;
        _caption.FontSize = 11 * scale;
        _caption.Margin = new Thickness(0, 1 * scale, 0, 0);
        _caption.MaxWidth = (BaseWidth - 28) * scale;
        _timer.Foreground = muted;
        _timer.FontSize = 10 * scale;
        var text = new SolidColorBrush(!live ? skin.Text : _glassLight == true ? Color.FromRgb(0x16, 0x18, 0x1D) : Colors.White);
        text.Freeze();
        _live.Apply(12.5 * scale, (WidestAtOne - 30) * scale, text);

        BuildMeter(accent, scale);
        Render(0);
    }

    /// <summary>A notice or a status ("Listening…"), with or without the meter.</summary>
    public void ShowText(string caption, bool meter, bool processing = false)
    {
        _caption.Text = caption;
        _meterShown = meter;
        _meter.Visibility = meter ? Visibility.Visible : Visibility.Collapsed;
        _processing = processing;
        Refresh();
    }

    public void ShowDraft(string settled, string tail)
    {
        _live.Show(settled, tail);
        Refresh();
    }

    public void ClearDraft()
    {
        _live.Clear();
        Refresh();
    }

    /// <summary>The recording time, shown when the style asks for it; null hides it.</summary>
    public TimeSpan? Elapsed
    {
        set
        {
            _elapsed = value;
            Refresh();
        }
    }

    public void SetLevel(double rootMeanSquare) =>
        _level = (_level * 0.7) + (Math.Clamp(rootMeanSquare * 6, 0, 1) * 0.3); // Light smoothing against plosives.

    /// <summary>A made-up voice level for previews.</summary>
    public void SetPreviewLevel(double level) => _level = level;

    public void Quiet() => _level = _shownLevel = 0;

    /// <summary>Tells screen readers what the capsule says.</summary>
    public void Announce(string text) =>
        UIElementAutomationPeer.CreatePeerForElement(_caption)?.RaiseNotificationEvent(
            AutomationNotificationKind.Other,
            AutomationNotificationProcessing.ImportantMostRecent,
            text,
            "SuperDictate.Status");

    /// <summary>
    /// Moves the meter for this moment. Recording, it follows the voice; processing,
    /// it settles into a low ripple that no longer reacts to sound, so it never looks
    /// like it is still listening. With Windows animations off it holds still.
    /// </summary>
    public void Render(double seconds)
    {
        var calm = !SystemParameters.ClientAreaAnimation;
        var scale = _style.Scale;
        var phase = seconds * 9;
        _shownLevel += (_level - _shownLevel) * 0.3;
        _art?.Render(calm ? 0 : seconds, _processing ? 0 : _shownLevel);

        if (_style.Meter == "dots")
        {
            // Each column lights from the bottom up, the top dot only on the loudest moments.
            for (var column = 0; column < 7; column++)
            {
                var wave = calm ? 1.0 : 0.5 + (0.5 * Math.Sin(phase + (column * 0.7)));
                var fill = _processing ? (calm ? 0.35 : 0.2 + (0.3 * wave)) : wave * _shownLevel;
                for (var row = 0; row < 3; row++) _marks[(column * 3) + row].Opacity = Math.Clamp((fill * 3) - row, 0.18, 1);
            }

            return;
        }

        if (_style.Meter == "scope" && _scope is not null)
        {
            // A trace like an oscilloscope's, pinned at both ends; it swings with the voice.
            var swing = (_processing ? (calm ? 1.5 : 1.2 + (0.8 * (0.5 + (0.5 * Math.Sin(phase * 0.5))))) : 1.2 + (7.2 * _shownLevel)) * scale;
            var points = _scope.Points;
            for (var index = 0; index < points.Count; index++)
            {
                var x = index / (double)(points.Count - 1);
                var shape = (0.7 * Math.Sin((x * 14) + (phase * 1.3))) + (0.3 * Math.Sin((x * 31) - (phase * 0.8)));
                points[index] = new Point(x * ScopeWidth * scale, (10 * scale) + (swing * Math.Sin(Math.PI * x) * shape));
            }

            return;
        }

        var wavy = _style.Meter == "wave";
        var lowest = wavy ? 3 : 5;
        var range = wavy ? 17 : 15;
        for (var index = 0; index < _marks.Count; index++)
        {
            var wave = calm ? 1.0 : 0.5 + (0.5 * Math.Sin(phase + (index * (wavy ? 0.45 : 0.7))));
            // The wave is taller in the middle, like a voice print.
            var shape = wavy ? Math.Sin(Math.PI * (index + 0.5) / _marks.Count) : 1;
            var fill = _processing ? (calm ? 0.35 : 0.2 + (0.3 * wave)) : wave * _shownLevel * shape;
            _marks[index].Height = (lowest + (range * fill)) * scale;
        }
    }

    /// <summary>The meter's strip, and where its marks reach in it now; for the self-test.</summary>
    internal (double Strip, Rect Ink) MeterReach()
    {
        var ink = Rect.Empty;
        foreach (var mark in _marks)
        {
            var bounds = mark is Path path ? path.Data.GetRenderBounds(new Pen(null, path.StrokeThickness)) : new Rect(mark.RenderSize);
            ink.Union(mark.TransformToAncestor(_meter).TransformBounds(bounds));
        }

        return (_meter.Height, ink);
    }

    private void Refresh()
    {
        _caption.Visibility = _live.IsEmpty ? Visibility.Visible : Visibility.Collapsed;
        var showTimer = _style.Timer && _meterShown && _elapsed is not null;
        _timer.Visibility = showTimer ? Visibility.Visible : Visibility.Collapsed;
        if (showTimer) _timer.Text = $"{(int)_elapsed!.Value.TotalMinutes}:{_elapsed.Value.Seconds:00}";
    }

    private void BuildMeter(SolidColorBrush accent, double scale)
    {
        _meter.Children.Clear();
        _marks.Clear();
        _scope = null;
        // A fixed-height strip: the meter moves inside it, so the text never shifts, and
        // nothing it draws may leave it (the self-test checks every meter at full voice).
        _meter.Height = 20 * scale;
        _meter.ClipToBounds = true;

        if (_style.Meter == "dots")
        {
            // A small LED matrix: seven columns of three dots.
            var pitch = 5 * scale;
            var size = 3.4 * scale;
            var matrix = new Canvas { Width = 7 * pitch, Height = 3 * pitch, VerticalAlignment = VerticalAlignment.Center };
            for (var column = 0; column < 7; column++)
            {
                for (var row = 0; row < 3; row++)
                {
                    var dot = new Ellipse { Width = size, Height = size, Fill = accent };
                    Canvas.SetLeft(dot, (column * pitch) + ((pitch - size) / 2));
                    Canvas.SetTop(dot, ((2 - row) * pitch) + ((pitch - size) / 2));
                    _marks.Add(dot);
                    matrix.Children.Add(dot);
                }
            }

            _meter.Children.Add(matrix);
            return;
        }

        if (_style.Meter == "scope")
        {
            _scope = new PolyLineSegment(Enumerable.Range(0, 40).Select(index => new Point(index * ScopeWidth * scale / 39, 10 * scale)), true);
            var trace = new Path
            {
                Width = ScopeWidth * scale,
                Height = 20 * scale,
                Stroke = accent,
                StrokeThickness = 1.6 * scale,
                StrokeLineJoin = PenLineJoin.Round,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                Data = new PathGeometry(new[] { new PathFigure(new Point(0, 10 * scale), new[] { _scope }, false) }),
            };
            _marks.Add(trace);
            _meter.Children.Add(trace);
            return;
        }

        var wavy = _style.Meter == "wave";
        var strip = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        for (var index = 0; index < (wavy ? 13 : 5); index++)
        {
            var bar = new Rectangle
            {
                Width = (wavy ? 2 : 4) * scale,
                RadiusX = (wavy ? 1 : 2) * scale,
                RadiusY = (wavy ? 1 : 2) * scale,
                Margin = new Thickness((wavy ? 1.5 : 3) * scale, 0, (wavy ? 1.5 : 3) * scale, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Fill = accent,
            };
            _marks.Add(bar);
            strip.Children.Add(bar);
        }

        _meter.Children.Add(strip);
    }

    private static T Frozen<T>(T freezable) where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }

    private static SolidColorBrush Accent(string hex)
    {
        SolidColorBrush brush;
        try
        {
            brush = (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
        }
        catch (FormatException)
        {
            brush = new SolidColorBrush(Color.FromRgb(0x5B, 0x8D, 0xEF));
        }

        brush.Freeze();
        return brush;
    }
}
