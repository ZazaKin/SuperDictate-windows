using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using SuperDictate.Interop;
using SuperDictate.Storage;

namespace SuperDictate.Ui;

/// <summary>
/// Liquid Glass with the real screen behind it, as on an iPhone or a Mac with
/// macOS 26. The screen under the capsule window is copied about 20 times a second
/// and shows through the capsule softly blurred; the bezel bends what lies beyond
/// the edge inward, with a hint of color spread, and light catches the rim. The
/// glass turns light over bright windows and dark over dark ones, and the words on
/// it flip to match (<see cref="Light"/>), so they stay readable.
///
/// The capsule window keeps itself out of the copy (<see cref="KeepOutOfCopies"/>),
/// which also keeps it out of screenshots and screen sharing. Windows 10 2004 and
/// later can do that; elsewhere, or if the shader can't load, Liquid Glass is painted.
/// </summary>
internal sealed class LiveGlass : Grid, IDisposable
{
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(50);

    private readonly Image _behind = new() { Stretch = Stretch.Fill, Effect = new BlurEffect { Radius = 10 } };
    private readonly GlassLens _lens = new();
    private readonly Border _shadow = new()
    {
        Background = Brushes.Black,
        HorizontalAlignment = HorizontalAlignment.Left,
        VerticalAlignment = VerticalAlignment.Top,
        Effect = new DropShadowEffect { BlurRadius = 24, ShadowDepth = 6, Direction = 270, Opacity = 0.22 },
    };

    private WriteableBitmap? _image;
    private int[] _pixels = Array.Empty<int>();
    private IntPtr _dc;
    private IntPtr _dib;
    private IntPtr _bits;
    private IntPtr _previous;
    private int _width;
    private int _height;
    private DateTime _copied;

    public LiveGlass()
    {
        IsHitTestVisible = false;
        Visibility = Visibility.Hidden; // Hidden, not collapsed: it keeps its size for Follow.
        Children.Add(_shadow);
        // The clip keeps the blur from widening what the shader sees, so its
        // coordinates stay the layer's own.
        // Without a shader (it didn't compile) the layer draws no effect; the window never shows it then.
        Children.Add(new Grid { Effect = Available ? _lens : null, Children = { new Grid { ClipToBounds = true, Children = { _behind } } } });
    }

    /// <summary>Whether this Windows can draw the glass: its shader compiled.</summary>
    public static bool Available => GlassLens.Shader is not null;

    /// <summary>Light glass with dark words over a bright screen; dark glass with white words otherwise. Null before the first look.</summary>
    public bool? Light { get; private set; }

    /// <summary>Keeps the window out of screen copies, or lets it back in. False when Windows can't.</summary>
    public static bool KeepOutOfCopies(IntPtr window, bool on) =>
        NativeMethods.SetWindowDisplayAffinity(window, on ? NativeMethods.WDA_EXCLUDEFROMCAPTURE : NativeMethods.WDA_NONE);

    /// <summary>
    /// Shows the screen behind <paramref name="window"/> through the capsule, which is at
    /// <paramref name="capsule"/> in this layer's coordinates; called once per frame.
    /// </summary>
    public void Follow(IntPtr window, Rect capsule, double radius)
    {
        if (capsule.Width <= 0 || capsule.Height <= 0 || ActualWidth <= 0) return;
        if (DateTime.UtcNow - _copied >= Interval && NativeMethods.GetWindowRect(window, out var area) && Copy(area))
        {
            _copied = DateTime.UtcNow;
        }

        if (_behind.Source is null) return;
        Visibility = Visibility.Visible;

        // Flips at different points each way, so it never flickers on a mid-grey screen.
        var brightness = Brightness(capsule);
        Light = brightness > 0.6 || (Light == true && brightness > 0.45);

        var height = capsule.Height;
        _lens.Size = new Point4D(ActualWidth, ActualHeight, 0, 0);
        _lens.Shape = new Point4D(capsule.X, capsule.Y, capsule.Width, height);
        _lens.Bend = new Point4D(radius, height * 0.36, height * 0.24, 0.12);
        _lens.Tint = Light == true
            ? new Point4D(1, 1, 1, 0.2 + (0.45 * (1 - brightness)))
            : new Point4D(0, 0, 0, 0.12 + (0.55 * brightness));
        _lens.Light = new Point4D(-0.45, -0.89, Light == true ? 0.35 : 0.55, 0.08);

        // The shadow sits a point inside the edge, so only its soft fall-off shows.
        _shadow.Margin = new Thickness(capsule.X + 1, capsule.Y + 1, 0, 0);
        _shadow.Width = capsule.Width - 2;
        _shadow.Height = height - 2;
        _shadow.CornerRadius = new CornerRadius(Math.Max(0, radius - 1));
    }

    public void Hide() => Visibility = Visibility.Hidden;

