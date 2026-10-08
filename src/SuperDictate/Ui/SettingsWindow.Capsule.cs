using System;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace SuperDictate.Ui;

/// <summary>
/// The Capsule page. While it is open (and the window is in front) the real
/// capsule shows on screen, coming in as it does for a dictation, with sample
/// words building up in it; every change on the page shows on it at once.
/// Save makes the changes stick; closing without saving puts the old look back.
/// </summary>
public sealed partial class SettingsWindow
{
    private FrameworkElement CapsulePage()
    {
        // Skins: an even grid of tiles, as many columns as fit.
        var gallery = new UniformGrid { Columns = 3, Margin = new Thickness(14, 6, 14, 10) };
        gallery.SizeChanged += (_, e) => gallery.Columns = Math.Clamp((int)(e.NewSize.Width / 150), 2, 4);
        foreach (var skin in CapsuleSkin.All)
        {
            var mini = new CapsuleView { IsHitTestVisible = false, HorizontalAlignment = HorizontalAlignment.Center };
            var name = Text(skin.Name, 12);
            name.HorizontalAlignment = HorizontalAlignment.Center;
            name.Margin = new Thickness(0, 8, 0, 0);
            var tile = new RadioButton
            {
                Style = StyleOf("SkinTile"),
                GroupName = "capsule-skin",
                Margin = new Thickness(4),
                IsChecked = skin.Id == _draft.Pending.CapsuleSkin,
                Content = new StackPanel { Children = { mini, name } },
            };
            AutomationProperties.SetName(tile, $"{skin.Name} skin");
            var id = skin.Id;
            tile.Checked += (_, _) => Edit(s => s.CapsuleSkin = id);
            _shows.Add(() => tile.IsChecked = _draft.Pending.CapsuleSkin == id);
            _skinTiles.Add((skin.Id, mini));
            gallery.Children.Add(tile);
        }

        // Position
        _position = Combo(PresetId(Placement), 160,
            CapsulePlacement.Presets.Select(preset => (preset.Id, preset.Name)).Append(("custom", "Custom")).ToArray());
        _position.SelectionChanged += (_, _) =>
        {
            if (_syncingPosition) return;
            var chosen = CapsulePlacement.Presets.FirstOrDefault(preset => preset.Id == Selected(_position));
            if (chosen.Id is null) return; // "Custom" keeps the dragged spot.
            Edit(s => Place(s, chosen.Placement));
        };
        var move = new Button { Content = "Move…", Margin = new Thickness(8, 0, 0, 0) };
        AutomationProperties.SetName(move, "Move the capsule by dragging it");
        move.Click += (_, _) =>
        {
            // The editor shows its own capsule; the live one steps aside (the window deactivates).
            var editor = new CapsuleLayoutEditor(PendingStyle(), Placement, this);
            if (editor.ShowDialog() != true || editor.Result is not { } placed) return;
            Edit(s => Place(s, placed));
            SyncPosition();
        };
        _screen = Choice(190, s => s.CapsuleScreen, (s, screen) => s.CapsuleScreen = screen,
            ("active", "Where you're working"), ("primary", "Main screen"));
        _shows.Add(SyncPosition);

        // Look
        var sizeValue = Text($"{_draft.Pending.CapsuleScale:0.0}×", 13, "Muted");
        sizeValue.Width = 40;
        sizeValue.TextAlignment = TextAlignment.Right;
        _scale = new Slider
        {
            Minimum = 0.7,
            Maximum = 1.6,
            TickFrequency = 0.1,
            SmallChange = 0.1,
            LargeChange = 0.1,
            IsSnapToTickEnabled = true,
            Width = 150,
        };
        AutomationProperties.SetName(_scale, "Capsule size");
        _scale.ValueChanged += (_, e) => sizeValue.Text = $"{e.NewValue:0.0}×";
        Bind(_scale, s => s.CapsuleScale, (s, scale) => s.CapsuleScale = scale);

        var opacityValue = Text($"{_draft.Pending.CapsuleOpacity:0%}", 13, "Muted");
        opacityValue.Width = 40;
        opacityValue.TextAlignment = TextAlignment.Right;
        _opacity = new Slider
        {
            Minimum = 0.5,
            Maximum = 1,
            TickFrequency = 0.05,
            SmallChange = 0.05,
            LargeChange = 0.1,
            IsSnapToTickEnabled = true,
            Width = 150,
        };
        AutomationProperties.SetName(_opacity, "Capsule opacity");
        _opacity.ValueChanged += (_, e) => opacityValue.Text = $"{e.NewValue:0%}";
        Bind(_opacity, s => Math.Clamp(s.CapsuleOpacity, 0.5, 1), (s, opacity) => s.CapsuleOpacity = opacity);

        var swatches = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var (hex, swatchName) in Accents)
        {
            var swatch = new RadioButton
            {
                Style = StyleOf("Swatch"),
                GroupName = "accent",
                Background = (Brush)new BrushConverter().ConvertFromString(hex)!,
                IsChecked = string.Equals(hex, _draft.Pending.CapsuleAccent, StringComparison.OrdinalIgnoreCase),
                ToolTip = swatchName,
            };
            AutomationProperties.SetName(swatch, swatchName);
            swatch.Checked += (_, _) => Edit(s => s.CapsuleAccent = hex);
            _shows.Add(() => swatch.IsChecked = string.Equals(hex, _draft.Pending.CapsuleAccent, StringComparison.OrdinalIgnoreCase));
            swatches.Children.Add(swatch);
        }

