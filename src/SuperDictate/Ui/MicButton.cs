using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using SuperDictate.Interop;
using SuperDictate.Storage;

namespace SuperDictate.Ui;

/// <summary>
/// A floating microphone for people who'd rather click than use hotkeys: one
/// click starts dictation, the next stops it. Drag it anywhere; it remembers the
/// spot. Like the capsule it is never activated, so focus, the caret and the paste
/// target stay in the app the user is typing in.
/// </summary>
public sealed class MicButton : Window
{
    private const double FaceSize = 44;
    private const double Room = 10;       // Around the face, for the shadow and the recording ring.
    private const int DragThreshold = 4;  // Pixels of movement before a press becomes a drag.

    // Telegram's round blue voice button; the app's accent, deep enough for the white glyph (4.6:1).
    private static readonly Brush Idle = Frozen(new SolidColorBrush(Color.FromRgb(0x1E, 0x77, 0xCE)));
    private static readonly Brush IdleHover = Frozen(new SolidColorBrush(Color.FromRgb(0x1A, 0x6C, 0xBE)));
    private static readonly Brush Recording = Frozen(new SolidColorBrush(Color.FromRgb(0xC9, 0x3A, 0x42)));
    private static readonly Brush Hairline = Frozen(new SolidColorBrush(Color.FromArgb(56, 255, 255, 255)));

    private readonly DictationController _controller;
    private readonly Settings _settings;
    private readonly Border _face;
    private readonly TextBlock _glyph;
    private readonly Ellipse _ring;
    private readonly ScaleTransform _ringScale = new(1, 1);
    private readonly ScaleTransform _press = new(1, 1);

    private DictationState _state;
    private NativeMethods.POINT _pressedAt;
    private NativeMethods.RECT _windowAtPress;
    private bool _tracking;
    private bool _dragging;

