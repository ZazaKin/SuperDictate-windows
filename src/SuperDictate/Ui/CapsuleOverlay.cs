using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;
using SuperDictate.Interop;

namespace SuperDictate.Ui;

public enum OverlayState
{
    Hidden,
    Recording,
    Transcribing,
}

/// <summary>
/// The capsule that slides down from the top of the screen while dictating. It
/// must never take focus: activating it would move focus away from the field
/// that is about to receive the paste.
/// </summary>
public sealed class CapsuleOverlay : Window
{
    private const int BarCount = 5;

    // Gap between the top of the work area and the capsule, and room for its shadow (DIPs).
    private const double TopGap = 12;
    private const double ShadowRoom = 16;

    // Strong ease-out, cubic-bezier(0.23, 1, 0.32, 1): arrives fast, settles softly.
    private static readonly KeySpline EaseOut = Frozen(new KeySpline(0.23, 1, 0.32, 1));
    private static readonly TimeSpan EnterTime = TimeSpan.FromMilliseconds(220);
    private static readonly TimeSpan ExitTime = TimeSpan.FromMilliseconds(160);
    private static readonly TimeSpan FadeOnlyTime = TimeSpan.FromMilliseconds(120);

    private static readonly Brush CapsuleFill = Frozen(new SolidColorBrush(Color.FromRgb(24, 24, 28)));
    private static readonly Brush Hairline = Frozen(new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)));

    private readonly List<Rectangle> _bars = new();
    private readonly StackPanel _barStrip = new() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
    private readonly TextBlock _caption = new()
    {
        Foreground = Brushes.White,
        Opacity = 0.75,
        HorizontalAlignment = HorizontalAlignment.Center,
        TextTrimming = TextTrimming.CharacterEllipsis,
    };

    private readonly Border _shadow;
    private readonly Border _capsule;
    private readonly Grid _body = new() { VerticalAlignment = VerticalAlignment.Top, HorizontalAlignment = HorizontalAlignment.Left };
    private readonly TranslateTransform _slide = new();
    private readonly DispatcherTimer _noticeTimer = new() { Interval = TimeSpan.FromSeconds(1.6) };
    private readonly Stopwatch _clock = Stopwatch.StartNew();

    private double _scale = 1;
    private double _capsuleHeight;
    private double _level;
    private double _shownLevel;
    private bool _processing;
    private bool _shown;
    private bool _rendering;

    public CapsuleOverlay(double scale, string accent)
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        ResizeMode = ResizeMode.NoResize;
        IsHitTestVisible = false;
        Focusable = false;

        for (var index = 0; index < BarCount; index++)
        {
            var bar = new Rectangle { VerticalAlignment = VerticalAlignment.Center };
            _bars.Add(bar);
            _barStrip.Children.Add(bar);
        }

        // The shadow is its own layer so the effect never re-renders the bars or blurs the text.
        _shadow = new Border
        {
            Background = CapsuleFill,
            Effect = new DropShadowEffect { BlurRadius = 16, ShadowDepth = 3, Direction = 270, Opacity = 0.35 },
        };
        // The hairline keeps the edge visible over dark apps.
        _capsule = new Border
        {
            Background = CapsuleFill,
            BorderBrush = Hairline,
            BorderThickness = new Thickness(1),
            Child = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { _barStrip, _caption } },
        };
        _body.Children.Add(_shadow);
        _body.Children.Add(_capsule);
        _body.RenderTransform = _slide;
        _body.Opacity = 0;
        Content = new Grid { Children = { _body } };

        _noticeTimer.Tick += (_, _) =>
        {
            _noticeTimer.Stop();
            SlideOut();
        };

        Apply(scale, accent);

        // Create the window now so the first dictation doesn't pay for it.
        new WindowInteropHelper(this).EnsureHandle();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        // Transparent to hit testing, absent from Alt+Tab, and never activated.
        var handle = new WindowInteropHelper(this).Handle;
        var style = (long)NativeMethods.GetWindowLongPtr(handle, NativeMethods.GWL_EXSTYLE);
        style |= NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_TRANSPARENT | NativeMethods.WS_EX_TOOLWINDOW;
        NativeMethods.SetWindowLongPtr(handle, NativeMethods.GWL_EXSTYLE, new IntPtr(style));
    }

    /// <summary>Size and accent from settings. Takes effect at once, even while the capsule is shown.</summary>
    public void Apply(double scale, string accent)
    {
        _scale = scale;
        var width = 168 * scale;
        _capsuleHeight = 48 * scale;
        Width = width + (2 * ShadowRoom);
        Height = TopGap + _capsuleHeight + ShadowRoom;

        Brush accentBrush;
        try
        {
            accentBrush = Frozen((SolidColorBrush)new BrushConverter().ConvertFromString(accent)!);
        }
        catch (FormatException)
        {
            accentBrush = Frozen(new SolidColorBrush(Color.FromRgb(0x5B, 0x8D, 0xEF)));
        }

        foreach (var bar in _bars)
        {
            bar.Width = 4 * scale;
            bar.Height = 5 * scale;
            bar.RadiusX = bar.RadiusY = 2 * scale;
            bar.Margin = new Thickness(3 * scale, 0, 3 * scale, 0);
            bar.Fill = accentBrush;
        }

        // A fixed-height strip: the bars grow inside it, so the caption never moves.
        _barStrip.Height = 20 * scale;
        _caption.FontSize = 11 * scale;
        _caption.Margin = new Thickness(0, 1 * scale, 0, 0);
        _caption.MaxWidth = width - (28 * scale);
        _shadow.CornerRadius = new CornerRadius(14 * scale);
        _capsule.CornerRadius = new CornerRadius(14 * scale);
        _capsule.Padding = new Thickness(14 * scale, 5 * scale, 14 * scale, 5 * scale);
        _body.Width = width;
        _body.Height = _capsuleHeight;
        _body.Margin = new Thickness(ShadowRoom, TopGap, ShadowRoom, 0);

        if (_shown)
        {
            MoveToActiveScreen();
        }
        else
        {
            _slide.BeginAnimation(TranslateTransform.YProperty, null);
            _slide.Y = HiddenOffset;
        }
    }

    public void SetState(OverlayState state, string caption)
    {
        _noticeTimer.Stop();
        if (state == OverlayState.Hidden)
        {
            SlideOut();
            return;
        }

        ShowContent(bars: true, caption, processing: state == OverlayState.Transcribing);

        // Screen readers hear what happens after recording. "Listening" is not
        // announced: speech from the speakers would end up in the recording.
        if (_processing) Announce(caption);
        SlideIn();
    }

    /// <summary>A short message such as "Copied last transcript" that slides away by itself.</summary>
    public void Notify(string message)
    {
        ShowContent(bars: false, message, processing: false);
        Announce(message);
        SlideIn();
        _noticeTimer.Stop();
        _noticeTimer.Start();
    }

    /// <summary>The capsule as it looks while listening, for the settings preview.</summary>
    public void Preview()
    {
        ShowContent(bars: true, "Listening…", processing: false);
        _level = 0.6;
        SlideIn();
        _noticeTimer.Stop();
        _noticeTimer.Start();
    }

    public void UpdateLevel(double rootMeanSquare)
    {
        // Light smoothing so the capsule does not flicker on plosives.
        _level = (_level * 0.7) + (Math.Clamp(rootMeanSquare * 6, 0, 1) * 0.3);
    }

    private double HiddenOffset => -(TopGap + _capsuleHeight + ShadowRoom + 4);

    private void ShowContent(bool bars, string caption, bool processing)
    {
        _barStrip.Visibility = bars ? Visibility.Visible : Visibility.Collapsed;
        _caption.Text = caption;
        _processing = processing;
    }

    private void SlideIn()
    {
        if (!_shown)
        {
            _shown = true;
            MoveToActiveScreen();
            StartRendering();
        }

        Animate(show: true);
    }

    private void SlideOut()
    {
        if (!_shown) return;
        _shown = false;
        Animate(show: false, whenDone: () =>
        {
            if (_shown) return;
            StopRendering();
            _slide.BeginAnimation(TranslateTransform.YProperty, null);
            _slide.Y = HiddenOffset;
        });
    }

    /// <summary>
    /// Slides and fades the capsule in or out. Each call starts from wherever the
    /// capsule is now, so a quick stop-start never makes it jump.
    /// </summary>
    private void Animate(bool show, Action? whenDone = null)
    {
        var moving = SystemParameters.ClientAreaAnimation;
        var time = !moving ? FadeOnlyTime : show ? EnterTime : ExitTime;

        if (moving)
        {
            _slide.BeginAnimation(TranslateTransform.YProperty, Eased(show ? 0 : HiddenOffset, time));
        }
        else
        {
            // Windows animations are turned off: no movement, only a short fade.
            _slide.BeginAnimation(TranslateTransform.YProperty, null);
            _slide.Y = 0;
        }

        var fade = Eased(show ? 1 : 0, time);
        if (whenDone is not null) fade.Completed += (_, _) => whenDone();
        _body.BeginAnimation(OpacityProperty, fade);
    }

    private static DoubleAnimationUsingKeyFrames Eased(double to, TimeSpan time)
    {
        var animation = new DoubleAnimationUsingKeyFrames { Duration = time };
        animation.KeyFrames.Add(new SplineDoubleKeyFrame(to, KeyTime.FromTimeSpan(time), EaseOut));
        return animation;
    }

    private void StartRendering()
    {
        if (_rendering) return;
        _rendering = true;
        CompositionTarget.Rendering += OnFrame;
    }

    private void StopRendering()
    {
        if (!_rendering) return;
        _rendering = false;
        CompositionTarget.Rendering -= OnFrame;
        _level = _shownLevel = 0;
    }

    /// <summary>Runs once per display frame while the capsule is on screen.</summary>
    private void OnFrame(object? sender, EventArgs e)
    {
        // With Windows animations turned off, the bars still show the voice level, without the wave.
        var calm = !SystemParameters.ClientAreaAnimation;
        var phase = _clock.Elapsed.TotalSeconds * 9;
        _shownLevel += (_level - _shownLevel) * 0.3;

        for (var index = 0; index < _bars.Count; index++)
        {
            var wave = calm ? 1.0 : 0.5 + (0.5 * Math.Sin(phase + (index * 0.7)));

            // Recording follows the voice. Processing is a low, even ripple that no
            // longer reacts to sound, so it never looks like it is still listening.
            var fill = _processing
                ? (calm ? 0.35 : 0.2 + (0.3 * wave))
                : wave * _shownLevel;
            _bars[index].Height = (5 + (15 * fill)) * _scale;
        }
    }

    private void Announce(string text) =>
        UIElementAutomationPeer.CreatePeerForElement(_caption)?.RaiseNotificationEvent(
            AutomationNotificationKind.Other,
            AutomationNotificationProcessing.ImportantMostRecent,
            text,
            "SuperDictate.Status");

    /// <summary>
    /// Top centre of the monitor the user is working on. Placed in physical pixels
    /// with that monitor's DPI, so it stays centred on mixed-DPI setups.
    /// </summary>
    private void MoveToActiveScreen()
    {
        var handle = new WindowInteropHelper(this).EnsureHandle();

        // Shown once, then kept up: while idle it is empty, transparent and click-through.
        if (!IsVisible) Show();

        var foreground = NativeMethods.GetForegroundWindow();
        IntPtr monitor;
        if (foreground != IntPtr.Zero)
        {
            monitor = NativeMethods.MonitorFromWindow(foreground, NativeMethods.MONITOR_DEFAULTTONEAREST);
        }
        else
        {
            NativeMethods.GetCursorPos(out var cursor);
            monitor = NativeMethods.MonitorFromPoint(cursor, NativeMethods.MONITOR_DEFAULTTONEAREST);
        }

        var info = new NativeMethods.MONITORINFO { cbSize = Marshal.SizeOf<NativeMethods.MONITORINFO>() };
        if (!NativeMethods.GetMonitorInfo(monitor, ref info)) return;

        var scale = NativeMethods.GetDpiForMonitor(monitor, 0, out var dpi, out _) == 0
            ? dpi / 96.0
            : VisualTreeHelper.GetDpi(this).DpiScaleX;
        var left = CenteredLeft(info.rcWork.Left, info.rcWork.Right, Width * scale);

        // Re-asserting topmost also lifts it above windows that went topmost since.
        NativeMethods.SetWindowPos(
            handle, NativeMethods.HWND_TOPMOST, (int)Math.Round(left), info.rcWork.Top, 0, 0,
            NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
    }

    /// <summary>Left edge, in screen pixels, that centres a window of this width in the work area.</summary>
    internal static double CenteredLeft(double areaLeft, double areaRight, double width) =>
        areaLeft + Math.Max(0, (areaRight - areaLeft - width) / 2);

    private static T Frozen<T>(T freezable) where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }
}
