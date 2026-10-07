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
        Console.WriteLine($"Snapshots written to {folder}");
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
