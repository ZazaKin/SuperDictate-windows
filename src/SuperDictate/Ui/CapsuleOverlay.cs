using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
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
/// The window that shows the capsule while dictating, wherever the user put it
/// (see <see cref="CapsulePlacement"/>). It must never take focus: activating it
/// would move focus away from the field that is about to receive the paste.
///
/// The window is as wide as the capsule can grow. The capsule sits against the
/// window's side that matches its anchored edge, so as words arrive it grows
/// into the free room and the window itself never moves.
/// </summary>
public sealed class CapsuleOverlay : Window
{
    // Room around the capsule for its shadow, and for the live glass to bend and blur in (DIPs).
    private double ShadowRoom => 24 * Math.Max(1, _view.Current.Scale);

    // Strong ease-out, cubic-bezier(0.23, 1, 0.32, 1): arrives fast, settles softly.
    private static readonly KeySpline EaseOut = Frozen(new KeySpline(0.23, 1, 0.32, 1));
    private static readonly TimeSpan EnterTime = TimeSpan.FromMilliseconds(240);
    private static readonly TimeSpan ExitTime = TimeSpan.FromMilliseconds(170);
    private static readonly TimeSpan FadeOnlyTime = TimeSpan.FromMilliseconds(120);

    private readonly CapsuleView _view = new();
    private readonly LiveGlass _glass = new();
    private readonly TranslateTransform _slide = new();
    private readonly ScaleTransform _zoom = new(1, 1);
    private readonly DispatcherTimer _noticeTimer = new() { Interval = TimeSpan.FromSeconds(1.6) };
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly Stopwatch _recording = new();
    private readonly DispatcherTimer _stageTimer = new() { Interval = TimeSpan.FromMilliseconds(420) };

    private CapsulePlacement _placement = CapsulePlacement.TopCenter;
    private bool _primaryScreen;
    private bool _shown;
    private bool _rendering;
    private bool _dictating;
    private bool _staging;
    private bool _stageAfterNotice;
    private bool _liveText = true;
    private bool _glassWanted;
    private bool _glassOn;
    private bool _glassLive;
    private NativeMethods.RECT? _copiedAt;
    private NativeMethods.RECT _seen;
    private int _steady;
    private int _clearing;
    private int _stageStep;
    private long _shownSecond = -1;

    public CapsuleOverlay(CapsuleLook look)
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

        _view.RenderTransform = new TransformGroup { Children = { _zoom, _slide } };
        _view.Opacity = 0;
        _view.VerticalAlignment = VerticalAlignment.Center;
        Content = new System.Windows.Controls.Grid { Children = { _glass, _view } };

        _noticeTimer.Tick += (_, _) =>
        {
            _noticeTimer.Stop();
            // A notice over the settings page's sample hands the capsule back to it.
            if (_stageAfterNotice)
            {
                _stageAfterNotice = false;
                BeginStage();
                return;
            }

            SlideOut();
        };
        _stageTimer.Tick += (_, _) => StageTick();

