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
                IsChecked = skin.Id == _skin,
                Content = new StackPanel { Children = { mini, name } },
            };
            AutomationProperties.SetName(tile, $"{skin.Name} skin");
            var id = skin.Id;
            tile.Checked += (_, _) =>
            {
                _skin = id;
                RefreshCapsule();
            };
            _skinTiles.Add((skin.Id, mini));
            gallery.Children.Add(tile);
        }

        // Position
        _position = Combo(PresetId(_placement), 160,
            CapsulePlacement.Presets.Select(preset => (preset.Id, preset.Name)).Append(("custom", "Custom")).ToArray());
        _position.SelectionChanged += (_, _) =>
        {
            if (_syncingPosition) return;
            var chosen = CapsulePlacement.Presets.FirstOrDefault(preset => preset.Id == Selected(_position));
            if (chosen.Id is null) return; // "Custom" keeps the dragged spot.
            _placement = chosen.Placement;
            RefreshCapsule();
        };
        var move = new Button { Content = "Move…", Margin = new Thickness(8, 0, 0, 0) };
        AutomationProperties.SetName(move, "Move the capsule by dragging it");
        move.Click += (_, _) =>
        {
            // The editor shows its own capsule; the live one steps aside (the window deactivates).
            var editor = new CapsuleLayoutEditor(PendingStyle(), _placement, this);
            if (editor.ShowDialog() != true || editor.Result is not { } placed) return;
            _placement = placed;
            SyncPosition();
            RefreshCapsule();
        };
        _screen = Combo(_settings.CapsuleScreen, 190, ("active", "Where you're working"), ("primary", "Main screen"));
        _screen.SelectionChanged += (_, _) => RefreshCapsule();

        // Look
        var sizeValue = Text($"{_settings.CapsuleScale:0.0}×", 13, "Muted");
        sizeValue.Width = 40;
        sizeValue.TextAlignment = TextAlignment.Right;
        _scale = new Slider
        {
            Minimum = 0.7,
            Maximum = 1.6,
            Value = _settings.CapsuleScale,
            TickFrequency = 0.1,
            SmallChange = 0.1,
            LargeChange = 0.1,
            IsSnapToTickEnabled = true,
            Width = 150,
        };
        AutomationProperties.SetName(_scale, "Capsule size");
        _scale.ValueChanged += (_, e) =>
        {
            sizeValue.Text = $"{e.NewValue:0.0}×";
            RefreshCapsule();
        };

        var opacityValue = Text($"{_settings.CapsuleOpacity:0%}", 13, "Muted");
        opacityValue.Width = 40;
        opacityValue.TextAlignment = TextAlignment.Right;
        _opacity = new Slider
        {
            Minimum = 0.5,
            Maximum = 1,
            Value = Math.Clamp(_settings.CapsuleOpacity, 0.5, 1),
            TickFrequency = 0.05,
            SmallChange = 0.05,
            LargeChange = 0.1,
            IsSnapToTickEnabled = true,
            Width = 150,
        };
        AutomationProperties.SetName(_opacity, "Capsule opacity");
        _opacity.ValueChanged += (_, e) =>
        {
            opacityValue.Text = $"{e.NewValue:0%}";
            RefreshCapsule();
        };

        var swatches = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var (hex, swatchName) in Accents)
        {
            var swatch = new RadioButton
            {
                Style = StyleOf("Swatch"),
                GroupName = "accent",
                Background = (Brush)new BrushConverter().ConvertFromString(hex)!,
                IsChecked = string.Equals(hex, _accent, StringComparison.OrdinalIgnoreCase),
                ToolTip = swatchName,
            };
            AutomationProperties.SetName(swatch, swatchName);
            swatch.Checked += (_, _) =>
            {
                _accent = hex;
                RefreshCapsule();
            };
            swatches.Children.Add(swatch);
        }

        _meter = Combo(_settings.CapsuleMeter, 150, ("bars", "Bars"), ("wave", "Wave"), ("pulse", "Pulse"));
        _meter.SelectionChanged += (_, _) => RefreshCapsule();

        var widthValue = Text($"{_settings.CapsuleMaxWidth:0}", 13, "Muted");
        widthValue.Width = 40;
        widthValue.TextAlignment = TextAlignment.Right;
        _maxWidth = new Slider
        {
            Minimum = CapsuleView.LeastWidest,
            Maximum = CapsuleView.MostWidest,
            Value = Math.Clamp(_settings.CapsuleMaxWidth, CapsuleView.LeastWidest, CapsuleView.MostWidest),
            TickFrequency = 20,
            SmallChange = 20,
            LargeChange = 100,
            IsSnapToTickEnabled = true,
            Width = 150,
        };
        AutomationProperties.SetName(_maxWidth, "Widest the capsule grows");
        _maxWidth.ValueChanged += (_, e) =>
        {
            widthValue.Text = $"{e.NewValue:0}";
            RefreshCapsule();
        };

        _liveGlass = new CheckBox { IsChecked = _settings.CapsuleLiveGlass };
        _liveGlass.Checked += (_, _) => RefreshCapsule();
        _liveGlass.Unchecked += (_, _) => RefreshCapsule();

        // What it shows
        _liveText = new CheckBox { IsChecked = _settings.CapsuleLiveText };
        _liveText.Checked += (_, _) => RefreshCapsule();
        _liveText.Unchecked += (_, _) => RefreshCapsule();
        _timer = new CheckBox { IsChecked = _settings.CapsuleTimer };
        _timer.Checked += (_, _) => RefreshCapsule();
        _timer.Unchecked += (_, _) => RefreshCapsule();

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
                Row("Live glass (experimental)", _liveGlass,
                    "Liquid Glass shows the screen behind it, blurred and bent at the rim. The capsule then stays out of screenshots and screen sharing.")),
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

    private CapsuleStyle PendingStyle() =>
        new(_skin, _accent, _scale.Value, _opacity.Value, Selected(_meter) ?? "bars", _timer.IsChecked == true, _maxWidth.Value);

    private CapsuleLook PendingLook() =>
        new(PendingStyle(), _placement, Selected(_screen) == "primary", _liveText.IsChecked == true, _liveGlass.IsChecked == true);

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
        if (_meter is null || _timer is null || _liveText is null || _opacity is null || _screen is null
            || _maxWidth is null || _liveGlass is null) return; // Still being built.

        // Live glass is a way of drawing Liquid Glass; other skins don't use it.
        _liveGlass.IsEnabled = CapsuleSkin.Find(_skin).Glass;

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
        var id = PresetId(_placement);
        _position.SelectedItem = _position.Items.Cast<ComboBoxItem>().First(item => (string)item.Tag == id);
        _syncingPosition = false;
    }

    private static string PresetId(CapsulePlacement placement) =>
        CapsulePlacement.Presets.FirstOrDefault(preset => preset.Placement == placement).Id ?? "custom";
}