    public MicButton(DictationController controller, Settings settings)
    {
        _controller = controller;
        _settings = settings;

        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        ResizeMode = ResizeMode.NoResize;
        Width = Height = FaceSize + (2 * Room);

        _glyph = new TextBlock
        {
            FontFamily = new FontFamily(Theme.IconFont),
            FontSize = 18,
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _ring = new Ellipse
        {
            Width = FaceSize,
            Height = FaceSize,
            Stroke = Recording,
            StrokeThickness = 3,
            Opacity = 0,
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = _ringScale,
            IsHitTestVisible = false,
        };
        var shadow = new Ellipse
        {
            Width = FaceSize,
            Height = FaceSize,
            Fill = Idle,
            Effect = new DropShadowEffect { BlurRadius = 12, ShadowDepth = 2, Direction = 270, Opacity = 0.4 },
            IsHitTestVisible = false,
        };
        _face = new Border
        {
            Width = FaceSize,
            Height = FaceSize,
            CornerRadius = new CornerRadius(FaceSize / 2),
            Background = Idle,
            BorderBrush = Hairline,
            BorderThickness = new Thickness(1),
            Child = _glyph,
            Cursor = Cursors.Hand,
        };
        var body = new Grid { RenderTransformOrigin = new Point(0.5, 0.5), RenderTransform = _press, Children = { shadow, _face } };
        Content = new Grid { Children = { _ring, body } };

        _face.MouseEnter += (_, _) => ShowState(_state);
        _face.MouseLeave += (_, _) => ShowState(_state);
        _face.MouseLeftButtonDown += (_, e) =>
        {
            BeginPress();
            e.Handled = true;
        };

        _controller.StateChanged += OnStateChanged;
        Closed += (_, _) =>
        {
            _controller.StateChanged -= OnStateChanged;
            EndTracking();
        };
        ShowState(_controller.State);
    }

    /// <summary>Shows the button where the user last left it, or bottom right.</summary>
    public void ShowAtSavedSpot()
    {
        if (!IsVisible) Show();
        PlaceOnScreen(_settings.MicButtonX, _settings.MicButtonY);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var handle = new WindowInteropHelper(this).Handle;
        var style = (long)NativeMethods.GetWindowLongPtr(handle, NativeMethods.GWL_EXSTYLE);
        style |= NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_TOOLWINDOW;
        NativeMethods.SetWindowLongPtr(handle, NativeMethods.GWL_EXSTYLE, new IntPtr(style));

        // Belt and braces: a click must never activate the button, or the paste
        // would go to it instead of the app the user is typing in.
        HwndSource.FromHwnd(handle)!.AddHook((IntPtr _, int message, IntPtr _, IntPtr _, ref bool handled) =>
        {
            if (message != NativeMethods.WM_MOUSEACTIVATE) return IntPtr.Zero;
            handled = true;
            return new IntPtr(NativeMethods.MA_NOACTIVATE);
        });
    }

    private void OnStateChanged(object? sender, DictationState state) => Dispatcher.InvokeAsync(() => ShowState(state));

    private void ShowState(DictationState state)
    {
        _state = state;
        var recording = state == DictationState.Recording;
        var usable = state is DictationState.Ready or DictationState.Recording or DictationState.Error;

        _glyph.Text = recording ? "" : "";
        _face.Background = recording ? Recording : _face.IsMouseOver && usable ? IdleHover : Idle;
        _face.Opacity = usable ? 1 : 0.55;
        AutomationProperties.SetName(this, recording ? "Stop dictation" : "Start dictation");
        AutomationProperties.SetName(_face, recording ? "Stop dictation" : "Start dictation");

        // While recording, a ring pulses out of the button: the one sign it is listening
        // when the user isn't looking at the top of the screen. Steady with animations off.
        _ring.BeginAnimation(OpacityProperty, null);
        _ringScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        _ringScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        if (!recording)
        {
            _ring.Opacity = 0;
            return;
        }

        if (!SystemParameters.ClientAreaAnimation)
        {
            _ring.Opacity = 0.6;
            _ringScale.ScaleX = _ringScale.ScaleY = 1.2;
            return;
        }

        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var pulse = TimeSpan.FromMilliseconds(1200);
        _ring.BeginAnimation(OpacityProperty, new DoubleAnimation(0.7, 0, pulse) { EasingFunction = ease, RepeatBehavior = RepeatBehavior.Forever });
        var grow = new DoubleAnimation(1, 1.4, pulse) { EasingFunction = ease, RepeatBehavior = RepeatBehavior.Forever };
        _ringScale.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
        _ringScale.BeginAnimation(ScaleTransform.ScaleYProperty, grow);
    }

    /// <summary>
    /// Follows the pointer each frame until the button is released. Polling the
    /// cursor works without mouse capture, which a window that never activates
    /// can't rely on.
    /// </summary>
    private void BeginPress()
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (!NativeMethods.GetCursorPos(out _pressedAt) || !NativeMethods.GetWindowRect(handle, out _windowAtPress)) return;
        _dragging = false;
        Squeeze(0.92, 90);
        if (_tracking) return;
        _tracking = true;
        CompositionTarget.Rendering += Track;
    }

    private void Track(object? sender, EventArgs e)
    {
        var button = System.Windows.Forms.SystemInformation.MouseButtonsSwapped ? NativeMethods.VK_RBUTTON : NativeMethods.VK_LBUTTON;
        var held = (NativeMethods.GetAsyncKeyState(button) & 0x8000) != 0;
        NativeMethods.GetCursorPos(out var cursor);
        var dx = cursor.X - _pressedAt.X;
        var dy = cursor.Y - _pressedAt.Y;

        if (!_dragging && (Math.Abs(dx) > DragThreshold || Math.Abs(dy) > DragThreshold))
        {
            _dragging = true;
            Squeeze(1, 240);
        }
        if (_dragging)
        {
            NativeMethods.SetWindowPos(new WindowInteropHelper(this).Handle, IntPtr.Zero,
                _windowAtPress.Left + dx, _windowAtPress.Top + dy, 0, 0,
                NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);
        }

        if (held) return;
        EndTracking();
        Squeeze(1, 240);

        if (_dragging)
        {
            RememberSpot();
        }
        else if (_state is DictationState.Ready or DictationState.Recording or DictationState.Error or DictationState.NeedsSetup)
        {
            // Before setup, Toggle doesn't record: it says so and opens the setup page.
            _controller.Toggle(_settings.PressEnterAfterPaste);
        }
    }