        _meter = Choice(150, s => s.CapsuleMeter, (s, meter) => s.CapsuleMeter = meter, ("bars", "Bars"), ("wave", "Wave"), ("pulse", "Pulse"));

        var widthValue = Text($"{_draft.Pending.CapsuleMaxWidth:0}", 13, "Muted");
        widthValue.Width = 40;
        widthValue.TextAlignment = TextAlignment.Right;
        _maxWidth = new Slider
        {
            Minimum = CapsuleView.LeastWidest,
            Maximum = CapsuleView.MostWidest,
            TickFrequency = 20,
            SmallChange = 20,
            LargeChange = 100,
            IsSnapToTickEnabled = true,
            Width = 150,
        };
        AutomationProperties.SetName(_maxWidth, "Widest the capsule grows");
        _maxWidth.ValueChanged += (_, e) => widthValue.Text = $"{e.NewValue:0}";
        Bind(_maxWidth, s => Math.Clamp(s.CapsuleMaxWidth, CapsuleView.LeastWidest, CapsuleView.MostWidest), (s, width) => s.CapsuleMaxWidth = width);

        _liveGlass = Switch(s => s.CapsuleLiveGlass, (s, on) => s.CapsuleLiveGlass = on);

        // What it shows
        _liveText = Switch(s => s.CapsuleLiveText, (s, on) => s.CapsuleLiveText = on);
        _timer = Switch(s => s.CapsuleTimer, (s, on) => s.CapsuleTimer = on);

        var page = Page(
            LiveBanner(),
            Group("Skin", gallery),
            Group("Look",
                Row("Size", new StackPanel { Orientation = Orientation.Horizontal, Children = { _scale, sizeValue } }),
                Row("Opacity", new StackPanel { Orientation = Orientation.Horizontal, Children = { _opacity, opacityValue } }),
                Row("Accent", swatches, "Color of the voice meter, and of the Neon and Aurora rims"),
                Row("Voice meter", _meter),
                Row("Widest", new StackPanel { Orientation = Orientation.Horizontal, Children = { _maxWidth, widthValue } },
                    "How far it grows as your words arrive, in points at 1×"),
                Row("Live glass", _liveGlass,
                    "Liquid Glass shows the screen behind it, bent at the edge as through real glass. Off, it is painted, and shows in screenshots and screen sharing.")),
            Group("Position",
                Row("Place", new StackPanel { Orientation = Orientation.Horizontal, Children = { _position, move } },
                    "Move… lets you drag it anywhere. By the left or right edge it grows away from that edge."),
                Row("Screen", _screen, "Where you're working follows the window you type in")),
            Group("While you dictate",
                Row("Show words as you speak", _liveText, "A quick draft of your words in the capsule. Off saves work on a slow PC."),
                Row("Show recording time", _timer)));

