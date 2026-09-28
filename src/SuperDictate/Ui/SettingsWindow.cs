using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Shell;
using System.Windows.Threading;
using SuperDictate.Audio;
using SuperDictate.Input;
using SuperDictate.Speech;
using SuperDictate.Storage;
using SuperDictate.Support;

namespace SuperDictate.Ui;

/// <summary>
/// Settings and dictation monitor: sidebar navigation, one page per area,
/// grouped rows of label + control. Colors and templates come from <see cref="Theme"/>.
/// </summary>
public sealed class SettingsWindow : Window
{
    private static readonly FontFamily IconFont = new(Theme.IconFont);

    /// <summary>Transparent room around the rounded window for its drop shadow.</summary>
    private const double ShadowMargin = 16;

    private static readonly (string Key, string Glyph, string Title)[] Pages =
    {
        ("dictation", "\uE720", "Dictation"),
        ("languages", "\uE774", "Languages"),
        ("models", "\uE945", "Speech model"),
        ("ai_cleanup", "\uE771", "AI cleanup"),
        ("history", "\uE81C", "History"),
        ("settings", "\uE713", "General"),
        ("support", "\uE006", "Support"),
    };

    private static readonly (string Code, string Native, string English)[] Languages =
    {
        ("en", "English", "English"),
        ("ru", "Русский", "Russian"),
        ("de", "Deutsch", "German"),
        ("pl", "Polski", "Polish"),
    };

    private static readonly (string Hex, string Name)[] Accents =
    {
        ("#5B8DEF", "Blue"),
        ("#635BFF", "Violet"),
        ("#00C853", "Green"),
        ("#FF4081", "Coral"),
    };

