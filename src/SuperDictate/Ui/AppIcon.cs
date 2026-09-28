using System.IO;
using System.Linq;
using System.Windows.Media.Imaging;
using SuperDictate.Speech;

namespace SuperDictate.Ui;

/// <summary>
/// SuperDictate.ico, embedded next to the exe's own icon resource: title bars,
/// the taskbar, the tray and the sidebar logo all show the same mark.
/// Redraw it with scripts\make-icon.py.
/// </summary>
internal static class AppIcon
{
    private static byte[]? _bytes;
    private static BitmapFrame? _window;
    private static BitmapSource? _large;

    private static byte[] Bytes => _bytes ??= SpeechRuntime.ReadResource("SuperDictate.ico");

    /// <summary>For Window.Icon: WPF picks the small and large frames from the icon.</summary>
    public static BitmapFrame Window => _window ??= BitmapFrame.Create(new MemoryStream(Bytes), BitmapCreateOptions.None, BitmapCacheOption.OnLoad);

    /// <summary>The 256 px frame, for drawing the logo at any size.</summary>
    public static BitmapSource Large => _large ??= new IconBitmapDecoder(new MemoryStream(Bytes), BitmapCreateOptions.None, BitmapCacheOption.OnLoad)
        .Frames.OrderByDescending(frame => frame.PixelWidth).First();

    /// <summary>The frame nearest the tray's icon size.</summary>
    public static System.Drawing.Icon Tray() =>
        new(new MemoryStream(Bytes), System.Windows.Forms.SystemInformation.SmallIconSize);
}
