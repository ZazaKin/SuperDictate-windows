using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SuperDictate.Storage;
using SuperDictate.Ui;

namespace SuperDictate;

/// <summary>
/// <c>SuperDictate.exe --snapshot &lt;folder&gt;</c>: draws the settings pages, every
/// capsule skin, and the frames of the capsule building a sentence, to PNG files,
/// then quits. The pictures in the README come from here, as the Mac app's do
/// from its own <c>--snapshot</c>. It uses default settings and saves none,
/// records nothing, and its windows sit off screen and never take focus.
/// </summary>
internal static class Snapshots
{
    private static readonly Brush Wallpaper = new LinearGradientBrush(
        new GradientStopCollection
        {
            new(Color.FromRgb(0x4F, 0x46, 0xE5), 0),
            new(Color.FromRgb(0x25, 0x63, 0xEB), 0.5),
            new(Color.FromRgb(0x0D, 0x94, 0x88), 1),
        }, 20);

    public static int Run(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: SuperDictate.exe --snapshot <folder>");
            return 2;
        }

        var folder = Path.GetFullPath(args[1]);
        Directory.CreateDirectory(folder);
        var application = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var result = 1;
        application.Startup += async (_, _) =>
        {
            try
            {
                await Draw(folder);
                result = 0;
            }
            catch (Exception error)
            {
                Console.Error.WriteLine($"Snapshots failed: {error}");
            }
            finally
            {
                application.Shutdown();
            }
        };
        application.Run();
        return result;
    }

    private static async Task Draw(string folder)
    {
        var settings = new Settings();
        using var controller = new DictationController(settings);
        foreach (var (page, name) in new[]
                 {
                     ("dictation", "dictation"), ("languages", "languages"), ("capsule", "capsule"),
                     ("models", "models"), ("ai_cleanup", "ai-cleanup"), ("settings", "general"), ("support", "support"),
                 })
        {
            var window = new SettingsWindow(controller, settings, page);
            await ShowOffScreen(window, 1040, 760);
            await Task.Delay(1200); // The cards finish rising in.
            Save(window, Path.Combine(folder, $"settings-{name}.png"));
            window.Close();
        }

        await DrawSkins(Path.Combine(folder, "capsule-skins.png"));
        await DrawSentence(Path.Combine(folder, "capsule-frames"));
        await DrawLiveGlass(Path.Combine(folder, "capsule-live-glass.png"));
        Console.WriteLine($"Snapshots written to {folder}");
    }

    /// <summary>
    /// Liquid Glass over a busy window: live (the experimental setting) above, painted
    /// below. The live one is fed a picture of the stage, as the app feeds it the screen.
    /// </summary>
    private static async Task DrawLiveGlass(string file)
    {
        var page = new Canvas { Width = 760, Height = 300, Background = Wallpaper, ClipToBounds = true };
        void Place(System.Windows.Shapes.Shape shape, double left, double top)
        {
            Canvas.SetLeft(shape, left);
            Canvas.SetTop(shape, top);
            page.Children.Add(shape);
        }

        Place(new System.Windows.Shapes.Rectangle { Width = 560, Height = 260, RadiusX = 12, RadiusY = 12, Fill = Brushes.White }, 100, 20);
        Place(new System.Windows.Shapes.Ellipse { Width = 120, Height = 120, Fill = new SolidColorBrush(Color.FromRgb(0xFF, 0x8A, 0x3D)) }, 150, 0);
        Place(new System.Windows.Shapes.Rectangle { Width = 150, Height = 70, RadiusX = 8, RadiusY = 8, Fill = new SolidColorBrush(Color.FromRgb(0xE1, 0x3B, 0x7A)) }, 470, 150);
        for (var line = 0; line < 9; line++)
        {
            var text = new TextBlock
            {
                Text = line % 3 == 0 ? "Quarterly review: ship the Mac version first" : "Notes, numbers and a few links to read before Thursday",
                FontSize = line % 3 == 0 ? 22 : 15,
                FontWeight = line % 3 == 0 ? FontWeights.SemiBold : FontWeights.Normal,
                Foreground = new SolidColorBrush(Color.FromRgb(0x1F, 0x23, 0x2B)),
            };
            Canvas.SetLeft(text, 130);
            Canvas.SetTop(text, 34 + (line * 27));
            page.Children.Add(text);
        }

        var glass = new LiveGlass();
        var live = Capsule(VerticalAlignment.Top);
        var painted = Capsule(VerticalAlignment.Bottom);
        var stage = new Grid { Width = page.Width, Height = page.Height, Children = { page, glass, live, painted } };
        var window = Host(stage);
        await ShowOffScreen(window);
        await Task.Delay(1500); // The words finish arriving.

        live.Visibility = painted.Visibility = Visibility.Hidden;
        stage.UpdateLayout();
        var picture = new RenderTargetBitmap((int)page.Width, (int)page.Height, 96, 96, PixelFormats.Pbgra32);
        picture.Render(page);
        live.Visibility = painted.Visibility = Visibility.Visible;
        stage.UpdateLayout();

        // The live one's own fill and rim stay; underneath, the glass shows the page.
        var body = live.Body;
        glass.Show(picture);
        glass.Follow(IntPtr.Zero, body.TransformToAncestor(stage).TransformBounds(new Rect(body.RenderSize)), live.Rounding);
        foreach (var view in new[] { live, painted })
        {
            for (var frame = 0; frame < 12; frame++) view.Render(0.35);
        }

        Save(stage, file);
        window.Close();

        static CapsuleView Capsule(VerticalAlignment edge)
        {
            var view = new CapsuleView { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = edge, Margin = new Thickness(0, 40, 0, 40) };
            view.Apply(new CapsuleStyle("liquid", "#5B8DEF", 1.25, 1, "bars", true));
            view.ShowText("Listening…", meter: true);
            view.ShowDraft("So the plan for tomorrow is simple:", "we ship the Mac version first");
            view.Elapsed = TimeSpan.FromSeconds(14);
            view.SetPreviewLevel(0.6);
            return view;
        }
    }

    /// <summary>Every skin with words in it, over a stand-in for whatever is underneath.</summary>
    private static async Task DrawSkins(string file)
    {
        var grid = new System.Windows.Controls.Primitives.UniformGrid { Columns = 2 };
        var views = CapsuleSkin.All.Select((skin, index) =>
        {
            var view = new CapsuleView { HorizontalAlignment = HorizontalAlignment.Center };
            view.Apply(new CapsuleStyle(skin.Id, "#5B8DEF", 1, 1, new[] { "bars", "wave", "pulse" }[index % 3], index < 2));
            view.ShowText("Listening…", meter: true);
            view.ShowDraft("Lunch at noon, then", "the design review");
            view.Elapsed = TimeSpan.FromSeconds(14);
            view.SetPreviewLevel(0.6);
            var name = new TextBlock
            {
                Text = skin.Name,
                Foreground = Brushes.White,
                Opacity = 0.85,
                FontSize = 12,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 8, 0, 0),
            };
            grid.Children.Add(new StackPanel { Margin = new Thickness(16, 12, 16, 12), Children = { view, name } });
            return view;
        }).ToList();

        var sheet = new Border { Background = Wallpaper, Padding = new Thickness(24), Child = grid };
        var window = Host(sheet);
        await ShowOffScreen(window);
        await Task.Delay(1500); // The words finish arriving.
        foreach (var view in views)
        {
            for (var frame = 0; frame < 12; frame++) view.Render(0.35); // A still of a voice mid-word.
        }

        Save(sheet, file);
        window.Close();
    }

    /// <summary>
    /// The live preview building a sentence, about 25 frames a second, for the
    /// README's animation. frames.txt lists how long each frame really lasted.
    /// </summary>
    private static async Task DrawSentence(string folder)
    {
        Directory.CreateDirectory(folder);
        var view = new CapsuleView { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        view.Apply(new CapsuleStyle("liquid", "#5B8DEF", 1.25, 1, "bars", false));
        view.ShowText("Listening…", meter: true);
        var stage = new Border { Background = Wallpaper, Width = 720, Height = 150, Child = view };
        var window = Host(stage);
        await ShowOffScreen(window);

        var words = "Move the design review to Thursday at three, and send the new mockups tonight".Split(' ');
        var shown = 0;
        var clock = Stopwatch.StartNew();
        var last = TimeSpan.Zero;
        var timings = new System.Collections.Generic.List<int>();
        for (var frame = 0; clock.Elapsed.TotalSeconds < 7.5; frame++)
        {
            var seconds = clock.Elapsed.TotalSeconds;
            // A word every 0.3 s after a short pause; the last two are still settling.
            var due = Math.Clamp((int)((seconds - 0.8) / 0.3) + 1, 0, words.Length);
            if (due != shown)
            {
                shown = due;
                var unsure = shown == words.Length ? 0 : Math.Min(2, shown);
                view.ShowDraft(string.Join(' ', words.Take(shown - unsure)), string.Join(' ', words.Skip(shown - unsure).Take(unsure)));
            }

            // A voice that rises and falls, quiet once the sentence is done.
            view.SetPreviewLevel(shown == words.Length ? 0.05 : 0.55 + (0.3 * Math.Sin(seconds * 7)));
            view.Render(seconds);
            await Task.Delay(30);
            Save(stage, Path.Combine(folder, $"frame-{frame:000}.png"), scale: 1.5);
            timings.Add((int)(clock.Elapsed - last).TotalMilliseconds);
            last = clock.Elapsed;
        }

        File.WriteAllLines(Path.Combine(folder, "frames.txt"), timings.Select(ms => ms.ToString()));
        window.Close();
    }

    private static Window Host(FrameworkElement content) => new()
    {
        WindowStyle = WindowStyle.None,
        AllowsTransparency = true,
        Background = Brushes.Transparent,
        SizeToContent = SizeToContent.WidthAndHeight,
        Content = content,
    };

    /// <summary>Shows a window far off screen, without taking focus, and waits for it to lay out.</summary>
    private static async Task ShowOffScreen(Window window, double width = double.NaN, double height = double.NaN)
    {
        window.ShowActivated = false;
        window.ShowInTaskbar = false;
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Left = -20000;
        window.Top = -20000;
        if (!double.IsNaN(width)) window.Width = width;
        if (!double.IsNaN(height)) window.Height = height;
        window.Show();
        await Task.Delay(100);
    }

    private static void Save(FrameworkElement visual, string file, double scale = 2)
    {
        visual.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(visual.ActualWidth * scale), (int)Math.Ceiling(visual.ActualHeight * scale),
            96 * scale, 96 * scale, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(file);
        encoder.Save(stream);
    }
}