    /// <summary>
    /// Sinks the button the moment it's pressed and lets it back out on release,
    /// from whatever size it has on screen. With animations off it just changes size.
    /// </summary>
    private void Squeeze(double to, int milliseconds)
    {
        var time = SystemParameters.ClientAreaAnimation ? TimeSpan.FromMilliseconds(milliseconds) : TimeSpan.Zero;
        var scale = new DoubleAnimation(to, time) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
        _press.BeginAnimation(ScaleTransform.ScaleXProperty, scale);
        _press.BeginAnimation(ScaleTransform.ScaleYProperty, scale);
    }

    private void EndTracking()
    {
        if (!_tracking) return;
        _tracking = false;
        CompositionTarget.Rendering -= Track;
    }

    /// <summary>Keeps the button fully on a monitor, then saves where it is.</summary>
    private void RememberSpot()
    {
        NativeMethods.GetWindowRect(new WindowInteropHelper(this).Handle, out var rect);
        PlaceOnScreen(rect.Left, rect.Top);
        NativeMethods.GetWindowRect(new WindowInteropHelper(this).Handle, out rect);
        _settings.MicButtonX = rect.Left;
        _settings.MicButtonY = rect.Top;
        try
        {
            SettingsStore.Save(_settings);
        }
        catch (Exception error) when (error is System.IO.IOException or UnauthorizedAccessException)
        {
            AppLogger.Error("Could not save the microphone button position", error);
        }
    }

    /// <summary>At the given screen pixels, pulled back inside the nearest monitor's work area.</summary>
    private void PlaceOnScreen(int? x, int? y)
    {
        var handle = new WindowInteropHelper(this).EnsureHandle();
        var anchor = new NativeMethods.POINT { X = x ?? 0, Y = y ?? 0 };
        var monitor = x is null || y is null
            ? NativeMethods.MonitorFromPoint(anchor, NativeMethods.MONITOR_DEFAULTTOPRIMARY)
            : NativeMethods.MonitorFromPoint(anchor, NativeMethods.MONITOR_DEFAULTTONEAREST);
        var info = new NativeMethods.MONITORINFO { cbSize = Marshal.SizeOf<NativeMethods.MONITORINFO>() };
        if (!NativeMethods.GetMonitorInfo(monitor, ref info)) return;

        var scale = NativeMethods.GetDpiForMonitor(monitor, 0, out var dpi, out _) == 0 ? dpi / 96.0 : 1;
        var size = (int)Math.Round(Width * scale);
        var margin = (int)Math.Round(24 * scale);
        var work = info.rcWork;
        var (left, top) = ClampToArea(x ?? work.Right - size - margin, y ?? work.Bottom - size - margin, size, work.Left, work.Top, work.Right, work.Bottom);
        NativeMethods.SetWindowPos(handle, NativeMethods.HWND_TOPMOST, left, top, 0, 0,
            NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
    }

    /// <summary>A window of this size at (x, y), moved just enough to be entirely inside the area.</summary>
    internal static (int Left, int Top) ClampToArea(int x, int y, int size, int left, int top, int right, int bottom) =>
        (Math.Clamp(x, left, Math.Max(left, right - size)), Math.Clamp(y, top, Math.Max(top, bottom - size)));

    private static T Frozen<T>(T freezable) where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }
}