    /// <summary>A picture in place of the screen, for the snapshots.</summary>
    public void Show(BitmapSource picture)
    {
        var converted = new FormatConvertedBitmap(picture, PixelFormats.Bgr32, null, 0);
        _width = converted.PixelWidth;
        _height = converted.PixelHeight;
        _pixels = new int[_width * _height];
        converted.CopyPixels(_pixels, _width * 4, 0);
        _behind.Source = picture;
    }

    /// <summary>Copies this part of the screen (physical pixels) into the picture shown.</summary>
    public bool Copy(NativeMethods.RECT area)
    {
        var width = area.Right - area.Left;
        var height = area.Bottom - area.Top;
        if (width <= 0 || height <= 0) return false;
        if (width != _width || height != _height || _dc == IntPtr.Zero)
        {
            Release();
            if (!Prepare(width, height)) return false;
        }

        // Only what is on a screen can be copied; beyond its edge, the nearest pixels carry on.
        var left = Math.Max(area.Left, NativeMethods.GetSystemMetrics(NativeMethods.SM_XVIRTUALSCREEN));
        var top = Math.Max(area.Top, NativeMethods.GetSystemMetrics(NativeMethods.SM_YVIRTUALSCREEN));
        var right = Math.Min(area.Right, NativeMethods.GetSystemMetrics(NativeMethods.SM_XVIRTUALSCREEN) + NativeMethods.GetSystemMetrics(NativeMethods.SM_CXVIRTUALSCREEN));
        var bottom = Math.Min(area.Bottom, NativeMethods.GetSystemMetrics(NativeMethods.SM_YVIRTUALSCREEN) + NativeMethods.GetSystemMetrics(NativeMethods.SM_CYVIRTUALSCREEN));
        if (right <= left || bottom <= top) return false;

        var screen = NativeMethods.GetDC(IntPtr.Zero);
        if (screen == IntPtr.Zero) return false;
        var copied = NativeMethods.BitBlt(_dc, left - area.Left, top - area.Top, right - left, bottom - top, screen, left, top,
            NativeMethods.SRCCOPY | NativeMethods.CAPTUREBLT);
        NativeMethods.ReleaseDC(IntPtr.Zero, screen);
        if (!copied) return false;

        NativeMethods.GdiFlush();
        Marshal.Copy(_bits, _pixels, 0, _pixels.Length);
        Extend(new Int32Rect(left - area.Left, top - area.Top, right - left, bottom - top));
        _image!.WritePixels(new Int32Rect(0, 0, width, height), _pixels, width * 4, 0);
        return true;
    }

    /// <summary>Fills the picture outside <paramref name="copied"/> with its nearest copied pixels.</summary>
    private void Extend(Int32Rect copied)
    {
        var right = copied.X + copied.Width;
        for (var y = copied.Y; y < copied.Y + copied.Height; y++)
        {
            var row = y * _width;
            if (copied.X > 0) Array.Fill(_pixels, _pixels[row + copied.X], row, copied.X);
            if (right < _width) Array.Fill(_pixels, _pixels[row + right - 1], row + right, _width - right);
        }

        for (var y = 0; y < copied.Y; y++) Array.Copy(_pixels, copied.Y * _width, _pixels, y * _width, _width);
        for (var y = copied.Y + copied.Height; y < _height; y++)
        {
            Array.Copy(_pixels, (copied.Y + copied.Height - 1) * _width, _pixels, y * _width, _width);
        }
    }

