using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;

namespace SuperDictate.Ui;

/// <summary>
/// One line of live text that builds itself word by word. Each new word rises
/// into place, sharpening out of a blur as it fades in, a beat after the word
/// before it. Words that may still change are dimmer and brighten once they
/// settle. The line widens to fit, up to its maximum; past that it glides left so
/// the newest words stay in view while the oldest fade out at the left edge.
/// Every motion starts from where things are on screen, so a fast talker never
/// makes it jump.
/// </summary>
internal sealed class LiveCaption : Canvas
{
    private const double Unsettled = 0.55;   // Opacity of words that may still change.
    private const int MostWords = 48;        // Older words, long scrolled out of view, are let go.
    private static readonly TimeSpan Arrive = TimeSpan.FromMilliseconds(420);
    private static readonly TimeSpan Glide = TimeSpan.FromMilliseconds(360);
    private static readonly TimeSpan Stagger = TimeSpan.FromMilliseconds(55);
    private static readonly IEasingFunction Out = Frozen(new QuinticEase { EasingMode = EasingMode.EaseOut });

    private readonly StackPanel _line = new() { Orientation = Orientation.Horizontal };
    private readonly TranslateTransform _shift = new();
    private Brush? _edgeFade;
    private double _fontSize = 12;
    private double _widest = 400;
    private Brush _foreground = Brushes.White;
    private int _dropped;

    public LiveCaption()
    {
        ClipToBounds = true;
        Width = 0;
        _line.RenderTransform = _shift;
        Children.Add(_line);
    }

    public bool IsEmpty => _line.Children.Count == 0;

    /// <summary>The words on screen, and how many of them are settled; for the self-test.</summary>
    internal string Shown => string.Join(" ", Words().Select(word => word.Text));

    internal int SettledShown => Words().Count(word => (bool)word.Tag);

    public void Apply(double fontSize, double widest, Brush foreground)
    {
        _fontSize = fontSize;
        _widest = widest;
        _foreground = foreground;
        Height = Math.Ceiling(fontSize * 1.45);
        foreach (var word in Words())
        {
            word.FontSize = fontSize;
            word.Foreground = foreground;
        }

        var fade = new LinearGradientBrush { MappingMode = BrushMappingMode.Absolute, StartPoint = new Point(0, 0), EndPoint = new Point(fontSize * 2.5, 0) };
        fade.GradientStops.Add(new GradientStop(Colors.Transparent, 0));
        fade.GradientStops.Add(new GradientStop(Colors.Black, 1));
        _edgeFade = Frozen(fade);
        Fit(motion: false);
    }

    /// <param name="settled">Words that won't change any more.</param>
    /// <param name="tail">Words after them that may still be corrected.</param>
    public void Show(string settled, string tail)
    {
        var settledCount = Split(settled).Count;
        var next = Split(settled).Concat(Split(tail)).ToList();
        if (next.Count < _dropped) Clear(); // Settled words only ever grow; start over rather than misalign.

        // Words that didn't change stay as they are; everything after the first change is new.
        var shown = Words().ToList();
        var keep = 0;
        while (keep < shown.Count && _dropped + keep < next.Count && shown[keep].Text == next[_dropped + keep]) keep++;
        _line.Children.RemoveRange(keep, shown.Count - keep);

        var motion = SystemParameters.ClientAreaAnimation;
        for (var index = 0; index < keep; index++)
        {
            var settledNow = _dropped + index < settledCount;
            if ((bool)shown[index].Tag == settledNow) continue;
            shown[index].Tag = settledNow;
            shown[index].BeginAnimation(OpacityProperty, new DoubleAnimation(settledNow ? 1 : Unsettled, Glide) { EasingFunction = Out });
        }

        for (var index = _dropped + keep; index < next.Count; index++)
        {
            var settledWord = index < settledCount;
            var word = new TextBlock
            {
                Text = next[index],
                Tag = settledWord,
                FontSize = _fontSize,
                Foreground = _foreground,
                Margin = new Thickness(_line.Children.Count == 0 ? 0 : _fontSize * 0.3, 0, 0, 0),
            };
            _line.Children.Add(word);
            Enter(word, settledWord ? 1 : Unsettled, (index - _dropped - keep) * Stagger, motion);
        }

        LetGoOfOldest();
        Fit(motion);
    }

    public void Clear()
    {
        _line.Children.Clear();
        _dropped = 0;
        Fit(motion: false);
    }

    private IEnumerable<TextBlock> Words() => _line.Children.OfType<TextBlock>();

    private static List<string> Split(string text) =>
        text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).ToList();

    private void Enter(TextBlock word, double opacity, TimeSpan delay, bool motion)
    {
        word.Opacity = 0;
        if (!motion)
        {
            // Windows animations are off: a short fade, nothing moves.
            word.BeginAnimation(OpacityProperty, new DoubleAnimation(opacity, TimeSpan.FromMilliseconds(120)));
            return;
        }

        var rise = new TranslateTransform(0, _fontSize * 0.5);
        var blur = new BlurEffect { Radius = _fontSize * 0.45 };
        word.RenderTransform = rise;
        word.Effect = blur;

        var land = new DoubleAnimation(0, Arrive) { BeginTime = delay, EasingFunction = Out };
        // Once landed the blur goes: left on, every word would cost an extra render pass.
        land.Completed += (_, _) => word.Effect = null;
        word.BeginAnimation(OpacityProperty, new DoubleAnimation(opacity, Arrive) { BeginTime = delay, EasingFunction = Out });
        rise.BeginAnimation(TranslateTransform.YProperty, land);
        blur.BeginAnimation(BlurEffect.RadiusProperty, new DoubleAnimation(0, Arrive) { BeginTime = delay, EasingFunction = Out });
    }

    /// <summary>
    /// Drops the oldest words once there are many. They are settled and far off
    /// the left edge; the line moves right by their width, so nothing on screen shifts.
    /// </summary>
    private void LetGoOfOldest()
    {
        var extra = _line.Children.Count - MostWords;
        if (extra <= 0) return;

        _line.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var width = Words().Take(extra).Sum(word => word.DesiredSize.Width);
        _line.Children.RemoveRange(0, extra);
        _dropped += extra;

        var x = _shift.X + width;
        _shift.BeginAnimation(TranslateTransform.XProperty, null);
        _shift.X = x;
    }

    /// <summary>Widens to the text, up to the maximum, and keeps the newest words at the right edge.</summary>
    private void Fit(bool motion)
    {
        _line.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var total = _line.DesiredSize.Width;
        var width = Math.Min(total, _widest);
        OpacityMask = total > _widest ? _edgeFade : null;
        _width = width;
        _offset = width - total;
        if (motion && !double.IsNaN(Width)) return; // Follow eases the line there, frame by frame.
        Width = width;
        _shift.X = _offset;
    }

    private double _width;
    private double _offset;
    private double _followed = double.NaN;

    /// <summary>
    /// Moves the line's width and glide toward where the words want them, called once a
    /// frame: a spring that keeps its speed from one word to the next, so the capsule
    /// grows in one smooth motion rather than a kick per word. A still (no time passing)
    /// lands at once.
    /// </summary>
    public void Follow(double seconds)
    {
        var step = seconds - _followed;
        _followed = seconds;
        if (!(step > 0 && step < 0.5) || double.IsNaN(Width))
        {
            Width = _width;
            _shift.X = _offset;
            return;
        }

        var pull = 1 - Math.Exp(-step / 0.14);
        Width += (_width - Width) * pull;
        _shift.X += (_offset - _shift.X) * pull;
    }


    private static T Frozen<T>(T freezable) where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }
}