        // The live capsule shows only while this page is on screen and the window is in front.
        page.IsVisibleChanged += (_, _) => Stage(page.IsVisible && IsActive);
        Activated += (_, _) => Stage(page.IsVisible);
        Deactivated += (_, _) => Stage(false);
        RefreshCapsule();
        return page;
    }

    /// <summary>Says where the preview is: on the screen itself.</summary>
    private Border LiveBanner()
    {
        var icon = Glyph("", 15, new Thickness(0, 1, 12, 0));
        icon.Foreground = Br("Link");
        icon.VerticalAlignment = VerticalAlignment.Top;
        DockPanel.SetDock(icon, Dock.Left);
        var text = Wrapped(Text("The capsule is live on your screen while this page is open. Every change shows on it at once; Save keeps it.", 13));
        return new Border
        {
            Margin = new Thickness(0, 0, 0, 16),
            Padding = new Thickness(16, 12, 16, 12),
            CornerRadius = new CornerRadius(12),
            Background = Br("AccentSoft"),
            Child = new DockPanel { Children = { icon, text } },
        };
    }

    private CapsuleStyle PendingStyle() => CapsuleStyle.From(_draft.Pending);

    private CapsuleLook PendingLook() => CapsuleLook.From(_draft.Pending);

    private CapsulePlacement Placement => PendingLook().Placement;

    private static void Place(Storage.Settings settings, CapsulePlacement placement)
    {
        settings.CapsuleHorizontal = CapsuleLook.Name(placement.Horizontal);
        settings.CapsuleX = placement.X;
        settings.CapsuleVertical = CapsuleLook.Name(placement.Vertical);
        settings.CapsuleY = placement.Y;
    }

    /// <summary>Shows or puts away the live capsule.</summary>
    private void Stage(bool on)
    {
        if (on == _capsuleStaged) return;
        _capsuleStaged = on;
        if (on)
        {
            _controller.ApplyCapsule(PendingLook());
            _controller.StageCapsule();
        }
        else
        {
            _controller.UnstageCapsule();
        }
    }

    /// <summary>Puts the look being edited on the live capsule and the skin tiles.</summary>
    private void RefreshCapsule()
    {
        if (_liveGlass is null) return; // Still being built.

        // Live glass is a way of drawing Liquid Glass; other skins don't use it.
        _liveGlass.IsEnabled = CapsuleSkin.Find(_draft.Pending.CapsuleSkin).Glass;

        var style = PendingStyle();
        if (_capsuleStaged) _controller.ApplyCapsule(PendingLook());

        foreach (var (skin, view) in _skinTiles)
        {
            view.Apply(style with { Skin = skin, Scale = 0.75, Opacity = 1, Timer = false });
            view.ShowText("Listening…", meter: true);
            view.SetPreviewLevel(0.6);
            for (var frame = 0; frame < 12; frame++) view.Render(0.35); // A still of a voice mid-word.
        }
    }

    private void SyncPosition()
    {
        _syncingPosition = true;
        var id = PresetId(Placement);
        _position.SelectedItem = _position.Items.Cast<ComboBoxItem>().First(item => (string)item.Tag == id);
        _syncingPosition = false;
    }

    private static string PresetId(CapsulePlacement placement) =>
        CapsulePlacement.Presets.FirstOrDefault(preset => preset.Placement == placement).Id ?? "custom";
}
