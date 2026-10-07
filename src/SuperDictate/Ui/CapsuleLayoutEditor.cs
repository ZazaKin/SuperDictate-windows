using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using SuperDictate.Interop;

namespace SuperDictate.Ui;

/// <summary>
/// Where the capsule goes, chosen by dragging it, in the manner of NVIDIA's
/// overlay layout. The screen dims; the capsule can be dragged anywhere and
/// snaps to the edges and centre lines, which light up when it rests on them.
/// A dashed outline shows how far it grows as words arrive, and from which
/// edge: next to the left or right side it grows away from that side.
/// Arrow keys nudge it (Shift for bigger steps), Enter keeps it, Escape cancels.
/// </summary>
internal sealed class CapsuleLayoutEditor : Window
{
    private static readonly Brush Scrim = Frozen(new SolidColorBrush(Color.FromArgb(150, 6, 8, 12)));
    private static readonly Brush GuideIdle = Frozen(new SolidColorBrush(Color.FromArgb(55, 255, 255, 255)));
    private static readonly Brush ReachStroke = Frozen(new SolidColorBrush(Color.FromArgb(120, 255, 255, 255)));
    private static readonly Brush BadgeFill = Frozen(new SolidColorBrush(Color.FromArgb(170, 0, 0, 0)));

    private readonly Canvas _canvas = new() { ClipToBounds = true };
    private readonly CapsuleView _capsule = new() { Cursor = Cursors.SizeAll };
    private readonly Border _ring = new() { BorderThickness = new Thickness(2), Opacity = 0, IsHitTestVisible = false };
    private readonly Rectangle _reach = new() { StrokeThickness = 1.5, StrokeDashArray = new DoubleCollection { 4, 3 }, IsHitTestVisible = false };
    private readonly TextBlock _growsText = new() { Foreground = Brushes.White, FontSize = 12 };
    private readonly Border _grows;
    private readonly Border _panel;
    private readonly Dictionary<CapsulePlacement.Guides, Line> _guides = new();
    private readonly ScaleTransform _lift = new(1, 1);
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly CapsulePlacement _initial;
    private readonly IntPtr _monitor;

    private Rect _area;
    private Rect _rect;
    private CapsulePlacement.Guides _snapped;
    private Vector _grab;
    private bool _dragging;

    /// <summary>Where the user left it, when they chose Done.</summary>
    public CapsulePlacement? Result { get; private set; }

    public CapsuleLayoutEditor(CapsuleStyle style, CapsulePlacement placement, Window? owner)
    {
        Title = "Move the capsule";
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Scrim;
        ShowInTaskbar = false;
        Topmost = true;
        ResizeMode = ResizeMode.NoResize;
        Resources = Theme.Create();
        FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI");
        if (owner is not null) Owner = owner;

        _initial = placement;
        // The screen the settings window is on; the choice applies to every screen anyway.
        _monitor = owner is null
            ? NativeMethods.MonitorFromPoint(new NativeMethods.POINT(), NativeMethods.MONITOR_DEFAULTTOPRIMARY)
            : NativeMethods.MonitorFromWindow(new WindowInteropHelper(owner).Handle, NativeMethods.MONITOR_DEFAULTTONEAREST);

        _capsule.Apply(style);
        _capsule.ShowText("Drag me anywhere", meter: true);
        _capsule.RenderTransformOrigin = new Point(0.5, 0.5);
        _capsule.RenderTransform = _lift;
        _ring.BorderBrush = (Brush)Resources["Link"];
        _ring.CornerRadius = new CornerRadius((14 * style.Scale) + 5);
        _reach.Stroke = ReachStroke;
        _reach.RadiusX = _reach.RadiusY = 14 * style.Scale;
        _grows = new Border { Background = BadgeFill, CornerRadius = new CornerRadius(8), Padding = new Thickness(9, 4, 9, 5), Child = _growsText, IsHitTestVisible = false };

        foreach (var guide in new[] { CapsulePlacement.Guides.Left, CapsulePlacement.Guides.CenterX, CapsulePlacement.Guides.Right, CapsulePlacement.Guides.Top, CapsulePlacement.Guides.CenterY, CapsulePlacement.Guides.Bottom })
        {
            var line = new Line { StrokeThickness = 1, IsHitTestVisible = false };
            _guides[guide] = line;
            _canvas.Children.Add(line);
        }

        _panel = Panel();
        _canvas.Children.Add(_reach);
        _canvas.Children.Add(_grows);
        _canvas.Children.Add(_panel);
        _canvas.Children.Add(_ring);
        _canvas.Children.Add(_capsule);
        Content = _canvas;

        _capsule.MouseEnter += (_, _) => ShowRing();
        _capsule.MouseLeave += (_, _) => ShowRing();
        _capsule.MouseLeftButtonDown += (_, e) =>
        {
            _dragging = true;
            _grab = e.GetPosition(_canvas) - _rect.TopLeft; // Where it was grabbed stays under the pointer.
            _capsule.CaptureMouse();
            Lift(true);
            e.Handled = true;
        };
        _capsule.MouseMove += (_, e) =>
        {
            if (!_dragging) return;
            _rect = CapsulePlacement.Snap(new Rect(e.GetPosition(_canvas) - _grab, _rect.Size), _area, out _snapped);
            Layout();
        };
        _capsule.MouseLeftButtonUp += (_, _) => Settle();
        _capsule.LostMouseCapture += (_, _) => Settle();
        PreviewKeyDown += OnKey;
        CompositionTarget.Rendering += OnFrame;
        Closed += (_, _) => CompositionTarget.Rendering -= OnFrame;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        // Covers the whole screen, taskbar included, in that screen's own pixels.
        var info = new NativeMethods.MONITORINFO { cbSize = Marshal.SizeOf<NativeMethods.MONITORINFO>() };
        if (!NativeMethods.GetMonitorInfo(_monitor, ref info)) return;
        var scale = NativeMethods.GetDpiForMonitor(_monitor, 0, out var dpi, out _) == 0 ? dpi / 96.0 : 1;
        var screen = info.rcMonitor;
        var work = info.rcWork;
        NativeMethods.SetWindowPos(new WindowInteropHelper(this).Handle, NativeMethods.HWND_TOPMOST,
            screen.Left, screen.Top, screen.Right - screen.Left, screen.Bottom - screen.Top, 0);

        _area = new Rect((work.Left - screen.Left) / scale, (work.Top - screen.Top) / scale,
            (work.Right - work.Left) / scale, (work.Bottom - work.Top) / scale);
        _rect = _initial.Place(new Size(_capsule.NarrowestWidth, _capsule.CapsuleHeight), _capsule.Widest, _area);
        PlaceGuides();
        Layout();
    }