    private readonly DictationController _controller;
    private readonly Settings _settings;
    private readonly Dictionary<string, FrameworkElement> _pages = new();
    private readonly Dictionary<string, RadioButton> _navItems = new();
    private readonly ContentControl _host = new();
    private readonly TextBlock _title = new() { FontSize = 20, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly Ellipse _statusDot = new() { Width = 10, Height = 10, Margin = new Thickness(0, 4, 10, 0), VerticalAlignment = VerticalAlignment.Top };
    private readonly TextBlock _statusText = new() { FontWeight = FontWeights.SemiBold };

    // Responsive shell: the sidebar folds into an icon rail when the window is narrow.
    private Grid _frame = null!;
    private Border _sidebar = null!, _statusBox = null!;
    private StackPanel _brand = null!;
    private FrameworkElement _brandName = null!, _statusDetails = null!;
    private readonly List<(RadioButton Item, TextBlock Glyph, TextBlock Label, string Title)> _navParts = new();
    private bool? _compact;
    private readonly TextBlock _engineText = new() { FontSize = 11, TextTrimming = TextTrimming.CharacterEllipsis };

    // Inline problem shown under the title bar when Save can't proceed.
    private readonly TextBlock _noticeText = new() { TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
    private Border _notice = null!;
    private string? _problemPage;
    private Control? _flagged;

    // Read back on save.
    private readonly Dictionary<string, CheckBox> _languages = new();
    private string _accent;
    private ComboBox _langMode = null!, _model = null!, _mic = null!, _aiMode = null!;
    private TextBox _primaryHotkey = null!, _altHotkey = null!, _customFillers = null!;
    private TextBox _ollamaUrl = null!, _ollamaModel = null!, _cloudUrl = null!, _cloudModel = null!;
    private PasswordBox _cloudKey = null!;
    private string _savedCloudKey = "";
    private CheckBox _pressAndHold = null!, _pressEnter = null!;
    private CheckBox _aiEnable = null!, _aiFillers = null!, _aiPunct = null!, _aiDedupe = null!;
    private Slider _scale = null!;

    // Speech model page: the selected model's files, always in the app's Models folder.
    private TextBlock _folderText = null!;
    private Button _save = null!;
    private readonly System.Collections.Generic.List<ModelRow> _modelRows = new();
    private Job _runtimeRow = null!, _gpuRow = null!, _llmRow = null!;
    private FrameworkElement _gpuRowView = null!;
    private readonly DispatcherTimer _downloadTimer = new() { Interval = TimeSpan.FromMilliseconds(400) };
    private ModelDownload? _watching;
    private RuntimeInstall? _watchingRuntime;
    private OllamaPull? _watchingPull;
    private Ollama.State? _llmState;
    private System.Threading.CancellationTokenSource? _llmCheck;
    private readonly DispatcherTimer _llmDebounce = new() { Interval = TimeSpan.FromMilliseconds(600) };
    private CheckBox _micSwitch = null!;
    private Job _updateRow = null!;
    private UpdateInfo? _available;
    private System.Threading.CancellationTokenSource? _updateWork;
    private string? _confirmDelete;
    private readonly DispatcherTimer _confirmTimer = new() { Interval = TimeSpan.FromSeconds(4) };
    private TextBlock _noticeIcon = null!;
    private Border _noticeBox = null!;

    /// <summary>Status line, progress bar and action button of one downloadable thing.</summary>
    private sealed record Job(TextBlock Status, ProgressBar Progress, Button Action);

    private sealed record ModelRow(ModelLibrary.Model Model, Job Job);
    private readonly string _initialPage;

    // Live dictation monitor.
    private Button _record = null!;
    private TextBlock _recordGlyph = null!, _recordText = null!, _lastMeta = null!, _lastText = null!;
    private Button _copyLast = null!;

    // Hotkey recording: the box being recorded, its text before, and the keys seen so far.
    private TextBox? _recordingBox;
    private Button? _recordingButton;
    private string _textBeforeRecording = "";
    private readonly HashSet<Key> _keysDown = new();
    private readonly List<Key> _chordKeys = new();
    private ProgressBar _level = null!;
    private string? _lastTranscript;

    /// <param name="page">Page key to open on, e.g. "models" from the tray.</param>
    public SettingsWindow(DictationController controller, Settings settings, string page = "languages")
    {
        _controller = controller;
        _settings = settings;
        _initialPage = page;
        _accent = settings.CapsuleAccent;
        _lastTranscript = controller.LastTranscript;

        Title = "SuperDictate";
        Icon = AppIcon.Window;
        // Resizable within the work area; narrow windows get the compact layout (Adapt).
        MaxWidth = SystemParameters.WorkArea.Width;
        MaxHeight = SystemParameters.WorkArea.Height;
        Width = Math.Min(900 + 2 * ShadowMargin, MaxWidth);
        Height = Math.Min(620 + 2 * ShadowMargin, MaxHeight);
        MinWidth = 520 + 2 * ShadowMargin;
        MinHeight = 440 + 2 * ShadowMargin;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.CanResize;
        // The transparent shadow margin doubles as the resize border.
        WindowChrome.SetWindowChrome(this, new WindowChrome
        {
            CaptionHeight = 0,
            ResizeBorderThickness = new Thickness(ShadowMargin),
            GlassFrameThickness = new Thickness(0),
            UseAeroCaptionButtons = false,
        });
        FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI");
        FontSize = 13;
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
        Resources = Theme.Create();
        Foreground = Br("Text");
        _engineText.Foreground = Br("Muted");

        _pages["dictation"] = DictationPage();
        _pages["languages"] = LanguagesPage();
        _pages["models"] = ModelPage();
        _pages["ai_cleanup"] = CleanupPage();
        _pages["settings"] = GeneralPage();
        _pages["support"] = SupportPage();

        // The shadow sits on its own layer so the effect never touches the text above it.
        _frame = new Grid
        {
            Margin = new Thickness(ShadowMargin),
            Children =
            {
                new Border
                {
                    CornerRadius = new CornerRadius(12),
                    Background = Br("Bg"),
                    Effect = new DropShadowEffect { BlurRadius = 24, ShadowDepth = 6, Direction = 270, Opacity = 0.45 },
                },
                new Border
                {
                    CornerRadius = new CornerRadius(12),
                    Background = Br("Bg"),
                    BorderBrush = Br("Stroke"),
                    BorderThickness = new Thickness(1),
                    Child = new DockPanel { Children = { Sidebar(out var defaultPage), Main() } },
                },
            },
        };
        Content = _frame;
        SizeChanged += (_, _) => Adapt();
        // Maximized (Win+Up), the window fills the work area without its shadow margin.
        StateChanged += (_, _) => _frame.Margin = new Thickness(WindowState == WindowState.Maximized ? 0 : ShadowMargin);

        _controller.StateChanged += OnStateChanged;
        _controller.LevelChanged += OnLevelChanged;
        _controller.DictationCompleted += OnDictationCompleted;
        Closed += (_, _) =>
        {
            StopRecording();
            _downloadTimer.Stop();
            _llmDebounce.Stop();
            _llmCheck?.Cancel();
            // Undo an unsaved preview; after Save these are the new values anyway.
            _controller.ApplyCapsule(_settings.CapsuleScale, _settings.CapsuleAccent);
            _controller.StateChanged -= OnStateChanged;
            _controller.LevelChanged -= OnLevelChanged;
            _controller.DictationCompleted -= OnDictationCompleted;
        };

        ShowState(_controller.State);
        defaultPage.IsChecked = true;

        if (!SpeechRuntime.IsInstalled || !ModelLibrary.All.Any(model => ModelLibrary.IsPresent(AppPaths.Models, model.Id)))
        {
            ShowProblem("models", "Welcome! To start dictating, install the speech runtime, then download a model.", error: false);
        }
    }

    // ---------- Shell ----------

    private FrameworkElement Sidebar(out RadioButton defaultPage)
    {
        var name = Text("SuperDictate", 15, bold: true);
        name.Margin = new Thickness(12, 0, 0, 0);
        _brandName = name;
        _brand = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(18, 18, 18, 16),
            Background = Brushes.Transparent,
            Children = { Logo(34), name },
        };
        _brand.MouseLeftButtonDown += (_, _) => DragMove();

        var nav = new StackPanel { Margin = new Thickness(10, 0, 10, 0) };
        // Like a tab strip: only the current page is a Tab stop, and Up/Down switch pages.
        nav.PreviewKeyDown += (_, e) =>
        {
            if (e.Key is not (Key.Up or Key.Down)) return;
            var items = nav.Children.OfType<RadioButton>().ToList();
            var current = items.FindIndex(item => item.IsChecked == true);
            var next = items[(current + (e.Key == Key.Down ? 1 : items.Count - 1)) % items.Count];
            next.IsChecked = true;
            next.Focus();
            e.Handled = true;
        };
        defaultPage = null!;
        foreach (var (key, glyph, title) in Pages)
        {
            var icon = Glyph(glyph, 18, new Thickness(4, 0, 14, 0));
            var label = new TextBlock { Text = title, FontSize = 13.5, VerticalAlignment = VerticalAlignment.Center };
            var item = new RadioButton
            {
                Style = StyleOf("Nav"),
                GroupName = "nav",
                IsTabStop = false,
                Content = new StackPanel { Orientation = Orientation.Horizontal, Children = { icon, label } },
            };
            AutomationProperties.SetName(item, title);
            item.Checked += (_, _) =>
            {
                item.IsTabStop = true;
                Navigate(key, title);
            };
            item.Unchecked += (_, _) => item.IsTabStop = false;
            _navItems[key] = item;
            _navParts.Add((item, icon, label, title));
            nav.Children.Add(item);
            if (key == _initialPage) defaultPage = item;
        }

        var details = new StackPanel { Children = { _statusText, _engineText } };
        _statusDetails = details;
        var statusRow = new DockPanel { Children = { _statusDot, details } };
        DockPanel.SetDock(_statusDot, Dock.Left);
        _statusBox = new Border
        {
            Margin = new Thickness(10, 8, 10, 12),
            Padding = new Thickness(12, 10, 12, 10),
            CornerRadius = new CornerRadius(12),
            Background = Br("Bg"),
            Child = statusRow,
        };

        var panel = new DockPanel { Children = { _brand, _statusBox, nav } };
        DockPanel.SetDock(_brand, Dock.Top);
        DockPanel.SetDock(_statusBox, Dock.Bottom);

        _sidebar = new Border
        {
            Width = 236,
            CornerRadius = new CornerRadius(11, 0, 0, 11),
            Background = Br("Side"),
            BorderBrush = Br("Stroke"),
            BorderThickness = new Thickness(0, 0, 1, 0),
            Child = panel,
        };
        DockPanel.SetDock(_sidebar, Dock.Left);
        return _sidebar;
    }

    /// <summary>
    /// Below 780 px the sidebar folds into a 72 px icon rail, the way Telegram
    /// folds its chat list; page names move into tooltips. Rows stack on their own (Row).
    /// </summary>
    private void Adapt()
    {
        var compact = ActualWidth - 2 * ShadowMargin < 780;
        if (compact == _compact) return;
        _compact = compact;

        _sidebar.Width = compact ? 72 : 236;
        _brand.Margin = compact ? new Thickness(19, 18, 19, 16) : new Thickness(18, 18, 18, 16);
        _brandName.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        _statusDetails.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        _statusDot.Margin = compact ? new Thickness(0) : new Thickness(0, 4, 10, 0);
        ((FrameworkElement)_statusBox.Child).HorizontalAlignment = compact ? HorizontalAlignment.Center : HorizontalAlignment.Stretch;
        _statusBox.ToolTip = compact ? _statusText.Text : null;
        foreach (var (item, glyph, label, title) in _navParts)
        {
            item.HorizontalContentAlignment = compact ? HorizontalAlignment.Center : HorizontalAlignment.Left;
            glyph.Margin = compact ? new Thickness(0) : new Thickness(4, 0, 14, 0);
            label.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
            item.ToolTip = compact ? title : null;
        }
    }

    private FrameworkElement Main()
    {
        var save = _save = new Button { Content = "Save", Style = StyleOf("AccentButton"), MinWidth = 88, Margin = new Thickness(12, 0, 10, 0) };
        save.Click += (_, _) => SaveAndApply();
        var minimize = IconButton("\uE921", "Minimize", () => WindowState = WindowState.Minimized, size: 10);
        var close = IconButton("\uE8BB", "Close", Close, "CloseButton", size: 10);

        var top = new Grid
        {
            Height = 68,
            Margin = new Thickness(28, 0, 14, 0),
            Background = Brushes.Transparent,
            ColumnDefinitions =
            {
                new ColumnDefinition(),
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto },
            },
        };
        top.Children.Add(_title);
        AutomationProperties.SetHeadingLevel(_title, AutomationHeadingLevel.Level1);
        foreach (var (button, column) in new[] { (save, 1), (minimize, 2), (close, 3) })
        {
            button.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(button, column);
            top.Children.Add(button);
        }
        top.MouseLeftButtonDown += (_, _) => DragMove();

        _noticeIcon = Glyph("\uE783", 15, new Thickness(0, 1, 12, 0));
        _noticeIcon.VerticalAlignment = VerticalAlignment.Top;
        DockPanel.SetDock(_noticeIcon, Dock.Left);
        _noticeText.Foreground = Br("Text");
        AutomationProperties.SetLiveSetting(_noticeText, AutomationLiveSetting.Assertive);
        _noticeBox = new Border
        {
            Margin = new Thickness(28, 0, 28, 14),
            Padding = new Thickness(16, 12, 16, 12),
            CornerRadius = new CornerRadius(12),
            MaxWidth = PageWidth,
            Visibility = Visibility.Collapsed,
            Child = new DockPanel { Children = { _noticeIcon, _noticeText } },
        };
        _notice = _noticeBox;

        // Tab order follows child order: the page first, then Save and the window buttons.
        _host.IsTabStop = false;
        Grid.SetRow(_notice, 1);
        Grid.SetRow(_host, 2);
        return new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition(),
            },
            Children = { _host, _notice, top },
        };
    }

    private void Navigate(string key, string title)
    {
        _title.Text = title;
        // History is rebuilt on every visit so it includes dictations made while the window is open.
        _host.Content = key == "history" ? HistoryPage() : _pages[key];
    }

    // ---------- Pages ----------

    private FrameworkElement DictationPage()
    {
        _recordGlyph = Glyph("\uE720", 13, new Thickness(0, 0, 8, 0));
        _recordText = new TextBlock { Text = "Dictate" };
        _record = new Button
        {
            Style = StyleOf("AccentButton"),
            Height = 38,
            MinWidth = 120,
            Content = new StackPanel { Orientation = Orientation.Horizontal, Children = { _recordGlyph, _recordText } },
        };
        _record.Click += (_, _) => _controller.Toggle(_settings.PressEnterAfterPaste);
        _level = new ProgressBar { Maximum = 1, Margin = new Thickness(0, 10, 0, 0) };
        AutomationProperties.SetName(_level, "Microphone level");

        var intro = new StackPanel
        {
            Margin = new Thickness(0, 0, 16, 0),
            Children = { Text("Test dictation", bold: true), Text($"Press {_settings.PrimaryHotkey} anywhere, or click Dictate.", 12, "Muted"), _level },
        };

        _lastMeta = Text("Last transcript", 12, "Muted");
        _lastText = Wrapped(Text(_lastTranscript ?? "Nothing dictated yet.", 12, _lastTranscript is null ? "Muted" : "Text"));
        _lastText.Margin = new Thickness(0, 2, 0, 0);
        _copyLast = CopyButton("Copy transcript", () => _lastTranscript);
        _copyLast.IsEnabled = _lastTranscript is not null;

        var devices = MicrophoneCapture.GetAvailableDevices();
        _mic = Combo(_settings.MicrophoneId ?? "", 260,
            new[] { ("", "System default") }
                .Concat(devices.Select(d => (d.Id, d.IsDefault ? $"{d.Name} (default)" : d.Name)))
                .ToArray());

        _primaryHotkey = Input(_settings.PrimaryHotkey, 160);
        _altHotkey = Input(_settings.AlternateHotkey, 160);
        _primaryHotkey.TextChanged += (_, _) => ClearProblem("dictation");
        _altHotkey.TextChanged += (_, _) => ClearProblem("dictation");
        _pressAndHold = new CheckBox { IsChecked = _settings.PressAndHold };
        _pressEnter = new CheckBox { IsChecked = _settings.PressEnterAfterPaste };

        return Page(
            Group(null,
                Padded(Columns(intro, _record)),
                Padded(Columns(new StackPanel { Children = { _lastMeta, _lastText } }, _copyLast))),
            Group("Input",
                Row("Microphone", _mic)),
            Group("Hotkeys",
                Row("Dictation hotkey", HotkeyField(_primaryHotkey, "Dictation hotkey"), "Record it, or type one like RightCtrl+Space"),
                Row("Alternate hotkey", HotkeyField(_altHotkey, "Alternate hotkey"), "Finishes with the opposite Enter behavior"),
                Row("Hold to talk", _pressAndHold, "Transcribe when the hotkey is released"),
                Row("Press Enter after paste", _pressEnter)));
    }

    private FrameworkElement LanguagesPage()
    {
        var selected = new HashSet<string>(
            _settings.SelectedLanguages ?? Languages.Select(l => l.Code).ToList(), StringComparer.OrdinalIgnoreCase);

        var chips = new UniformGrid { Columns = Languages.Length, Margin = new Thickness(-3, 0, -3, 0) };
        foreach (var (code, native, english) in Languages)
        {
            var chip = new CheckBox
            {
                Style = StyleOf("Chip"),
                IsChecked = selected.Contains(code),
                Margin = new Thickness(3, 0, 3, 0),
                Content = new TextBlock
                {
                    Inlines = { new Run(code.ToUpperInvariant() + "  ") { FontWeight = FontWeights.Bold, FontSize = 11 }, new Run(native) },
                },
            };
            AutomationProperties.SetName(chip, english);
            chip.Checked += (_, _) => ClearProblem("languages");
            _languages[code] = chip;
            chips.Children.Add(chip);
        }

        var hint = Wrapped(Text("Auto-detect only considers the selected languages, so unused ones never cause false detections.", 12, "Muted"));
        hint.Margin = new Thickness(0, 10, 0, 0);

        _langMode = Combo(string.IsNullOrEmpty(_settings.Language) ? "auto" : _settings.Language, 200,
            new[] { ("auto", "Auto-detect") }
                .Concat(Languages.Select(l => (l.Code, $"{l.English} only")))
                .ToArray());

        return Page(
            Group("Recognize", Padded(new StackPanel { Children = { chips, hint } })),
            Group("Detection", Row("Mode", _langMode, "Force one language to skip detection")));
    }

    private FrameworkElement ModelPage()
    {
        _model = Combo(_settings.ModelId, 240, ModelLibrary.All.Select(model => (model.Id, model.Name)).ToArray());
        _model.SelectionChanged += (_, _) => ShowModelStatus();

        _runtimeRow = NewJob("Speech runtime install progress", () => InstallOrCancelRuntime(includeGpu: false));
        _gpuRow = NewJob("GPU pack install progress", () => InstallOrCancelRuntime(includeGpu: true));
        foreach (var model in ModelLibrary.All)
        {
            _modelRows.Add(new ModelRow(model, NewJob($"{model.Name} download progress", () => ModelAction(model))));
        }

        _folderText = Wrapped(Text(AppPaths.RealPath(AppPaths.Models), 12, "Muted"));

        _confirmTimer.Tick += (_, _) =>
        {
            _confirmTimer.Stop();
            _confirmDelete = null;
            ShowModelStatus();
        };
        _gpuRowView = JobRow("GPU acceleration", _gpuRow);
        _gpuRowView.Visibility = SpeechRuntime.NvidiaGpuPresent ? Visibility.Visible : Visibility.Collapsed;
        _downloadTimer.Tick += (_, _) =>
        {
            ShowModelStatus();
            ShowLlmStatus();
        };
        ShowModelStatus();

        return Page(
            Group("Engine",
                Row("Model", _model, "faster-whisper, runs on this PC"),
                Row("Acceleration", Text("Automatic", 13, "Muted"), "CUDA GPU when available, otherwise CPU")),
            Group("Speech runtime",
                JobRow("Runtime", _runtimeRow),
                _gpuRowView),
            Group("Models", _modelRows.Select(row => JobRow(row.Model.Name, row.Job)).ToArray()),
            Group("Files",
                Row("Model folder", OpenFolderButton(() => AppPaths.Models, "Open model folder"), "Inside the SuperDictate folder", detail: _folderText)));
    }

    private Job NewJob(string progressName, Action onAction)
    {
        var progress = new ProgressBar { Maximum = 1, Margin = new Thickness(0, 6, 0, 2), Visibility = Visibility.Collapsed };
        AutomationProperties.SetName(progress, progressName);
        var action = new Button { MinWidth = 88 };
        action.Click += (_, _) => onAction();
        return new Job(Wrapped(Text("", 12, "Muted")), progress, action);
    }

    private FrameworkElement JobRow(string label, Job job) =>
        Row(label, job.Action, detail: new StackPanel { Children = { job.Status, job.Progress } });

    /// <summary>Status line; progress while running; an action button, or none when there is nothing to do.</summary>
    private void ShowJob(Job job, string status, double? progress = null, string? action = null, string? actionName = null, bool accent = false, bool enabled = true, bool danger = false)
    {
        job.Status.Text = status;
        job.Status.Visibility = status.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        job.Progress.Visibility = progress is null ? Visibility.Collapsed : Visibility.Visible;
        job.Progress.Value = progress ?? 0;
        job.Action.Visibility = action is null ? Visibility.Collapsed : Visibility.Visible;
        if (action is null) return;
        job.Action.Content = action;
        // Clearing, not null: a null Style would also drop the app's own button look.
        if (danger || accent) job.Action.Style = StyleOf(danger ? "DangerButton" : "AccentButton");
        else job.Action.ClearValue(StyleProperty);
        job.Action.IsEnabled = enabled;
        AutomationProperties.SetName(job.Action, actionName ?? action);
    }

    /// <summary>Runtime, GPU pack and every model: installed, missing, or how far along.</summary>
    private void ShowModelStatus()
    {
        if (_watching is { Finished.IsCompleted: true } finished)
        {
            _watching = null;
            if (finished.Finished.Result is { } error && !finished.Cancelled) ShowProblem("models", $"Download failed: {error}");
            else if (finished.Finished.Result is null) GuideSetup();
        }

        if (_watchingRuntime is { Finished.IsCompleted: true } installed)
        {
            _watchingRuntime = null;
            if (installed.Finished.Result is { } error && !installed.Cancelled) ShowProblem("models", $"Setup failed: {error}");
            else if (installed.Finished.Result is null) GuideSetup();
        }

        var runtime = RuntimeInstall.Current;
        var download = ModelDownload.Current;
        KeepTicking();

        // Speech runtime and the optional GPU pack.
        if (runtime is { IncludesGpu: false })
        {
            ShowJob(_runtimeRow, runtime.Status, runtime.Progress, "Cancel", "Cancel runtime install");
        }
        else if (SpeechRuntime.IsInstalled)
        {
            ShowJob(_runtimeRow, "Installed · Python with faster-whisper");
        }
        else
        {
            ShowJob(_runtimeRow, "Not installed · about 100 MB download (300 MB on disk), needed once", action: "Install", actionName: "Install speech runtime", accent: true, enabled: runtime is null);
        }

        if (runtime is { IncludesGpu: true })
        {
            ShowJob(_gpuRow, runtime.Status, runtime.Progress, "Cancel", "Cancel GPU pack install");
        }
        else if (SpeechRuntime.HasGpuPack)
        {
            ShowJob(_gpuRow, "Installed · NVIDIA CUDA");
        }
        else
        {
            ShowJob(_gpuRow,
                SpeechRuntime.IsInstalled ? "Optional · about 1.3 GB download · faster on NVIDIA GPUs" : "Optional · install the runtime first",
                action: "Install", actionName: "Install GPU acceleration", enabled: runtime is null && SpeechRuntime.IsInstalled);
        }

        // Every model, with the one selected above marked.
        var selected = Selected(_model);
        foreach (var (model, job) in _modelRows)
        {
            var inUse = model.Id == selected ? " · selected" : "";
            if (download is not null && download.Model == model)
            {
                var done = download.DownloadedBytes;
                var total = download.TotalBytes;
                ShowJob(job,
                    total > 0 ? $"Downloading… {ModelLibrary.FormatSize(done)} of {ModelLibrary.FormatSize(total)}" : "Starting download…",
                    total > 0 ? Math.Min(1, done / (double)total) : 0,
                    "Cancel", $"Cancel {model.Name} download");
            }
            else if (ModelLibrary.IsPresent(AppPaths.Models, model.Id))
            {
                // The loaded model's files are open in the engine, so it can't be deleted.
                var size = ModelLibrary.FormatSize(ModelLibrary.SizeOnDisk(AppPaths.Models, model.Id));
                var loaded = model.Id == _controller.LoadedModelId;
                if (loaded) ShowJob(job, $"Downloaded · {size} · in use");
                else if (_confirmDelete == model.Id) ShowJob(job, $"Downloaded · {size}{inUse}", action: $"Delete {size}?", actionName: $"Confirm deleting {model.Name}", danger: true);
                else ShowJob(job, $"Downloaded · {size}{inUse}", action: "Delete", actionName: $"Delete {model.Name}");
            }
            else
            {
                ShowJob(job,
                    download is null ? $"Not downloaded · about {ModelLibrary.FormatSize(model.ApproxBytes)}{inUse}" : $"Not downloaded · after {download.Model.Name}{inUse}",
                    action: "Download", actionName: $"Download {model.Name}", accent: model.Id == selected, enabled: download is null);
            }

        }
    }

    /// <summary>After a step of first-run setup finishes: point at the next one, or clear the guide.</summary>
    private void GuideSetup()
    {
        if (!SpeechRuntime.IsInstalled) return;
        if (ModelLibrary.All.Any(model => ModelLibrary.IsPresent(AppPaths.Models, model.Id))) ClearProblem("models");
        else ShowProblem("models", "Speech runtime installed. Now download a model below.", error: false);
    }

    /// <summary>The refresh timer runs only while something is downloading or installing.</summary>
    private void KeepTicking()
    {
        if (ModelDownload.Current is not null || RuntimeInstall.Current is not null || OllamaPull.Current is not null
            || _watching is not null || _watchingRuntime is not null || _watchingPull is not null)
        {
            _downloadTimer.Start();
        }
        else
        {
            _downloadTimer.Stop();
        }
    }

    /// <summary>Download, cancel, or delete in two clicks: the first arms, the second deletes.</summary>
    private void ModelAction(ModelLibrary.Model model)
    {
        if (!ModelLibrary.IsPresent(AppPaths.Models, model.Id) || ModelDownload.Current?.Model == model)
        {
            DownloadOrCancel(model);
            return;
        }

        if (_confirmDelete != model.Id)
        {
            _confirmDelete = model.Id;
            _confirmTimer.Stop();
            _confirmTimer.Start();
            ShowModelStatus();
            return;
        }

        _confirmDelete = null;
        try
        {
            System.IO.Directory.Delete(System.IO.Path.Combine(AppPaths.Models, model.Id), recursive: true);
            AppLogger.Info($"Deleted model {model.Id} from {AppPaths.Models}");
        }
        catch (Exception error) when (error is System.IO.IOException or UnauthorizedAccessException)
        {
            ShowProblem("models", $"Couldn't delete {model.Name}: {error.Message}");
        }

        ShowModelStatus();
    }

    private void DownloadOrCancel(ModelLibrary.Model model)
    {
        if (ModelDownload.Current is { } running)
        {
            running.Cancel();
            return;
        }

        ClearProblem();
        try
        {
            _watching = _controller.DownloadModel(model);
        }
        catch (Exception error) when (error is System.IO.IOException or UnauthorizedAccessException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            ShowProblem("models", $"Couldn't start the download: {error.Message}");
            return;
        }

        ShowModelStatus();
    }

    private void InstallOrCancelRuntime(bool includeGpu)
    {
        if (RuntimeInstall.Current is { } running)
        {
            running.Cancel();
            return;
        }

        ClearProblem();
        try
        {
            _watchingRuntime = _controller.InstallRuntime(includeGpu);
        }
        catch (InvalidOperationException error)
        {
            ShowProblem("models", error.Message);
            return;
        }

        ShowModelStatus();
    }

    private FrameworkElement CleanupPage()
    {
        _aiEnable = new CheckBox { IsChecked = _settings.AiCleanupEnabled };
        _aiMode = Combo(_settings.AiCleanupMode, 240,
            ("local_smart", "Built-in rules (offline)"),
            ("local_llm", "Local LLM (Ollama, LM Studio)"),
            ("cloud", "Cloud LLM (OpenAI-compatible)"));
        _aiFillers = new CheckBox { IsChecked = _settings.AiRemoveFillers };
        _aiPunct = new CheckBox { IsChecked = _settings.AiFormatPunctuation };
        _aiDedupe = new CheckBox { IsChecked = _settings.AiRemoveDuplicates };
        _customFillers = Input(_settings.CustomFillerWords, 200);
        _ollamaUrl = Input(_settings.LocalLlmEndpoint, 240);
        _ollamaModel = Input(_settings.LocalLlmModel, 240);
        _cloudUrl = Input(_settings.AiBaseUrl, 240);
        _cloudModel = Input(_settings.AiModel, 240);
        try
        {
            _savedCloudKey = CredentialStore.Read(CredentialStore.AiCleanupTarget) ?? "";
        }
        catch (System.ComponentModel.Win32Exception error)
        {
            // Show an empty box; saving only touches the credential if the user types a key.
            AppLogger.Error("Could not read the AI cleanup key", error);
        }
        _cloudKey = new PasswordBox { Password = _savedCloudKey, Width = 240 };
        _cloudKey.PasswordChanged += (_, _) => ClearProblem("ai_cleanup");

        _llmRow = NewJob("Local model download progress", LlmAction);
        _llmDebounce.Tick += (_, _) =>
        {
            _llmDebounce.Stop();
            CheckLlm();
        };
        _ollamaUrl.TextChanged += (_, _) => RestartLlmCheck();
        _ollamaModel.TextChanged += (_, _) => RestartLlmCheck();
        var local = Group("Local LLM",
            Row("Endpoint", _ollamaUrl),
            Row("Model", _ollamaModel, "An Ollama model name, like llama3.2:1b"),
            JobRow("Model files", _llmRow));
        var cloud = Group("Cloud LLM", Row("Base URL", _cloudUrl), Row("Model", _cloudModel), Row("API key", _cloudKey, "Stored in Windows Credential Manager. Only the final text is sent."));
        void ShowModePanels()
        {
            var mode = Selected(_aiMode);
            local.Visibility = mode == "local_llm" ? Visibility.Visible : Visibility.Collapsed;
            cloud.Visibility = mode == "cloud" ? Visibility.Visible : Visibility.Collapsed;
            if (mode == "local_llm") CheckLlm();
        }
        _aiMode.SelectionChanged += (_, _) => ShowModePanels();
        ShowModePanels();

        var sample = new TextBox { Text = "ну короче эээ мы должны отправить этот файл типа сегодня запятая чтобы успеть точка", TextWrapping = TextWrapping.Wrap };
        AutomationProperties.SetName(sample, "Sample transcript");
        var output = Wrapped(Text("The cleaned text appears here.", 13, "Muted"));
        var run = new Button { Content = "Run rules", HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 8, 0, 8) };
        run.Click += (_, _) =>
        {
            output.Text = AiCleanupService.CleanLocalSmart(sample.Text, new Settings
            {
                AiRemoveFillers = _aiFillers.IsChecked == true,
                AiFormatPunctuation = _aiPunct.IsChecked == true,
                AiRemoveDuplicates = _aiDedupe.IsChecked == true,
                CustomFillerWords = _customFillers.Text,
            });
            output.Foreground = Br("Text");
        };

        return Page(
            Group("Cleanup",
                Row("Clean up transcripts", _aiEnable, "Runs after recognition, before paste"),
                Row("Engine", _aiMode)),
            Group("Rules",
                Row("Remove fillers", _aiFillers, "um, эээ, äh, yyy, типа"),
                Row("Spoken punctuation", _aiPunct, "“comma” → ,   “точка” → ."),
                Row("Remove repeated words", _aiDedupe, "the the → the"),
                Row("Custom fillers", _customFillers, "Comma-separated")),
            local,
            cloud,
            Group("Try the rules", Padded(new StackPanel { Children = { sample, run, output } })));
    }

    private void RestartLlmCheck()
    {
        _llmState = null;
        ShowLlmStatus();
        _llmDebounce.Stop();
        _llmDebounce.Start();
    }

    /// <summary>Asks the server whether the model is there. A newer check cancels an older one.</summary>
    private async void CheckLlm()
    {
        _llmCheck?.Cancel();
        var check = _llmCheck = new System.Threading.CancellationTokenSource();
        _llmState = null;
        ShowLlmStatus();
        if (string.IsNullOrWhiteSpace(_ollamaModel.Text)) return;

        // Back to the UI thread explicitly: the window can open before the dispatcher
        // loop runs (first start), when there is no context to resume on.
        var state = await Ollama.CheckAsync(_ollamaUrl.Text, _ollamaModel.Text, check.Token).ConfigureAwait(false);
        await Dispatcher.InvokeAsync(() =>
        {
            if (check.IsCancellationRequested) return;
            _llmState = state;
            ShowLlmStatus();
        });
    }

    /// <summary>The local AI cleanup model: in Ollama, missing, downloading, or no Ollama to ask.</summary>
    private void ShowLlmStatus()
    {
        if (_llmRow is null) return;
        if (_watchingPull is { Finished.IsCompleted: true } pulled)
        {
            _watchingPull = null;
            if (pulled.Finished.Result is { } error && !pulled.Cancelled) ShowProblem("ai_cleanup", $"Download failed: {error}");
            CheckLlm();
            return;
        }

        KeepTicking();
        var model = _ollamaModel.Text.Trim();
        var pull = OllamaPull.Current;
        if (pull is not null && pull.Model == Ollama.FullName(model))
        {
            var (done, total) = pull.Bytes;
            ShowJob(_llmRow,
                total > 0 ? $"{pull.Status} {ModelLibrary.FormatSize(done)} of {ModelLibrary.FormatSize(total)}" : pull.Status,
                total > 0 ? Math.Min(1, done / (double)total) : 0,
                "Cancel", $"Cancel {model} download");
            return;
        }

        var server = Ollama.ServerUrl(_ollamaUrl.Text);
        switch (string.IsNullOrEmpty(model) ? null : _llmState)
        {
            case null when string.IsNullOrEmpty(model):
                ShowJob(_llmRow, "Enter a model name to check for it");
                break;
            case null:
                ShowJob(_llmRow, "Checking…");
                break;
            case Ollama.State.Installed:
                ShowJob(_llmRow, "Installed in Ollama");
                break;
            case Ollama.State.Missing:
                ShowJob(_llmRow, pull is null ? "Not in Ollama yet" : $"Not in Ollama yet · after {pull.Model}",
                    action: "Download", actionName: $"Download {model} with Ollama", accent: true, enabled: pull is null);
                break;
            case Ollama.State.NotRunning:
                ShowJob(_llmRow, $"Can't reach Ollama at {server}. Start Ollama, or install it.",
                    action: "Get Ollama", actionName: "Open the Ollama download page");
                break;
            default:
                ShowJob(_llmRow, "This server can't download models. Add them in LM Studio or on the server itself.");
                break;
        }
    }

    private void LlmAction()
    {
        if (OllamaPull.Current is { } running)
        {
            running.Cancel();
            return;
        }

        if (_llmState == Ollama.State.NotRunning)
        {
            try { Process.Start(new ProcessStartInfo(Ollama.DownloadPage) { UseShellExecute = true }); }
            catch (System.ComponentModel.Win32Exception) { /* No browser registered; the status line names the site. */ }
            return;
        }

        ClearProblem();
        try
        {
            _watchingPull = OllamaPull.Start(_ollamaUrl.Text, _ollamaModel.Text);
        }
        catch (InvalidOperationException error)
        {
            ShowProblem("ai_cleanup", error.Message);
            return;
        }

        ShowLlmStatus();
    }

    private FrameworkElement HistoryPage()
    {
        var recent = HistoryStore.Recent(20);
        if (recent.Count == 0)
        {
            return Page(Group(null, Padded(Text($"Nothing yet. Press {_settings.PrimaryHotkey} to dictate.", 13, "Muted"))));
        }

        var rows = recent.Select(entry =>
        {
            var meta = Text($"{entry.At.ToLocalTime():MMM d, HH:mm} · {entry.Seconds:0.0}s · {entry.Model}", 12, "Muted");
            meta.Margin = new Thickness(0, 2, 0, 0);
            var row = Columns(
                new StackPanel { Children = { Wrapped(Text(entry.Text)), meta } },
                CopyButton("Copy", () => entry.Text));
            row.Margin = new Thickness(18, 10, 12, 10);
            return (FrameworkElement)row;
        });

        return Page(Group("Recent", rows.ToArray()));
    }

    private FrameworkElement GeneralPage()
    {
        var value = Text($"{_settings.CapsuleScale:0.0}×", 13, "Muted");
        value.Width = 36;
        value.TextAlignment = TextAlignment.Right;
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
            VerticalAlignment = VerticalAlignment.Center,
        };
        AutomationProperties.SetName(_scale, "Capsule size");
        _scale.ValueChanged += (_, e) => value.Text = $"{e.NewValue:0.0}×";

        var swatches = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var (hex, name) in Accents)
        {
            var swatch = new RadioButton
            {
                Style = StyleOf("Swatch"),
                GroupName = "accent",
                Background = (Brush)new BrushConverter().ConvertFromString(hex)!,
                IsChecked = string.Equals(hex, _accent, StringComparison.OrdinalIgnoreCase),
                ToolTip = name,
            };
            AutomationProperties.SetName(swatch, name);
            swatch.Checked += (_, _) => _accent = hex;
            swatches.Children.Add(swatch);
        }

        // Shows the unsaved size and accent; closing without saving puts the saved ones back.
        var preview = new Button { Content = "Preview" };
        AutomationProperties.SetName(preview, "Preview capsule");
        preview.Click += (_, _) =>
        {
            _controller.ApplyCapsule(_scale.Value, _accent);
            _controller.PreviewCapsule();
        };

        _micSwitch = new CheckBox { IsChecked = _settings.ShowMicButton };

        var folder = Wrapped(Text(AppPaths.RealPath(AppPaths.Root), 12, "Muted"));
        return Page(
            Group("Microphone button",
                Row("Show microphone button", _micSwitch, "Click it to start and stop dictation. Drag it anywhere.")),
            Group("Capsule overlay",
                Row("Size", new StackPanel { Orientation = Orientation.Horizontal, Children = { _scale, value } }),
                Row("Accent", swatches, "Color of the voice level bars"),
                Row("Position", preview, "Slides down at the top of the screen you're working on")),
            Group("Storage",
                Row("SuperDictate folder", OpenFolderButton(() => AppPaths.Root, "Open the SuperDictate folder"),
                    "Program, speech engine, models, settings and logs", detail: folder)));
    }

    private FrameworkElement SupportPage()
    {
        var intro = Wrapped(Text("SuperDictate is free. If it saves you time, a donation helps keep it improving. Thank you!"));
        var donations = Project.Donations.Select(DonationRow).ToArray();

        _updateRow = NewJob("Update download progress", UpdateAction);
        ShowJob(_updateRow, $"SuperDictate {Setup.Installer.Version}", action: "Check for updates", actionName: "Check for updates");

        // No donation options filled in release-settings.ini: no request for donations.
        return Page(new[]
        {
            donations.Length == 0 ? null : Group("Support SuperDictate", new[] { Padded(intro) }.Concat(donations).ToArray()),
            Group("Updates", JobRow("Version", _updateRow)),
            Group("Help",
                Row("User guide", LinkButton("Open", Project.UserGuide, "Open the user guide")),
                Row("Report a problem", LinkButton("Open", Project.ReportProblem, "Report a problem on GitHub"), "Opens GitHub Issues in your browser"),
                Row("License", ViewButton("License", "LICENSE"), "Free to use, change and share; not for sale"),
                Row("Third-party notices", ViewButton("Third-party notices", "NOTICE.md"))),
        }.OfType<FrameworkElement>().ToArray());
    }

    /// <summary>A link opens in the browser; a wallet address is shown and copied.</summary>
    private FrameworkElement DonationRow(Project.Donation donation)
    {
        if (donation.Kind == Project.DonationKind.Link)
        {
            return Row(donation.Name, LinkButton("Donate", donation.Value, $"Donate with {donation.Name}"), donation.Note);
        }

        var address = Wrapped(Text(donation.Value, 12, "Muted"));
        address.FontFamily = new FontFamily("Cascadia Mono, Consolas");
        var copy = CopyButton($"Copy {donation.Name} address", () => donation.Value);
        copy.VerticalAlignment = VerticalAlignment.Center;
        return Row(donation.Name, copy, donation.Note, detail: address);
    }

    /// <summary>Check, then download and install. The same button cancels whatever is running.</summary>
    private async void UpdateAction()
    {
        if (_updateWork is { } running)
        {
            running.Cancel();
            return;
        }

        var work = _updateWork = new System.Threading.CancellationTokenSource();
        try
        {
            if (_available is null)
            {
                ShowJob(_updateRow, "Checking for updates…", action: "Cancel", actionName: "Cancel update check");
                _available = await Updates.CheckAsync(work.Token);
                if (_available is null)
                {
                    ShowJob(_updateRow, $"SuperDictate {Setup.Installer.Version} is up to date", action: "Check again", actionName: "Check for updates again");
                    return;
                }

                var notes = string.IsNullOrWhiteSpace(_available.Notes) ? "" : $" · {_available.Notes}";
                ShowJob(_updateRow, $"Version {_available.Version} is available{notes}", action: "Download and install", actionName: $"Download and install version {_available.Version}", accent: true);
                return;
            }

            var update = _available;
            ShowJob(_updateRow, $"Downloading version {update.Version}…", 0, "Cancel", "Cancel update download");
            var progress = new Progress<double>(value => ShowJob(_updateRow, $"Downloading version {update.Version}…", value, "Cancel", "Cancel update download"));
            await Updates.DownloadAndRunAsync(update, progress, work.Token);
            ShowJob(_updateRow, "The installer is open. Follow it to finish the update.");
        }
        catch (OperationCanceledException)
        {
            ShowJob(_updateRow, $"SuperDictate {Setup.Installer.Version}", action: _available is null ? "Check for updates" : "Download and install", accent: _available is not null);
        }
        catch (System.Net.Http.HttpRequestException error) when (error.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            ShowJob(_updateRow, "No update information is published yet.", action: "Check again");
        }
        catch (Exception error) when (error is System.Net.Http.HttpRequestException or System.IO.IOException or System.Text.Json.JsonException or System.ComponentModel.Win32Exception)
        {
            AppLogger.Error("Update failed", error);
            ShowJob(_updateRow,
                _available is null ? "Couldn't reach GitHub. Check the internet connection and try again." : $"The update didn't finish: {error.Message}",
                action: _available is null ? "Try again" : "Download and install", accent: _available is not null);
        }
        finally
        {
            if (_updateWork == work) _updateWork = null;
        }
    }

    private static Button LinkButton(string text, string url, string name)
    {
        var button = new Button { Content = text, MinWidth = 88 };
        AutomationProperties.SetName(button, name);
        button.Click += (_, _) =>
        {
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
            catch (System.ComponentModel.Win32Exception) { /* No browser registered; nothing useful to do. */ }
        };
        return button;
    }

    private Button ViewButton(string title, string resource)
    {
        var button = new Button { Content = "View", MinWidth = 88 };
        AutomationProperties.SetName(button, $"View {title.ToLowerInvariant()}");
        button.Click += (_, _) => ShowText(title, System.Text.Encoding.UTF8.GetString(SpeechRuntime.ReadResource(resource)));
        return button;
    }

    /// <summary>A read-only text window in the app's colours, for the licence and notices.</summary>
    private void ShowText(string title, string text)
    {
        var window = new Window
        {
            Title = $"SuperDictate · {title}",
            Icon = AppIcon.Window,
            Owner = this,
            Width = 640,
            Height = 560,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ShowInTaskbar = false,
            Background = Br("Bg"),
        };
        window.Resources.MergedDictionaries.Add(Resources);
        Theme.UseDarkTitleBar(window);
        window.Content = new TextBox
        {
            Text = text,
            IsReadOnly = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            FontFamily = new FontFamily("Cascadia Mono, Consolas"),
            FontSize = 12,
            Padding = new Thickness(14),
            BorderThickness = new Thickness(0),
            Background = Br("Bg"),
        };
        window.ShowDialog();
    }

    // ---------- Live state ----------

    private void OnStateChanged(object? sender, DictationState state) => Dispatcher.InvokeAsync(() => ShowState(state));

    private void OnLevelChanged(object? sender, double level) => Dispatcher.InvokeAsync(() => _level.Value = Math.Min(1.0, level * 6.0));

    private void OnDictationCompleted(object? sender, DictationResultArgs args) => Dispatcher.InvokeAsync(() => ShowResult(args));

    private void ShowState(DictationState state)
    {
        var (text, color) = state switch
        {
            DictationState.Loading => ("Loading model…", "Warn"),
            DictationState.Recording => ("Recording", "Bad"),
            DictationState.Transcribing => ("Transcribing…", "Accent"),
            DictationState.Error => ("Engine error", "Bad"),
            DictationState.NeedsSetup => ("Setup needed", "Warn"),
            _ => ("Ready", "Ok"),
        };
        _statusText.Text = text;
        if (_compact == true) _statusBox.ToolTip = text;
        _statusDot.Fill = Br(color);
        _engineText.Text = state == DictationState.NeedsSetup ? "Install the runtime and a model" : _controller.EngineSummary;

        var recording = state == DictationState.Recording;
        _record.Background = _record.BorderBrush = Br(recording ? "BadFill" : "Accent");
        _recordGlyph.Text = recording ? "\uE71A" : "\uE720";
        _recordText.Text = recording ? "Stop" : state == DictationState.Transcribing ? "Working…" : "Dictate";
        AutomationProperties.SetName(_record, _recordText.Text);
        _record.IsEnabled = state is DictationState.Ready or DictationState.Recording or DictationState.Error;
        if (!recording) _level.Value = 0;
    }

    private void ShowResult(DictationResultArgs args)
    {
        var ok = !string.IsNullOrEmpty(args.Text);
        if (ok) _lastTranscript = args.Text;
        _copyLast.IsEnabled = _lastTranscript is not null;
        _lastMeta.Text = ok
            ? $"{DateTime.Now:HH:mm:ss} · {args.DurationSeconds:0.0}s · {args.Model}"
            : $"{DateTime.Now:HH:mm:ss} · nothing pasted";
        _lastText.Text = ok ? args.Text : args.ErrorMessage ?? "No speech recognized.";
        _lastText.Foreground = Br(ok ? "Text" : "Warn");
    }

    private void SaveAndApply()
    {
        ClearProblem();

        var selected = _languages.Where(pair => pair.Value.IsChecked == true).Select(pair => pair.Key).ToList();
        if (selected.Count == 0)
        {
            ShowProblem("languages", "Select at least one language to recognize.");
            return;
        }

        // An unparseable or clashing chord would silently unbind dictation, so it never saves.
        if (HotkeyProblem(_primaryHotkey, out var primary) is { } primaryProblem)
        {
            ShowProblem("dictation", primaryProblem, _primaryHotkey);
            return;
        }

        if (HotkeyProblem(_altHotkey, out var alternate) is { } alternateProblem)
        {
            ShowProblem("dictation", alternateProblem, _altHotkey);
            return;
        }

        if (SameChord(primary!, alternate!))
        {
            ShowProblem("dictation", "The dictation and alternate hotkeys are the same. Choose a different alternate hotkey.", _altHotkey);
            return;
        }

        if (Hotkey.TryParse(_settings.HistoryHotkey, out var history))
        {
            foreach (var (chord, box, name) in new[] { (primary!, _primaryHotkey, "dictation"), (alternate!, _altHotkey, "alternate") })
            {
                if (SameChord(chord, history!))
                {
                    ShowProblem("dictation", $"{_settings.HistoryHotkey} already opens history. Choose a different {name} hotkey.", box);
                    return;
                }
            }
        }

        // The key goes to Credential Manager, never settings.json. Store it first so a
        // failure keeps the window open with nothing half-applied.
        var cloudKey = _cloudKey.Password.Trim();
        if (cloudKey != _savedCloudKey)
        {
            try
            {
                if (cloudKey.Length == 0) CredentialStore.Delete(CredentialStore.AiCleanupTarget);
                else CredentialStore.Write(CredentialStore.AiCleanupTarget, cloudKey);
                _savedCloudKey = cloudKey;
            }
            catch (System.ComponentModel.Win32Exception error)
            {
                AppLogger.Error("Could not store the AI cleanup key", error);
                ShowProblem("ai_cleanup", $"Couldn't save the API key to Windows Credential Manager: {error.Message} Nothing was saved.", _cloudKey);
                return;
            }
        }

        // Reloading the engine takes seconds, so only a change to what it recognizes restarts it.
        var engineChanged = _settings.ModelId != (Selected(_model) ?? _settings.ModelId)
                            || _settings.Language != (Selected(_langMode) ?? "auto")
                            || !(_settings.SelectedLanguages ?? new List<string>()).OrderBy(code => code).SequenceEqual(selected.OrderBy(code => code));

        _settings.SelectedLanguages = selected;
        _settings.Language = Selected(_langMode) ?? "auto";
        _settings.ModelId = Selected(_model) ?? _settings.ModelId;
        _settings.PrimaryHotkey = primary!.Spec.Trim();
        _settings.AlternateHotkey = alternate!.Spec.Trim();
        _settings.PressAndHold = _pressAndHold.IsChecked == true;
        _settings.PressEnterAfterPaste = _pressEnter.IsChecked == true;
        _settings.MicrophoneId = Selected(_mic) is { Length: > 0 } micId ? micId : null;

        _settings.AiCleanupEnabled = _aiEnable.IsChecked == true;
        _settings.AiCleanupMode = Selected(_aiMode) ?? "local_smart";
        _settings.AiRemoveFillers = _aiFillers.IsChecked == true;
        _settings.AiFormatPunctuation = _aiPunct.IsChecked == true;
        _settings.AiRemoveDuplicates = _aiDedupe.IsChecked == true;
        _settings.CustomFillerWords = _customFillers.Text.Trim();
        _settings.LocalLlmEndpoint = _ollamaUrl.Text.Trim();
        _settings.LocalLlmModel = _ollamaModel.Text.Trim();
        _settings.AiBaseUrl = _cloudUrl.Text.Trim();
        _settings.AiModel = _cloudModel.Text.Trim();

        _settings.ShowMicButton = _micSwitch.IsChecked == true;
        _settings.CapsuleScale = _scale.Value;
        _settings.CapsuleAccent = _accent;

        SettingsStore.Save(_settings);
        _controller.ApplySettings(_settings, restartEngine: engineChanged);
        ShowModelStatus();
        ShowSaved();
    }

    /// <summary>The window stays open after Save; the button confirms for a moment instead.</summary>
    private void ShowSaved()
    {
        _save.Content = new StackPanel { Orientation = Orientation.Horizontal, Children = { Glyph("\uE73E", 12, new Thickness(0, 0, 8, 0)), new TextBlock { Text = "Saved" } } };
        AutomationProperties.SetName(_save, "Saved");
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.8) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            _save.Content = "Save";
            AutomationProperties.SetName(_save, "Save");
        };
        timer.Start();
    }

    /// <summary>A hotkey text box plus a button that records the next chord pressed.</summary>
    private FrameworkElement HotkeyField(TextBox box, string label)
    {
        AutomationProperties.SetName(box, label);
        var name = $"Record {label.ToLowerInvariant()}";
        Button record = null!;
        record = IconButton("\uE765", name, () =>
        {
            if (_recordingBox == box) StopRecording();
            else StartRecording(box, record);
        }, size: 15);
        record.Tag = name;
        record.Margin = new Thickness(4, 0, 0, 0);
        record.VerticalAlignment = VerticalAlignment.Center;

        box.PreviewKeyDown += (_, e) => OnRecordKey(box, e, down: true);
        box.PreviewKeyUp += (_, e) => OnRecordKey(box, e, down: false);
        // Clicking or tabbing elsewhere cancels; the record button itself toggles instead.
        box.LostKeyboardFocus += (_, e) =>
        {
            if (_recordingBox == box && e.NewFocus != record) StopRecording();
        };
        return new StackPanel { Orientation = Orientation.Horizontal, Children = { box, record } };
    }

    private void StartRecording(TextBox box, Button button)
    {
        StopRecording();
        _recordingBox = box;
        _recordingButton = button;
        _textBeforeRecording = box.Text;
        _keysDown.Clear();
        _chordKeys.Clear();

        // Otherwise pressing the current hotkey here would start dictation.
        _controller.HotkeysSuspended = true;
        box.IsReadOnly = true;
        box.Text = "Press keys…";
        button.Content = "\uE711";
        button.ToolTip = "Cancel recording";
        AutomationProperties.SetName(button, "Cancel recording");
        box.Focus();
    }

    /// <summary>Ends recording, keeping the recorded chord or restoring the previous text.</summary>
    private void StopRecording(string? spec = null)
    {
        if (_recordingBox is not { } box) return;
        _recordingBox = null;
        _controller.HotkeysSuspended = false;
        box.IsReadOnly = false;
        box.Text = spec ?? _textBeforeRecording;
        box.CaretIndex = box.Text.Length;

        if (_recordingButton is { Tag: string name } button)
        {
            button.Content = "\uE765";
            button.ToolTip = name;
            AutomationProperties.SetName(button, name);
        }

        _recordingButton = null;
    }

    private void OnRecordKey(TextBox box, KeyEventArgs e, bool down)
    {
        if (_recordingBox != box) return;

        // Tab, Alt and Enter are chord keys here, not navigation or menu keys.
        e.Handled = true;
        var key = e.Key switch
        {
            Key.System => e.SystemKey,
            Key.ImeProcessed => e.ImeProcessedKey,
            Key.DeadCharProcessed => e.DeadCharProcessedKey,
            _ => e.Key,
        };

        if (down)
        {
            if (e.IsRepeat) return;
            if (key == Key.Escape && _chordKeys.Count == 0)
            {
                StopRecording();
                return;
            }

            _keysDown.Add(key);
            if (!_chordKeys.Contains(key)) _chordKeys.Add(key);
            box.Text = string.Join("+", InChordOrder(_chordKeys).Select(KeyName));
            return;
        }

        // The chord is complete once every key is up. A key-up with nothing recorded
        // belongs to a press from before recording started.
        _keysDown.Remove(key);
        if (_keysDown.Count > 0 || _chordKeys.Count == 0) return;

        var problem = ChordFromKeys(_chordKeys, out var spec);
        StopRecording(spec);
        if (problem is not null) ShowProblem("dictation", problem, box);
    }

    private static readonly Key[] ModifierOrder =
        { Key.LeftCtrl, Key.RightCtrl, Key.LeftShift, Key.RightShift, Key.LeftAlt, Key.RightAlt, Key.LWin, Key.RWin };

    /// <summary>
    /// Turns recorded keys into a hotkey spec, modifiers first (Ctrl, Shift, Alt, Win).
    /// Returns what to tell the user when the keys can't form a hotkey.
    /// </summary>
    internal static string? ChordFromKeys(IReadOnlyCollection<Key> keys, out string? spec)
    {
        spec = null;
        var names = InChordOrder(keys).Select(KeyName).ToList();
        if (names.FirstOrDefault(name => !Hotkey.TryParse(name, out _)) is { } unsupported)
        {
            return $"{unsupported} can't be part of a hotkey. Use modifiers with a letter, digit, F-key, Space, Enter, Tab, Escape, CapsLock, Insert or Delete.";
        }

        var candidate = string.Join("+", names);
        if (!Hotkey.TryParse(candidate, out _))
        {
            return "A hotkey can have only one key besides modifiers.";
        }

        spec = candidate;
        return null;
    }

    private static IEnumerable<Key> InChordOrder(IEnumerable<Key> keys) =>
        keys.OrderBy(key => Array.IndexOf(ModifierOrder, key) is var index and >= 0 ? index : ModifierOrder.Length);

    private static string KeyName(Key key) => key switch
    {
        Key.LWin => "LeftWin",
        Key.RWin => "RightWin",
        Key.Return => "Enter",
        Key.Capital => "CapsLock",
        _ => key.ToString(),
    };

    /// <summary>Opens the page with the problem, explains it inline and outlines the field.</summary>
    private void ShowProblem(string page, string message, Control? field = null, bool error = true)
    {
        _navItems[page].IsChecked = true;
        _problemPage = page;
        _noticeText.Text = message;
        _noticeIcon.Text = error ? "\uE783" : "\uE946";
        _noticeIcon.Foreground = Br(error ? "Bad" : "Link");
        _noticeBox.Background = Br(error ? "BadSoft" : "AccentSoft");
        _notice.Visibility = Visibility.Visible;
        (UIElementAutomationPeer.FromElement(_noticeText) ?? UIElementAutomationPeer.CreatePeerForElement(_noticeText))
            ?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);

        if (field is null) return;
        _flagged = field;
        field.BorderBrush = Br("Bad");
        field.Focus();
    }

    /// <summary>Hides the problem; with a page, only if the problem belongs to that page.</summary>
    private void ClearProblem(string? page = null)
    {
        if (_problemPage is null || (page is not null && page != _problemPage)) return;
        _notice.Visibility = Visibility.Collapsed;
        _flagged?.ClearValue(Control.BorderBrushProperty);
        _flagged = null;
        _problemPage = null;
    }

    /// <summary>Null when the box holds a chord the keyboard hook can bind; otherwise what to tell the user.</summary>
    private static string? HotkeyProblem(TextBox box, out Hotkey? hotkey)
    {
        hotkey = null;
        var spec = box.Text.Trim();
        if (spec.Length == 0) return "Enter a hotkey, like RightAlt.";
        return Hotkey.TryParse(spec, out hotkey)
            ? null
            : $"SuperDictate doesn't recognize “{spec}” as a hotkey. Use key names joined by +, like RightAlt or RightCtrl+Space.";
    }

    private static bool SameChord(Hotkey a, Hotkey b) => a.Key == b.Key && a.Modifiers.SetEquals(b.Modifiers);

    // ---------- Building blocks ----------

    private Brush Br(string key) => (Brush)Resources[key];

    private Style StyleOf(string key) => (Style)Resources[key];

    /// <summary>Content column width: wide windows center it instead of stretching rows across.</summary>
    private const double PageWidth = 760;

    private static FrameworkElement Page(params FrameworkElement[] groups)
    {
        var stack = new StackPanel { Margin = new Thickness(28, 0, 28, 20), MaxWidth = PageWidth };
        foreach (var group in groups) stack.Children.Add(group);
        // Not a tab stop itself: focusing a control inside scrolls it into view.
        return new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Focusable = false, Content = stack };
    }

    /// <summary>A rounded white card with its title in blue, rows split by inset hairlines, as in Telegram's settings.</summary>
    private FrameworkElement Group(string? title, params FrameworkElement[] rows)
    {
        var body = new StackPanel { Margin = new Thickness(0, title is null ? 6 : 0, 0, 6) };
        if (title is not null)
        {
            var caption = Text(title, 13.5, "Link", bold: true);
            caption.Margin = new Thickness(18, 16, 18, 4);
            AutomationProperties.SetHeadingLevel(caption, AutomationHeadingLevel.Level2);
            body.Children.Add(caption);
        }

        var first = true;
        foreach (var row in rows)
        {
            if (!first) body.Children.Add(new Border { Height = 1, Margin = new Thickness(18, 0, 0, 0), Background = Br("Stroke") });
            body.Children.Add(row);
            first = false;
        }

        return new Border
        {
            Margin = new Thickness(0, 0, 0, 14),
            Background = Br("Card"),
            CornerRadius = new CornerRadius(12),
            Child = body,
        };
    }

    /// <param name="detail">Live content under the label, when a fixed hint isn't enough.</param>
    private FrameworkElement Row(string label, FrameworkElement control, string? hint = null, FrameworkElement? detail = null)
    {
        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 16, 0), Children = { Text(label, 13.5) } };
        if (hint is not null)
        {
            var hintText = Wrapped(Text(hint, size: 12, brush: "Muted"));
            hintText.Margin = new Thickness(0, 2, 0, 0);
            text.Children.Add(hintText);
        }

        if (detail is not null) text.Children.Add(detail);

        // Screen readers announce the row's label and hint with its control.
        if (control is Control named && string.IsNullOrEmpty(AutomationProperties.GetName(named)))
        {
            AutomationProperties.SetName(named, label);
            if (hint is not null) AutomationProperties.SetHelpText(named, hint);
        }

        control.VerticalAlignment = VerticalAlignment.Center;
        var slot = new Border { Child = control, VerticalAlignment = VerticalAlignment.Center };
        var row = Columns(text, slot);
        row.Margin = new Thickness(18, 11, 18, 11);
        row.MinHeight = 34;
        row.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        row.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        // Narrow rows put the control under its label instead of squeezing the label.
        row.SizeChanged += (_, e) =>
        {
            var stacked = e.NewSize.Width < 420;
            if ((Grid.GetRow(slot) == 1) == stacked) return;
            Grid.SetRow(slot, stacked ? 1 : 0);
            Grid.SetColumn(slot, stacked ? 0 : 1);
            Grid.SetColumnSpan(slot, stacked ? 2 : 1);
            slot.HorizontalAlignment = stacked ? HorizontalAlignment.Left : HorizontalAlignment.Stretch;
            slot.Margin = new Thickness(0, stacked ? 10 : 0, 0, 0);
        };
        return row;
    }

    private static Border Padded(UIElement child) => new() { Padding = new Thickness(18, 14, 18, 14), Child = child };

    /// <summary>Left fills, right sizes to content.</summary>
    private static Grid Columns(UIElement left, UIElement right)
    {
        Grid.SetColumn(right, 1);
        return new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition { Width = GridLength.Auto } },
            Children = { left, right },
        };
    }

    private TextBlock Text(string text, double size = 13, string brush = "Text", bool bold = false) => new()
    {
        Text = text,
        FontSize = size,
        Foreground = Br(brush),
        FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal,
        TextTrimming = TextTrimming.CharacterEllipsis,
        VerticalAlignment = VerticalAlignment.Center,
    };

    private static TextBlock Wrapped(TextBlock text)
    {
        text.TextWrapping = TextWrapping.Wrap;
        return text;
    }

    private static TextBlock Glyph(string glyph, double size, Thickness margin = default) => new()
    {
        Text = glyph,
        FontFamily = IconFont,
        FontSize = size,
        Margin = margin,
        VerticalAlignment = VerticalAlignment.Center,
    };

    private Button IconButton(string glyph, string name, Action onClick, string style = "GhostButton", double size = 13)
    {
        var button = new Button { Content = glyph, Style = StyleOf(style), FontSize = size, ToolTip = name, VerticalAlignment = VerticalAlignment.Top };
        AutomationProperties.SetName(button, name);
        button.Click += (_, _) => onClick();
        return button;
    }

    /// <summary>Copies on click and shows a check mark for a moment when it worked.</summary>
    private Button CopyButton(string name, Func<string?> text)
    {
        Button button = null!;
        button = IconButton("\uE8C8", name, () =>
        {
            if (!Copy(text())) return;
            button.Content = "\uE73E";
            button.ToolTip = "Copied";
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                button.Content = "\uE8C8";
                button.ToolTip = name;
            };
            timer.Start();
        });
        return button;
    }

    private static Button OpenFolderButton(Func<string> path, string name)
    {
        var button = new Button { Content = "Open" };
        AutomationProperties.SetName(button, name);
        button.Click += (_, _) =>
        {
            try { Process.Start(new ProcessStartInfo("explorer.exe", AppPaths.RealPath(path())) { UseShellExecute = true }); }
            catch { /* Explorer unavailable: nothing useful to do. */ }
        };
        return button;
    }

    private static TextBox Input(string? text, double width) => new() { Text = text ?? "", Width = width };

    private static ComboBox Combo(string? current, double width, params (string Tag, string Text)[] items)
    {
        var combo = new ComboBox { Width = width };
        foreach (var (tag, text) in items) combo.Items.Add(new ComboBoxItem { Content = text, Tag = tag });
        combo.SelectedItem = combo.Items.Cast<ComboBoxItem>()
            .FirstOrDefault(item => string.Equals((string)item.Tag, current, StringComparison.OrdinalIgnoreCase))
            ?? combo.Items[0];
        return combo;
    }

    private static string? Selected(ComboBox combo) => (combo.SelectedItem as ComboBoxItem)?.Tag as string;

    private static bool Copy(string? text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        try
        {
            Clipboard.SetText(text);
            return true;
        }
        catch
        {
            // Clipboard held by another process; no check mark, so the user can retry.
            return false;
        }
    }

    private static FrameworkElement Logo(double size)
    {
        var logo = new Image { Source = AppIcon.Large, Width = size, Height = size, VerticalAlignment = VerticalAlignment.Center };
        RenderOptions.SetBitmapScalingMode(logo, BitmapScalingMode.HighQuality);
        return logo;
    }
}
