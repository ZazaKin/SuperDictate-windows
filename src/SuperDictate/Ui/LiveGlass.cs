using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using SuperDictate.Interop;

namespace SuperDictate.Ui;

/// <summary>
/// Experimental: Liquid Glass with the real screen behind it. The screen under the
/// capsule window is copied about 20 times a second and shows through the capsule
/// softly blurred; its rim shows what lies just beyond the edge, squeezed inward as
/// light bends through thick glass. The brighter the screen behind, the darker the
/// glass, so the words stay readable. The skin's tint, rim and sheen go on top.
///
/// The capsule window keeps itself out of the copy (<see cref="KeepOutOfCopies"/>),
/// which also keeps it out of screenshots and screen sharing. Windows 10 2004 and
/// later can do that; older Windows gets the painted Liquid Glass.
/// </summary>
internal sealed class LiveGlass : Grid, IDisposable
{
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(50);

    private readonly Image _behind = new() { Stretch = Stretch.Fill };
    private readonly Image _bent = new() { Stretch = Stretch.Fill };
    private readonly ScaleTransform _bend = new();
    private readonly RectangleGeometry _shape = new();
    private readonly RectangleGeometry _inside = new();
    private readonly Border _shade = new() { Background = Brushes.Black };

    private WriteableBitmap? _image;
    private int[] _pixels = Array.Empty<int>();
    private IntPtr _dc;
    private IntPtr _dib;
    private IntPtr _bits;
    private IntPtr _previous;
    private int _width;
    private int _height;
    private double _brightness;
    private DateTime _copied;

    public LiveGlass()
    {
        IsHitTestVisible = false;
        Visibility = Visibility.Collapsed;
        _bent.RenderTransform = _bend;
        var rim = new Border { Child = _bent, Clip = new CombinedGeometry(GeometryCombineMode.Exclude, _shape, _inside) };

        // One blur over both, so the rim melts into the middle; the clip keeps it inside the capsule.
        var soft = new Grid { Effect = new BlurEffect { Radius = 8 }, Children = { _behind, rim } };
        Children.Add(new Grid { Clip = _shape, Children = { soft, _shade } });
    }

    /// <summary>Keeps the window out of screen copies, or lets it back in. False when Windows can't.</summary>
    public static bool KeepOutOfCopies(IntPtr window, bool on) =>
        NativeMethods.SetWindowDisplayAffinity(window, on ? NativeMethods.WDA_EXCLUDEFROMCAPTURE : NativeMethods.WDA_NONE);

    /// <summary>
    /// Shows the screen behind <paramref name="window"/> through the capsule, which is at
    /// <paramref name="capsule"/> in this layer's coordinates; called once per frame.
    /// </summary>
    public void Follow(IntPtr window, Rect capsule, double radius)
    {
        if (capsule.Width <= 0 || capsule.Height <= 0) return;
        if (DateTime.UtcNow - _copied >= Interval && NativeMethods.GetWindowRect(window, out var area) && Copy(area))
        {
            _copied = DateTime.UtcNow;
        }

        if (_behind.Source is null) return;
        Visibility = Visibility.Visible;

        _shape.Rect = capsule;
        _shape.RadiusX = _shape.RadiusY = radius;
        var rim = capsule.Height * 0.16;
        var inside = capsule;
        inside.Inflate(-rim, -rim);
        _inside.Rect = inside;
        _inside.RadiusX = _inside.RadiusY = Math.Max(0, radius - rim);

        // The rim shows what lies up to `reach` beyond the edge, squeezed into it.
        var reach = rim * 1.4;
        _bend.CenterX = capsule.X + (capsule.Width / 2);
        _bend.CenterY = capsule.Y + (capsule.Height / 2);
        _bend.ScaleX = capsule.Width / (capsule.Width + (2 * reach));
        _bend.ScaleY = capsule.Height / (capsule.Height + (2 * reach));
        _shade.Opacity = 0.1 + (0.5 * _brightness);
    }

    public void Hide() => Visibility = Visibility.Collapsed;

    /// <summary>A picture in place of the screen, for the snapshots.</summary>
    public void Show(BitmapSource picture)
    {
        var converted = new FormatConvertedBitmap(picture, PixelFormats.Bgr32, null, 0);
        var pixels = new int[converted.PixelWidth * converted.PixelHeight];
        converted.CopyPixels(pixels, converted.PixelWidth * 4, 0);
        _brightness = Brightness(pixels);
        _behind.Source = _bent.Source = picture;
    }

    /// <summary>Copies this part of the screen (physical pixels) into the picture shown.</summary>
    public bool Copy(NativeMethods.RECT area)
    {
        var width = area.Right - area.Left;
        var height = area.Bottom - area.Top;
        if (width <= 0 || height <= 0) return false;
        if (width != _width || height != _height)
        {
            Release();
            if (!Prepare(width, height)) return false;
        }

        var screen = NativeMethods.GetDC(IntPtr.Zero);
        if (screen == IntPtr.Zero) return false;
        var copied = NativeMethods.BitBlt(_dc, 0, 0, width, height, screen, area.Left, area.Top,
            NativeMethods.SRCCOPY | NativeMethods.CAPTUREBLT);
        NativeMethods.ReleaseDC(IntPtr.Zero, screen);
        if (!copied) return false;

        NativeMethods.GdiFlush();
        Marshal.Copy(_bits, _pixels, 0, _pixels.Length);
        _image!.WritePixels(new Int32Rect(0, 0, width, height), _pixels, width * 4, 0);
        _brightness = Brightness(_pixels);
        return true;
    }

    /// <summary>How light the picture is, 0 to 1, from every 16th pixel.</summary>
    private static double Brightness(int[] pixels)
    {
        long sum = 0;
        var count = 0;
        for (var index = 0; index < pixels.Length; index += 16)
        {
            var pixel = pixels[index];
            sum += (((pixel >> 16) & 255) * 2126) + (((pixel >> 8) & 255) * 7152) + ((pixel & 255) * 722);
            count++;
        }

        return count == 0 ? 0 : sum / (count * 255.0 * 10000);
    }

    private bool Prepare(int width, int height)
    {
        var header = new NativeMethods.BITMAPINFOHEADER
        {
            biSize = Marshal.SizeOf<NativeMethods.BITMAPINFOHEADER>(),
            biWidth = width,
            biHeight = -height, // Top-down, like the picture.
            biPlanes = 1,
            biBitCount = 32,
        };
        _dc = NativeMethods.CreateCompatibleDC(IntPtr.Zero);
        _dib = NativeMethods.CreateDIBSection(_dc, ref header, 0, out _bits, IntPtr.Zero, 0);
        if (_dc == IntPtr.Zero || _dib == IntPtr.Zero)
        {
            Release();
            return false;
        }

        _previous = NativeMethods.SelectObject(_dc, _dib);
        _width = width;
        _height = height;
        _pixels = new int[width * height];
        _image = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgr32, null);
        _behind.Source = _bent.Source = _image;
        return true;
    }

    private void Release()
    {
        if (_dc != IntPtr.Zero && _previous != IntPtr.Zero) NativeMethods.SelectObject(_dc, _previous);
        if (_dib != IntPtr.Zero) NativeMethods.DeleteObject(_dib);
        if (_dc != IntPtr.Zero) NativeMethods.DeleteDC(_dc);
        _dc = _dib = _bits = _previous = IntPtr.Zero;
        _width = _height = 0;
        _image = null;
        _behind.Source = _bent.Source = null;
    }

    public void Dispose() => Release();
}