    /// <summary>How light the screen under the capsule is, 0 to 1, from every 4th pixel each way.</summary>
    private double Brightness(Rect capsule)
    {
        if (_pixels.Length == 0) return 0;
        var scale = _width / ActualWidth;
        var left = Math.Clamp((int)(capsule.Left * scale), 0, _width - 1);
        var right = Math.Clamp((int)(capsule.Right * scale), left + 1, _width);
        var top = Math.Clamp((int)(capsule.Top * scale), 0, _height - 1);
        var bottom = Math.Clamp((int)(capsule.Bottom * scale), top + 1, _height);
        long sum = 0;
        var count = 0;
        for (var y = top; y < bottom; y += 4)
        {
            for (var x = left; x < right; x += 4)
            {
                var pixel = _pixels[(y * _width) + x];
                sum += (((pixel >> 16) & 255) * 2126) + (((pixel >> 8) & 255) * 7152) + ((pixel & 255) * 722);
                count++;
            }
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
        _behind.Source = _image;
        return true;
    }

    private void Release()
    {
        if (_dc != IntPtr.Zero && _previous != IntPtr.Zero) NativeMethods.SelectObject(_dc, _previous);
        if (_dib != IntPtr.Zero) NativeMethods.DeleteObject(_dib);
        if (_dc != IntPtr.Zero) NativeMethods.DeleteDC(_dc);
        _dc = _dib = _bits = _previous = IntPtr.Zero;
        _width = _height = 0;
        _pixels = Array.Empty<int>();
        _image = null;
        _behind.Source = null;
    }

    public void Dispose() => Release();
}

/// <summary>
/// The glass itself, as a pixel shader over the blurred screen: the capsule's shape
/// with a smooth edge, the bend and color spread at the bezel, the tint, and the
/// light on the rim. Compiled when first needed by the shader compiler that comes
/// with Windows; null if that fails.
/// </summary>
internal sealed class GlassLens : ShaderEffect
{
    private const string Source = """
        sampler2D input : register(s0);
        float4 size : register(c0);   // xy: the layer, in points
        float4 shape : register(c1);  // the capsule: x, y, width, height
        float4 lens : register(c2);   // x: corner radius, y: bezel width, z: deepest bend, w: color spread
        float4 tint : register(c3);   // rgb, and how much of it
        float4 light : register(c4);  // xy: toward the light, z: rim strength, w: inner glow

        float4 main(float2 uv : TEXCOORD) : COLOR
        {
            float2 p = uv * size.xy;
            float2 extent = shape.zw * 0.5;
            float2 d = p - (shape.xy + extent);
            float2 q = abs(d) - extent + lens.x;
            float2 k = max(q, 0);
            float edge = length(k) + min(max(q.x, q.y), 0) - lens.x;   // negative inside
            float depth = -edge;
            float2 n = normalize(k + 0.0001) * (d >= 0 ? 1 : -1);

            // The bezel bends what lies beyond the edge in: most at the edge, none past the bezel.
            float t = saturate(1 - depth / lens.y);
            float2 bend = n * (lens.z * t * t) / size.xy;
            float3 color;
            color.r = tex2D(input, uv + bend * (1 + lens.w)).r;
            color.g = tex2D(input, uv + bend).g;
            color.b = tex2D(input, uv + bend * (1 - lens.w)).b;

            // More vivid, as glass is, then tinted for the words on top.
            float grey = dot(color, float3(0.2126, 0.7152, 0.0722));
            color = lerp(grey.xxx, color, 1.35);
            color = lerp(color, tint.rgb, tint.a);

            // The bezel's thickness: a touch darker on the side away from the light.
            // Light catches the rim where it faces the light, and faintly opposite.
            float facing = dot(n, light.xy);
            float lit = saturate(facing);
            float away = saturate(-facing);
            float bezel = t * t;
            color *= 1 - 0.18 * bezel * away;
            color += saturate(1.6 - depth) * (light.z * (lit + 0.45 * away) + 0.12) + light.w * bezel * (lit + 0.3);

            float alpha = saturate(0.5 - edge);
            return float4(color * alpha, alpha);
        }
        """;

    public static readonly PixelShader? Shader = Compile();

    public static readonly DependencyProperty InputProperty = RegisterPixelShaderSamplerProperty("Input", typeof(GlassLens), 0);
    public static readonly DependencyProperty SizeProperty = Constant(nameof(Size), 0);
    public static readonly DependencyProperty ShapeProperty = Constant(nameof(Shape), 1);
    public static readonly DependencyProperty BendProperty = Constant(nameof(Bend), 2);
    public static readonly DependencyProperty TintProperty = Constant(nameof(Tint), 3);
    public static readonly DependencyProperty LightProperty = Constant(nameof(Light), 4);

    public GlassLens()
    {
        PixelShader = Shader;
        UpdateShaderValue(InputProperty);
        foreach (var constant in new[] { SizeProperty, ShapeProperty, BendProperty, TintProperty, LightProperty }) UpdateShaderValue(constant);
    }

    public Point4D Size { get => (Point4D)GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    public Point4D Shape { get => (Point4D)GetValue(ShapeProperty); set => SetValue(ShapeProperty, value); }

    public Point4D Bend { get => (Point4D)GetValue(BendProperty); set => SetValue(BendProperty, value); }

    public Point4D Tint { get => (Point4D)GetValue(TintProperty); set => SetValue(TintProperty, value); }

    public Point4D Light { get => (Point4D)GetValue(LightProperty); set => SetValue(LightProperty, value); }

    private static DependencyProperty Constant(string name, int register) =>
        DependencyProperty.Register(name, typeof(Point4D), typeof(GlassLens), new UIPropertyMetadata(new Point4D(), PixelShaderConstantCallback(register)));

    private static PixelShader? Compile()
    {
        try
        {
            // ps_2_0 also draws in software, where the snapshots and some remote sessions render.
            var result = NativeMethods.D3DCompile(Source, (IntPtr)Source.Length, "glass", IntPtr.Zero, IntPtr.Zero,
                "main", "ps_2_0", NativeMethods.D3DCOMPILE_OPTIMIZATION_LEVEL3, 0, out var code, out _);
            if (result != 0 || code is null) return null;
            var bytes = new byte[(int)code.GetBufferSize()];
            Marshal.Copy(code.GetBufferPointer(), bytes, 0, bytes.Length);
            var shader = new PixelShader();
            shader.SetStreamSource(new MemoryStream(bytes));
            shader.Freeze();
            return shader;
        }
        catch (Exception error) when (error is DllNotFoundException or EntryPointNotFoundException or COMException)
        {
            AppLogger.Warn($"Live glass is unavailable: {error.Message}");
            return null;
        }
    }
}