    private Border Panel()
    {
        Button Make(string text, string? style, Action click)
        {
            var button = new Button { Content = text, MinWidth = 84, Margin = new Thickness(8, 0, 0, 0) };
            if (style is not null) button.Style = (Style)Resources[style];
            button.Click += (_, _) => click();
            return button;
        }

        var title = new TextBlock { Text = "Move the capsule", FontSize = 16, FontWeight = FontWeights.SemiBold, Foreground = (Brush)Resources["Text"] };
        var body = new TextBlock
        {
            Text = "Drag it anywhere on the screen. It snaps to the edges and the center. Next to the left or right edge it grows away from that edge as your words appear.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Brush)Resources["Muted"],
            FontSize = 13,
            Margin = new Thickness(0, 6, 0, 0),
        };
        var keys = new TextBlock
        {
            Text = "Arrow keys nudge it, Shift moves further. Enter keeps it, Esc cancels.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Brush)Resources["Muted"],
            FontSize = 12,
            Margin = new Thickness(0, 8, 0, 0),
        };
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 16, 0, 0),
            Children =
            {
                Make("Reset", null, () =>
                {
                    _rect = CapsulePlacement.TopCenter.Place(_rect.Size, _capsule.Widest, _area);
                    Layout();
                }),
                Make("Cancel", null, () => Finish(save: false)),
                Make("Done", "AccentButton", () => Finish(save: true)),
            },
        };

        return new Border
        {
            Width = 400,
            Padding = new Thickness(22, 18, 22, 18),
            CornerRadius = new CornerRadius(14),
            Background = (Brush)Resources["Card"],
            BorderBrush = (Brush)Resources["Stroke"],
            BorderThickness = new Thickness(1),
            Child = new StackPanel { Children = { title, body, keys, buttons } },
        };
    }

    private void PlaceGuides()
    {
        var usable = CapsulePlacement.Usable(_area);
        var middle = new Point(_area.Left + (_area.Width / 2), _area.Top + (_area.Height / 2));
        void Vertical(CapsulePlacement.Guides guide, double x) =>
            (_guides[guide].X1, _guides[guide].X2, _guides[guide].Y1, _guides[guide].Y2) = (x, x, _area.Top, _area.Bottom);
        void Horizontal(CapsulePlacement.Guides guide, double y) =>
            (_guides[guide].X1, _guides[guide].X2, _guides[guide].Y1, _guides[guide].Y2) = (_area.Left, _area.Right, y, y);

        Vertical(CapsulePlacement.Guides.Left, usable.Left);
        Vertical(CapsulePlacement.Guides.CenterX, middle.X);
        Vertical(CapsulePlacement.Guides.Right, usable.Right);
        Horizontal(CapsulePlacement.Guides.Top, usable.Top);
        Horizontal(CapsulePlacement.Guides.CenterY, middle.Y);
        Horizontal(CapsulePlacement.Guides.Bottom, usable.Bottom);
    }

    /// <summary>Puts everything where the capsule now is.</summary>
    private void Layout()
    {
        Canvas.SetLeft(_capsule, _rect.Left);
        Canvas.SetTop(_capsule, _rect.Top);
        _capsule.Width = _rect.Width;
        _capsule.Height = _rect.Height;
        var ring = _rect;
        ring.Inflate(5, 5);
        Canvas.SetLeft(_ring, ring.Left);
        Canvas.SetTop(_ring, ring.Top);
        _ring.Width = ring.Width;
        _ring.Height = ring.Height;

        // How far it will grow, from the edge it keeps to.
        var placement = CapsulePlacement.FromRect(_rect, _area);
        var reach = placement.Place(new Size(_capsule.Widest, _rect.Height), _capsule.Widest, _area);
        Canvas.SetLeft(_reach, reach.Left);
        Canvas.SetTop(_reach, reach.Top);
        _reach.Width = reach.Width;
        _reach.Height = reach.Height;
        _growsText.Text = placement.Horizontal switch
        {
            CapsuleEdge.Start => "Grows to the right  →",
            CapsuleEdge.End => "←  Grows to the left",
            _ => "←  Grows both ways  →",
        };
        _grows.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var below = _rect.Top + (_rect.Height / 2) < _area.Top + (_area.Height / 2);
        Canvas.SetLeft(_grows, Math.Clamp(_rect.Left + (_rect.Width / 2) - (_grows.DesiredSize.Width / 2), _area.Left, _area.Right - _grows.DesiredSize.Width));
        Canvas.SetTop(_grows, below ? _rect.Bottom + 12 : _rect.Top - 12 - _grows.DesiredSize.Height);

        // Centre lines always show faintly; a line lights up while the capsule rests on it.
        var accent = (Brush)Resources["Link"];
        foreach (var (guide, line) in _guides)
        {
            var on = _snapped.HasFlag(guide);
            var center = guide is CapsulePlacement.Guides.CenterX or CapsulePlacement.Guides.CenterY;
            line.Stroke = on ? accent : GuideIdle;
            line.StrokeDashArray = on ? null : new DoubleCollection { 6, 6 };
            line.Visibility = on || (center && _dragging) ? Visibility.Visible : Visibility.Hidden;
        }

        // The instructions stay clear of the capsule and its reach.
        _panel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var size = _panel.DesiredSize;
        var left = _area.Left + ((_area.Width - size.Width) / 2);
        var top = _area.Top + ((_area.Height - size.Height) / 2);
        var panel = new Rect(left, top, size.Width, size.Height);
        var taken = Rect.Union(_rect, reach);
        taken.Inflate(24, 24);
        if (panel.IntersectsWith(taken))
        {
            top = taken.Top + (taken.Height / 2) < _area.Top + (_area.Height / 2)
                ? _area.Bottom - size.Height - (_area.Height * 0.12)
                : _area.Top + (_area.Height * 0.12);
        }

        Canvas.SetLeft(_panel, left);
        Canvas.SetTop(_panel, top);
    }

    private void Settle()
    {
        if (!_dragging) return;
        _dragging = false;
        _capsule.ReleaseMouseCapture();
        Lift(false);
        // Settles where it will really show: the placement keeps it, at its widest, on screen.
        _rect = CapsulePlacement.FromRect(_rect, _area).Place(_rect.Size, _capsule.Widest, _area);
        _snapped = CapsulePlacement.Guides.None;
        Layout();
    }

    private void OnKey(object sender, KeyEventArgs e)
    {
        var step = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? 10 : 1;
        Vector? move = e.Key switch
        {
            Key.Left => new Vector(-step, 0),
            Key.Right => new Vector(step, 0),
            Key.Up => new Vector(0, -step),
            Key.Down => new Vector(0, step),
            _ => null,
        };

        if (move is { } by)
        {
            var usable = CapsulePlacement.Usable(_area);
            _rect = new Rect(
                Math.Clamp(_rect.Left + by.X, usable.Left, Math.Max(usable.Left, usable.Right - _rect.Width)),
                Math.Clamp(_rect.Top + by.Y, usable.Top, Math.Max(usable.Top, usable.Bottom - _rect.Height)),
                _rect.Width, _rect.Height);
            Layout();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            Finish(save: true);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            Finish(save: false);
            e.Handled = true;
        }
    }

    private void Finish(bool save)
    {
        if (save) Result = CapsulePlacement.FromRect(_rect, _area);
        DialogResult = save;
    }

    private void ShowRing() =>
        _ring.BeginAnimation(OpacityProperty, new DoubleAnimation(_capsule.IsMouseOver || _dragging ? 1 : 0, TimeSpan.FromMilliseconds(120)));

    /// <summary>Picked up, it grows a little; put down, it settles back.</summary>
    private void Lift(bool up)
    {
        var time = SystemParameters.ClientAreaAnimation ? TimeSpan.FromMilliseconds(up ? 120 : 220) : TimeSpan.Zero;
        var scale = new DoubleAnimation(up ? 1.04 : 1, time) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
        _lift.BeginAnimation(ScaleTransform.ScaleXProperty, scale);
        _lift.BeginAnimation(ScaleTransform.ScaleYProperty, scale);
        ShowRing();
    }

    /// <summary>The meter breathes, as if someone were talking.</summary>
    private void OnFrame(object? sender, EventArgs e)
    {
        var seconds = _clock.Elapsed.TotalSeconds;
        _capsule.SetPreviewLevel(0.35 + (0.25 * Math.Sin(seconds * 2.2)));
        _capsule.Render(seconds);
    }

    private static T Frozen<T>(T freezable) where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }
}