        Apply(look);

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
        UseGlass();
    }

    protected override void OnClosed(EventArgs e)
    {
        _glass.Dispose();
        base.OnClosed(e);
    }

    /// <summary>
    /// The glass follows the screen when Windows keeps the window out of its own
    /// screen copy. Windows 10 can't for a see-through window like this one, so
    /// there it shows a copy taken while the capsule was clear (<see cref="KeepCopyCurrent"/>).
    /// Without the shader, Liquid Glass stays painted.
    /// </summary>
    private void UseGlass()
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero) return;
        _glassOn = _glassWanted && LiveGlass.Available;
        _glassLive = _glassOn && LiveGlass.KeepOutOfCopies(handle, true);
        if (!_glassWanted) LiveGlass.KeepOutOfCopies(handle, false);
        _copiedAt = null;
        EndClearing();
        if (_glassOn) return;
        _glass.Hide();
        _view.GlassUnder(null);
    }

    /// <summary>Whether the Liquid Glass skin draws its glass, and whether that glass follows the screen; for the self-test.</summary>
    internal (bool On, bool Live) Glass => (_glassOn, _glassLive);

    /// <summary>Look, place and screen from settings. Takes effect at once, even while the capsule shows.</summary>
    public void Apply(CapsuleLook look)
    {
        _view.Apply(look.Style);
        _view.Margin = new Thickness(ShadowRoom);
        _placement = look.Placement;
        _primaryScreen = look.PrimaryScreen;
        _liveText = look.LiveText;
        var glass = look.LiveGlass && CapsuleSkin.Find(look.Style.Skin).Glass;
        if (glass != _glassWanted)
        {
            _glassWanted = glass;
            UseGlass();
        }

        Width = _view.Widest + (2 * ShadowRoom);
        Height = _view.CapsuleHeight + (2 * ShadowRoom);
        _view.HorizontalAlignment = _placement.Horizontal switch
        {
            CapsuleEdge.Start => HorizontalAlignment.Left,
            CapsuleEdge.End => HorizontalAlignment.Right,
            _ => HorizontalAlignment.Center,
        };
        // It grows, and zooms in, from its anchored corner.
        _view.RenderTransformOrigin = new Point(
            _placement.Horizontal switch { CapsuleEdge.Start => 0, CapsuleEdge.End => 1, _ => 0.5 },
            _placement.Vertical switch { CapsuleEdge.Start => 0, CapsuleEdge.End => 1, _ => 0.5 });

        if (_shown)
        {
            Place();
        }
        else
        {
            RestHidden();
        }
    }

    public void SetState(OverlayState state, string caption)
    {
        StopStage();
        _stageAfterNotice = false;
        _noticeTimer.Stop();
        if (state == OverlayState.Hidden)
        {
            _dictating = false;
            SlideOut();
            return;
        }

        if (state == OverlayState.Recording)
        {
            // A new dictation starts with an empty line and a fresh clock.
            _view.ClearDraft();
            _recording.Restart();
            _shownSecond = -1;
        }
        else
        {
            // While processing, the words heard so far and the time stay.
            _recording.Stop();
        }

        _dictating = true;
        _view.ShowText(caption, meter: true, processing: state == OverlayState.Transcribing);

        // Screen readers hear what happens after recording. "Listening" is not
        // announced: speech from the speakers would end up in the recording.
        if (state == OverlayState.Transcribing) _view.Announce(caption);
        SlideIn();
    }

    /// <summary>A short message such as "Copied last transcript" that goes away by itself.</summary>
    public void Notify(string message)
    {
        _stageAfterNotice |= _staging;
        StopStage();
        _dictating = false;
        _view.ClearDraft();
        _view.Elapsed = null;
        _view.ShowText(message, meter: false);
        _view.Announce(message);
        SlideIn();
        _noticeTimer.Stop();
        _noticeTimer.Interval = TimeSpan.FromSeconds(1.6);
        _noticeTimer.Start();
    }

    private static readonly string[] StageWords =
        "So the plan for tomorrow is simple: we ship the new version first thing in the morning".Split(' ');

    /// <summary>
    /// The capsule as a live sample while the Capsule settings page is open: it comes
    /// in as it does for a dictation, and example words build up in it, so every
    /// change made in settings (<see cref="Apply"/>) shows on it at once. A real
    /// dictation takes over the capsule if one starts.
    /// </summary>
    public void BeginStage()
    {
        if (_dictating) return;
        _staging = true;
        _noticeTimer.Stop();
        _stageStep = 0;
        _view.ClearDraft();
        _view.Elapsed = TimeSpan.Zero;
        _view.ShowText("Listening…", meter: true);
        _stageTimer.Start();
        SlideIn();
    }

    public void EndStage()
    {
        _stageAfterNotice = false;
        if (!_staging) return;
        StopStage();
        SlideOut();
    }

    private void StopStage()
    {
        _staging = false;
        _stageTimer.Stop();
    }

    /// <summary>The sample words arrive as in a real dictation, phrase by phrase; then it starts over.</summary>
    private void StageTick()
    {
        _stageStep++;
        if (_stageStep > StageWords.Length + 6)
        {
            _stageStep = 0;
            _view.ClearDraft();
        }

        _view.Elapsed = TimeSpan.FromSeconds(_stageStep);
        var shown = Math.Min(_stageStep, StageWords.Length);
        if (!_liveText)
        {
            _view.ClearDraft();
            return;
        }

        if (shown == 0) return;
        var settled = shown == StageWords.Length ? shown : shown / 5 * 5;
        _view.ShowDraft(string.Join(' ', StageWords[..settled]), string.Join(' ', StageWords[settled..shown]));
    }

    /// <summary>The live draft of what's being said; the capsule widens to fit it.</summary>
    public void ShowDraft(string settled, string tail) => _view.ShowDraft(settled, tail);

    public void UpdateLevel(double rootMeanSquare) => _view.SetLevel(rootMeanSquare);

    private void SlideIn()
    {
        if (!_shown)
        {
            _shown = true;
            Place();
            // Hidden until now, the window is clear: a still glass copies what is behind it at once.
            if (!_rendering && _glassOn && !_glassLive) CopyBehind();
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
            _view.ClearDraft();
            _view.Elapsed = null;
            RestHidden();
        });
    }

    /// <summary>
    /// Where the capsule waits while hidden. Against the top or bottom of the screen
    /// it slides in from beyond that edge; anywhere else it zooms in a little from
    /// its anchored corner, drifting from the side it keeps to.
    /// </summary>
    private (double Y, double Scale) HiddenPose()
    {
        if (_placement.AtTop) return (-(_view.CapsuleHeight + ShadowRoom + 4), 1);
        if (_placement.AtBottom) return (_view.CapsuleHeight + ShadowRoom + 4, 1);
        var drift = _placement.Vertical switch { CapsuleEdge.Start => -8, CapsuleEdge.End => 8, _ => 0 };
        return (drift * _view.Current.Scale, 0.9);
    }

    private void RestHidden()
    {
        var (y, scale) = HiddenPose();
        _slide.BeginAnimation(TranslateTransform.YProperty, null);
        _zoom.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        _zoom.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        _slide.Y = y;
        _zoom.ScaleX = _zoom.ScaleY = scale;
    }

    /// <summary>
    /// Moves or fades the capsule in or out. Each call starts from wherever the
    /// capsule is now, so a quick stop and start never makes it jump.
    /// </summary>
    private void Animate(bool show, Action? whenDone = null)
    {
        var moving = SystemParameters.ClientAreaAnimation;
        var time = !moving ? FadeOnlyTime : show ? EnterTime : ExitTime;
        var (hiddenY, hiddenScale) = HiddenPose();

        if (moving)
        {
            _slide.BeginAnimation(TranslateTransform.YProperty, Eased(show ? 0 : hiddenY, time));
            _zoom.BeginAnimation(ScaleTransform.ScaleXProperty, Eased(show ? 1 : hiddenScale, time));
            _zoom.BeginAnimation(ScaleTransform.ScaleYProperty, Eased(show ? 1 : hiddenScale, time));
        }
        else
        {
            // Windows animations are turned off: no movement, only a short fade.
            _slide.BeginAnimation(TranslateTransform.YProperty, null);
            _zoom.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            _zoom.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            _slide.Y = 0;
            _zoom.ScaleX = _zoom.ScaleY = 1;
        }

        var fade = Eased(show ? 1 : 0, time);
        if (whenDone is not null) fade.Completed += (_, _) => whenDone();
        _view.BeginAnimation(OpacityProperty, fade);
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
        EndClearing();
        _view.Quiet();
        _glass.Hide();
    }

    /// <summary>Runs once per display frame while the capsule is on screen.</summary>
    private void OnFrame(object? sender, EventArgs e)
    {
        var seconds = _clock.Elapsed.TotalSeconds;
        // On stage the meter breathes as if someone were talking.
        if (_staging) _view.SetPreviewLevel(0.4 + (0.3 * Math.Sin(seconds * 2.6)));
        _view.Render(seconds);
        if (_glassOn && !_glassLive) KeepCopyCurrent();
        if (_glassOn) FollowWithGlass();
        if (!_dictating) return;
        // The clock's text changes once a second, not every frame.
        var second = (long)_recording.Elapsed.TotalSeconds;
        if (second == _shownSecond) return;
        _shownSecond = second;
        _view.Elapsed = TimeSpan.FromSeconds(second);
    }

    /// <summary>The live glass under the capsule, wherever it is mid-animation, as it fades.</summary>
    private void FollowWithGlass()
    {
        var body = _view.Body;
        if (body.RenderSize.Width <= 0) return;
        var capsule = body.TransformToAncestor((Visual)Content).TransformBounds(new Rect(body.RenderSize));
        _glass.Opacity = _view.Opacity * Math.Clamp(_view.Current.Opacity, 0.5, 1);
        _glass.Follow(new WindowInteropHelper(this).Handle, capsule, _view.Rounding * _zoom.ScaleX, _glassLive);
        _view.GlassUnder(_glass.Light);
    }

    /// <summary>
    /// A still glass shows the screen as it was where the window is now. When Liquid
    /// Glass was just picked, or the window moved or resized and settled, the capsule
    /// clears for a few frames, long enough to leave the screen, and the glass copies
    /// the screen again.
    /// </summary>
    private void KeepCopyCurrent()
    {
        if (_clearing > 0)
        {
            if (--_clearing > 0) return;
            CopyBehind();
            EndClearing();
            return;
        }

        if (!NativeMethods.GetWindowRect(new WindowInteropHelper(this).Handle, out var area)) return;
        if (_copiedAt is { } at && at.Equals(area)) return;

        // While it still moves or resizes (a slider being dragged), it keeps the copy it has.
        _steady = area.Equals(_seen) ? _steady + 1 : 0;
        _seen = area;
        if (_copiedAt is not null && _steady < 8) return;
        ((UIElement)Content).Opacity = 0;
        _clearing = 3;
    }

    private void CopyBehind()
    {
        // Remembered even when the copy fails (off every screen), so it isn't tried every frame.
        if (!NativeMethods.GetWindowRect(new WindowInteropHelper(this).Handle, out var area)) return;
        _copiedAt = area;
        _glass.Copy(area);
    }

    private void EndClearing()
    {
        _clearing = 0;
        ((UIElement)Content).Opacity = 1;
    }

    /// <summary>
    /// Puts the window on the screen in use (where the user is working, or the main
    /// one) so the capsule lands on its placement. Placed in physical pixels with
    /// that monitor's DPI, so it is right on mixed-DPI setups.
    /// </summary>
    private void Place()
    {
        var handle = new WindowInteropHelper(this).EnsureHandle();

        // Shown once, then kept up: while idle it is empty, transparent and click-through.
        if (!IsVisible) Show();

        var monitor = _primaryScreen
            ? NativeMethods.MonitorFromPoint(new NativeMethods.POINT(), NativeMethods.MONITOR_DEFAULTTOPRIMARY)
            : ActiveMonitor();
        var info = new NativeMethods.MONITORINFO { cbSize = Marshal.SizeOf<NativeMethods.MONITORINFO>() };
        if (!NativeMethods.GetMonitorInfo(monitor, ref info)) return;

        var scale = NativeMethods.GetDpiForMonitor(monitor, 0, out var dpi, out _) == 0
            ? dpi / 96.0
            : VisualTreeHelper.GetDpi(this).DpiScaleX;
        var work = info.rcWork;
        var area = new Rect(work.Left / scale, work.Top / scale, (work.Right - work.Left) / scale, (work.Bottom - work.Top) / scale);
        var capsule = _placement.Place(new Size(_view.NarrowestWidth, _view.CapsuleHeight), _view.Widest, area);

        // The window's side that matches the anchored edge lines up with the capsule.
        var left = _placement.Horizontal switch
        {
            CapsuleEdge.Start => capsule.Left - ShadowRoom,
            CapsuleEdge.End => capsule.Right + ShadowRoom - Width,
            _ => capsule.Left + (capsule.Width / 2) - (Width / 2),
        };
        var top = capsule.Top - ShadowRoom;

        // Re-asserting topmost also lifts it above windows that went topmost since.
        NativeMethods.SetWindowPos(
            handle, NativeMethods.HWND_TOPMOST, (int)Math.Round(left * scale), (int)Math.Round(top * scale), 0, 0,
            NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
    }

    /// <summary>The monitor of the window the user is working in, else the one under the pointer.</summary>
    private static IntPtr ActiveMonitor()
    {
        var foreground = NativeMethods.GetForegroundWindow();
        if (foreground != IntPtr.Zero) return NativeMethods.MonitorFromWindow(foreground, NativeMethods.MONITOR_DEFAULTTONEAREST);
        NativeMethods.GetCursorPos(out var cursor);
        return NativeMethods.MonitorFromPoint(cursor, NativeMethods.MONITOR_DEFAULTTONEAREST);
    }

    private static T Frozen<T>(T freezable) where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }
}
