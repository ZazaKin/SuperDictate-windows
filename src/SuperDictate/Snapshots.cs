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

        // Leaving a page with a change on it asks first.
        var asking = new SettingsWindow(controller, settings, "capsule");
        await ShowOffScreen(asking, 1040, 760);
        await Task.Delay(1200);
        asking.Draft.Pending.CapsuleScale = 1.3;
        asking.Visit("languages");
        await Task.Delay(300);
        Save(asking, Path.Combine(folder, "settings-unsaved.png"));
        asking.Draft.Discard();
        asking.Close();

        await DrawSkins(Path.Combine(folder, "capsule-skins.png"));
        await DrawSentence(Path.Combine(folder, "capsule-frames"));
        await DrawLiveGlass(Path.Combine(folder, "capsule-live-glass.png"));
        // The video pushes in on the glass, so it gets a sharper copy; before the tour, which stops the clock.
        Directory.CreateDirectory(Path.Combine(folder, "tour"));
        await DrawLiveGlass(Path.Combine(folder, "tour", "glass.png"), scale: 3);
        await DrawTour(Path.Combine(folder, "tour"));
        Console.WriteLine($"Snapshots written to {folder}");
    }

    /// <summary>
    /// The capsule's part in the README video (videos/showcase): see-through frames of
    /// it dictating, changing skins, speaking other languages and waiting, each in its
    /// own folder with frames.txt as <see cref="DrawSentence"/> writes it. Liquid Glass
    /// sees the video's dark field behind it, so it stays dark with white words.
    /// </summary>
    private static async Task DrawTour(string folder)
    {
        var view = new CapsuleView { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 20, 0, 0) };
        var glass = new LiveGlass();
        var stage = new Grid { Width = 640, Height = 100, Children = { glass, view } };
        var window = Host(stage);
        await ShowOffScreen(window);
        var field = new Border { Width = stage.Width, Height = stage.Height, Background = new SolidColorBrush(Color.FromRgb(0x12, 0x1E, 0x2C)) };
        field.Measure(new Size(stage.Width, stage.Height));
        field.Arrange(new Rect(0, 0, stage.Width, stage.Height));
        var picture = new RenderTargetBitmap((int)stage.Width, (int)stage.Height, 96, 96, PixelFormats.Pbgra32);
        picture.Render(field);
        glass.Show(picture);
        // Exactly 30 frames a second, however long the glass takes to draw; real time if WPF won't lend its clock.
        var clock = FrameClock.Take();

        async Task Wait(double seconds)
        {
            if (clock is null) await Task.Delay(TimeSpan.FromSeconds(seconds));
            // Frame by frame: an animation starts on the first tick after it's begun, so one long step would only start it.
            else for (var frame = 0; frame < seconds * 30; frame++) clock.Advance(TimeSpan.FromSeconds(1 / 30.0));
        }

        void Look(string skin) => view.Apply(new CapsuleStyle(skin, "#5B8DEF", 1.25, 1, "bars", false));

        // Live glass under Liquid Glass, as the app draws it; painted skins draw themselves.
        void Follow()
        {
            stage.UpdateLayout();
            if (!CapsuleSkin.Find(view.Current.Skin).Glass)
            {
                glass.Hide();
                view.GlassUnder(null);
                return;
            }

            glass.Follow(IntPtr.Zero, view.Body.TransformToAncestor(stage).TransformBounds(new Rect(view.Body.RenderSize)), view.Rounding);
            view.GlassUnder(glass.Light);
        }

        async Task Record(string name, double length, Action<double> at)
        {
            var frames = Path.Combine(folder, name);
            Directory.CreateDirectory(frames);
            var times = new System.Collections.Generic.List<double>();
            var watch = Stopwatch.StartNew();
            for (var frame = 0; ; frame++)
            {
                var seconds = clock is null ? watch.Elapsed.TotalSeconds : frame / 30.0;
                if (seconds >= length) break;
                at(seconds);
                view.Render(seconds);
                Follow();
                await Task.Delay(clock is null ? 15 : 1);
                Save(stage, Path.Combine(frames, $"frame-{frame:000}.png"));
                times.Add(seconds);
                clock?.Advance(TimeSpan.FromSeconds(1 / 30.0));
            }

            // How long each frame shows: until the next one, the last until the end.
            File.WriteAllLines(Path.Combine(frames, "frames.txt"),
                times.Select((start, index) => ((int)Math.Round(((index + 1 < times.Count ? times[index + 1] : length) - start) * 1000)).ToString()));
        }

        static double Voice(double seconds) => 0.55 + (0.3 * Math.Sin(seconds * 7));

        // Dictating: "Listening…", then the sentence word by word, then processing.
        Look("liquid");
        view.ShowText("Listening…", meter: true);
        var words = "Move the design review to Thursday at three, and send the new mockups tonight".Split(' ');
        var shown = 0;
        var processing = false;
        await Record("dictate", 5.2, seconds =>
        {
            var due = Math.Clamp((int)((seconds - 0.5) / 0.25) + 1, 0, words.Length);
            if (due != shown)
            {
                shown = due;
                var unsure = shown == words.Length ? 0 : Math.Min(2, shown);
                view.ShowDraft(string.Join(' ', words.Take(shown - unsure)), string.Join(' ', words.Skip(shown - unsure).Take(unsure)));
            }

            if (seconds >= 4.2 && !processing)
            {
                processing = true;
                view.ShowText("Processing…", meter: true, processing: true);
            }

            view.SetPreviewLevel(seconds is > 0.3 and < 4.0 ? Voice(seconds) : 0.06);
        });

        // Every skin in turn, a quarter second or so each, Liquid Glass first.
        var skins = CapsuleSkin.All.OrderBy(skin => skin.Id != "liquid").ToList();
        File.WriteAllLines(Path.Combine(folder, "skins.txt"), skins.Select(skin => skin.Name));
        view.ClearDraft();
        view.ShowText("Listening…", meter: true);
        view.ShowDraft("Lunch at noon, then", "the design review");
        await Wait(1.5); // The words finish arriving.
        var current = -1;
        await Record("skins", 3.5, seconds =>
        {
            var index = Math.Min(skins.Count - 1, (int)(seconds / (3.5 / skins.Count)));
            if (index != current)
            {
                current = index;
                Look(skins[index].Id);
            }

            view.SetPreviewLevel(Voice(seconds));
        });

        // The same plan said in German, French and Russian.
        Look("liquid");
        var sentences = new[] { "Wir verschieben das Review auf Donnerstag", "On déplace la revue à jeudi", "Переносим обзор на четверг" };
        var spoken = -1;
        var said = 0;
        view.ClearDraft();
        await Record("languages", 3.5, seconds =>
        {
            var sentence = Math.Min(sentences.Length - 1, (int)(seconds / (3.5 / sentences.Length)));
            if (sentence != spoken)
            {
                spoken = sentence;
                said = 0;
                view.ClearDraft();
            }

            var parts = sentences[sentence].Split(' ');
            var due = Math.Clamp((int)((seconds - (sentence * 3.5 / sentences.Length) - 0.05) / 0.11) + 1, 0, parts.Length);
            if (due != said)
            {
                said = due;
                var unsure = said == parts.Length ? 0 : Math.Min(1, said);
                view.ShowDraft(string.Join(' ', parts.Take(said - unsure)), string.Join(' ', parts.Skip(said - unsure).Take(unsure)));
            }

            view.SetPreviewLevel(Voice(seconds));
        });

        // Waiting, the voice fading to nothing, then still.
        view.ClearDraft();
        view.ShowText("Listening…", meter: true);
        await Record("waiting", 3.5, seconds =>
        {
            if (seconds < 1.5) view.SetPreviewLevel(0.12 * (1 - (seconds / 1.5)));
            else view.Quiet();
        });

        // Each skin on the video's wheel, alive for six seconds: its art moving, a voice rising and falling.
        view.ShowDraft("Lunch at noon, then", "the design review");
        await Wait(1.5);
        foreach (var (id, index) in new[] { "liquid", "bloom", "eclipse", "chrome", "hive", "halftone", "mosaic", "mesa", "aurora", "neon" }.Select((id, index) => (id, index)))
        {
            Look(id);
            await Record($"skin-{id}", 6.0, seconds => view.SetPreviewLevel(Voice(seconds + index)));
        }

        window.Close();
        await DrawGlassMotion(Path.Combine(folder, "glass-motion"), clock);
        await DrawGallery(Path.Combine(folder, "gallery"), clock);
    }

    /// <summary>
    /// Liquid Glass alive, for the video's glass shot: the page behind the two capsules
    /// drifts, so the bend at their edges moves with it, and the white window slides
    /// away from under the top one, which turns from light glass to dark as the screen
    /// behind it does. 4.8 seconds at 30 frames a second, 2.5 times the points.
    /// </summary>
    private static async Task DrawGlassMotion(string folder, FrameClock? clock)
    {
        Directory.CreateDirectory(folder);
        var page = new Canvas { Width = 760, Height = 340, Background = Wallpaper, ClipToBounds = true };
        var window = new Canvas { Width = 600, Height = 150 };
        window.Children.Add(new System.Windows.Shapes.Rectangle { Width = 600, Height = 150, RadiusX = 12, RadiusY = 12, Fill = Brushes.White });
        for (var line = 0; line < 5; line++)
        {
            var text = new TextBlock
            {
                Text = line % 3 == 0 ? "Quarterly review: ship the Mac version first" : "Notes, numbers and a few links to read before Thursday",
                FontSize = line % 3 == 0 ? 22 : 15,
                FontWeight = line % 3 == 0 ? FontWeights.SemiBold : FontWeights.Normal,
                Foreground = new SolidColorBrush(Color.FromRgb(0x1F, 0x23, 0x2B)),
            };
            Canvas.SetLeft(text, 30);
            Canvas.SetTop(text, 12 + (line * 26));
            window.Children.Add(text);
        }

        var orange = new System.Windows.Shapes.Ellipse { Width = 110, Height = 110, Fill = new SolidColorBrush(Color.FromRgb(0xFF, 0x8A, 0x3D)) };
        var yellow = new System.Windows.Shapes.Ellipse { Width = 130, Height = 130, Fill = new SolidColorBrush(Color.FromRgb(0xFF, 0xC8, 0x3D)) };
        var pink = new System.Windows.Shapes.Rectangle { Width = 160, Height = 60, RadiusX = 8, RadiusY = 8, Fill = new SolidColorBrush(Color.FromRgb(0xE1, 0x3B, 0x7A)) };
        page.Children.Add(window);
        page.Children.Add(orange);
        page.Children.Add(yellow);
        page.Children.Add(pink);

        CapsuleView Capsule(double top)
        {
            var view = new CapsuleView { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, top, 0, 0) };
            view.Apply(new CapsuleStyle("liquid", "#5B8DEF", 1.25, 1, "bars", true));
            view.ShowText("Listening…", meter: true);
            view.ShowDraft("So the plan for tomorrow is simple:", "we ship the Mac version first");
            view.Elapsed = TimeSpan.FromSeconds(14);
            return view;
        }

        var light = Capsule(46);
        var dark = Capsule(240);
        var lightGlass = new LiveGlass();
        var darkGlass = new LiveGlass();
        var stage = new Grid { Width = page.Width, Height = page.Height, Children = { page, lightGlass, darkGlass, light, dark } };
        var host = Host(stage);
        await ShowOffScreen(host);
        // The words finish arriving.
        if (clock is null) await Task.Delay(1500);
        else for (var frame = 0; frame < 45; frame++) clock.Advance(TimeSpan.FromSeconds(1 / 30.0));

        static double Ease(double from, double to, double seconds)
        {
            var x = Math.Clamp((seconds - from) / (to - from), 0, 1);
            return x * x * (3 - (2 * x));
        }

        for (var frame = 0; frame < 144; frame++)
        {
            var seconds = frame / 30.0;
            // The page drifts; at two seconds the window slides up and away from under the top capsule.
            Canvas.SetLeft(window, 80 - (10 * seconds));
            Canvas.SetTop(window, 16 - (200 * Ease(2.0, 2.9, seconds)));
            Canvas.SetLeft(orange, 120 + (30 * Math.Sin(seconds * 0.9)));
            Canvas.SetTop(orange, 15 * Math.Cos(seconds * 0.8));
            Canvas.SetLeft(yellow, 500 - (40 * seconds));
            Canvas.SetTop(yellow, 220 - (10 * seconds));
            Canvas.SetLeft(pink, 150 + (25 * seconds));
            Canvas.SetTop(pink, 250);
            stage.UpdateLayout();

            // The glass sees the page, as the app sees the screen.
            var picture = new RenderTargetBitmap((int)page.Width, (int)page.Height, 96, 96, PixelFormats.Pbgra32);
            picture.Render(page);
            foreach (var (view, glass, voice) in new[] { (light, lightGlass, 0.0), (dark, darkGlass, 2.0) })
            {
                glass.Show(picture);
                glass.Follow(IntPtr.Zero, view.Body.TransformToAncestor(stage).TransformBounds(new Rect(view.Body.RenderSize)), view.Rounding);
                view.GlassUnder(glass.Light);
                view.SetPreviewLevel(0.5 + (0.3 * Math.Sin((seconds * 6) + voice)));
                view.Render(seconds);
            }

            await Task.Delay(1);
            Save(stage, Path.Combine(folder, $"frame-{frame:000}.png"), scale: 2.5);
            clock?.Advance(TimeSpan.FromSeconds(1 / 30.0));
        }

        host.Close();
    }

    /// <summary>Every skin at once, moving, for three seconds at 30 frames a second: how each one looks alive.</summary>
    private static async Task DrawGallery(string folder, FrameClock? clock)
    {
        Directory.CreateDirectory(folder);
        var grid = new System.Windows.Controls.Primitives.UniformGrid { Columns = 3 };
        var views = CapsuleSkin.All.Select((skin, index) =>
        {
            var view = new CapsuleView { HorizontalAlignment = HorizontalAlignment.Center };
            view.Apply(new CapsuleStyle(skin.Id, "#5B8DEF", 1, 1, CapsuleView.Meters[index % CapsuleView.Meters.Length], false));
            view.ShowText("Listening…", meter: true);
            view.ShowDraft("Lunch at noon, then", "the design review");
            var name = new TextBlock
            {
                Text = skin.Name,
                Foreground = Brushes.White,
                Opacity = 0.85,
                FontSize = 12,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 8, 0, 0),
            };
            grid.Children.Add(new StackPanel { Margin = new Thickness(14, 10, 14, 10), Children = { view, name } });
            return view;
        }).ToList();

        var sheet = new Border { Background = Wallpaper, Padding = new Thickness(20), Child = grid };
        var window = Host(sheet);
        await ShowOffScreen(window);
        // The words finish arriving.
        if (clock is null) await Task.Delay(1500);
        else for (var frame = 0; frame < 45; frame++) clock.Advance(TimeSpan.FromSeconds(1 / 30.0));

        for (var frame = 0; frame < 90; frame++)
        {
            var seconds = frame / 30.0;
            for (var index = 0; index < views.Count; index++)
            {
                // Each one a voice of its own, rising and falling.
                views[index].SetPreviewLevel(0.5 + (0.4 * Math.Sin((seconds * 5) + index)));
                views[index].Render(seconds + (index * 0.4));
            }

            await Task.Delay(1);
            Save(sheet, Path.Combine(folder, $"frame-{frame:000}.png"), scale: 1);
            clock?.Advance(TimeSpan.FromSeconds(1 / 30.0));
        }

        window.Close();
    }

    /// <summary>
    /// Live Liquid Glass over a bright window, where it turns light with dark words,
    /// and over a dark wallpaper, where it stays dark. Each glass is fed a picture of
    /// the stage, as the app feeds it the screen.
    /// </summary>
    private static async Task DrawLiveGlass(string file, double scale = 2)
    {
        var page = new Canvas { Width = 760, Height = 340, Background = Wallpaper, ClipToBounds = true };
        void Place(UIElement shape, double left, double top)
        {
            Canvas.SetLeft(shape, left);
            Canvas.SetTop(shape, top);
            page.Children.Add(shape);
        }

        Place(new System.Windows.Shapes.Rectangle { Width = 600, Height = 150, RadiusX = 12, RadiusY = 12, Fill = Brushes.White }, 80, 16);
        Place(new System.Windows.Shapes.Ellipse { Width = 110, Height = 110, Fill = new SolidColorBrush(Color.FromRgb(0xFF, 0x8A, 0x3D)) }, 120, 0);
        Place(new System.Windows.Shapes.Ellipse { Width = 130, Height = 130, Fill = new SolidColorBrush(Color.FromRgb(0xFF, 0xC8, 0x3D)) }, 500, 220);
        Place(new System.Windows.Shapes.Rectangle { Width = 160, Height = 60, RadiusX = 8, RadiusY = 8, Fill = new SolidColorBrush(Color.FromRgb(0xE1, 0x3B, 0x7A)) }, 150, 250);
        for (var line = 0; line < 5; line++)
        {
            var text = new TextBlock
            {
                Text = line % 3 == 0 ? "Quarterly review: ship the Mac version first" : "Notes, numbers and a few links to read before Thursday",
                FontSize = line % 3 == 0 ? 22 : 15,
                FontWeight = line % 3 == 0 ? FontWeights.SemiBold : FontWeights.Normal,
                Foreground = new SolidColorBrush(Color.FromRgb(0x1F, 0x23, 0x2B)),
            };
            Place(text, 110, 28 + (line * 26));
        }

        var light = Capsule(new Thickness(0, 46, 0, 0));
        var dark = Capsule(new Thickness(0, 240, 0, 0));
        var lightGlass = new LiveGlass();
        var darkGlass = new LiveGlass();
        var stage = new Grid { Width = page.Width, Height = page.Height, Children = { page, lightGlass, darkGlass, light, dark } };
        var window = Host(stage);
        await ShowOffScreen(window);
        await Task.Delay(1500); // The words finish arriving.

        light.Visibility = dark.Visibility = Visibility.Hidden;
        stage.UpdateLayout();
        var picture = new RenderTargetBitmap((int)page.Width, (int)page.Height, 96, 96, PixelFormats.Pbgra32);
        picture.Render(page);
        light.Visibility = dark.Visibility = Visibility.Visible;
        stage.UpdateLayout();

        foreach (var (view, glass) in new[] { (light, lightGlass), (dark, darkGlass) })
        {
            glass.Show(picture);
            glass.Follow(IntPtr.Zero, view.Body.TransformToAncestor(stage).TransformBounds(new Rect(view.Body.RenderSize)), view.Rounding);
            view.GlassUnder(glass.Light);
            stage.UpdateLayout();
            for (var frame = 0; frame < 12; frame++) view.Render(0.35);
        }

        Save(stage, file, scale);
        window.Close();

        static CapsuleView Capsule(Thickness margin)
        {
            var view = new CapsuleView { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top, Margin = margin };
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
            view.Apply(new CapsuleStyle(skin.Id, "#5B8DEF", 1, 1, CapsuleView.Meters[index % CapsuleView.Meters.Length], index < 2));
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

    /// <summary>
    /// WPF's animation clock, moved by hand. WPF keeps a settable clock for its own
    /// tests (TimeManager.TestTimingClock); this reaches it by reflection.
    /// </summary>
    // ponytail: WPF internals; if they move, Take returns null and the tour records in real time (about 13 frames a second under glass).
    private sealed class FrameClock
    {
        private readonly object _timeManager;
        private readonly object _clock;
        private readonly System.Reflection.PropertyInfo _now;
        private readonly System.Reflection.MethodInfo _tick;
        private TimeSpan _time;

        private FrameClock(object timeManager, object clock, System.Reflection.PropertyInfo now, System.Reflection.MethodInfo tick, TimeSpan time)
        {
            (_timeManager, _clock, _now, _tick, _time) = (timeManager, clock, now, tick, time);
        }

        public static FrameClock? Take()
        {
            try
            {
                const System.Reflection.BindingFlags Any = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static
                    | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
                var core = typeof(System.Windows.Media.Animation.Timeline).Assembly;
                var context = core.GetType("System.Windows.Media.MediaContext")!.GetMethod("From", Any)!.Invoke(null, new object[] { Application.Current.Dispatcher })!;
                var timeManager = context.GetType().GetProperty("TimeManager", Any)!.GetValue(context)!;
                var clockProperty = timeManager.GetType().GetProperty("Clock", Any)!;
                var real = clockProperty.GetValue(timeManager)!;
                var timeOf = real.GetType().GetInterfaces().Single(face => face.Name == "IClock").GetProperty("CurrentTime")!;
                var clock = Activator.CreateInstance(timeManager.GetType().GetNestedType("TestTimingClock", Any)!, nonPublic: true)!;
                var now = clock.GetType().GetProperty("CurrentTime", Any)!;
                var time = (TimeSpan)timeOf.GetValue(real)!;
                now.SetValue(clock, time); // Carries on from the real time, so running animations don't jump.
                clockProperty.SetValue(timeManager, clock);
                return new FrameClock(timeManager, clock, now, timeManager.GetType().GetMethod("Tick", Any, Type.EmptyTypes)!, time);
            }
            catch (Exception error) when (error is NullReferenceException or InvalidOperationException or InvalidCastException or ArgumentException
                                           or System.Reflection.TargetInvocationException or MissingMemberException)
            {
                return null;
            }
        }

        public void Advance(TimeSpan by)
        {
            _time += by;
            _now.SetValue(_clock, _time);
            _tick.Invoke(_timeManager, null);
        }
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
