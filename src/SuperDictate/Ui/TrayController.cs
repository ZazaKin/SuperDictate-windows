using System;
using System.Drawing;
using System.Linq;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using SuperDictate.Speech;
using SuperDictate.Storage;

namespace SuperDictate.Ui;

/// <summary>
/// Windows counterpart of the macOS menu bar item. The panel can be closed
/// while the dictation engine keeps running in the same process.
/// </summary>
public sealed class TrayController : IDisposable
{
    private readonly NotifyIcon _icon;
    private readonly ContextMenuStrip _menu;
    private readonly ToolStripMenuItem _status;
    private readonly ToolStripMenuItem _dictate;
    private readonly DictationController _controller;
    private readonly Settings _settings;
    private readonly Timer _clickTimer;
    private SettingsWindow? _settingsWin;

    private readonly ToolStripMenuItem _turboItem;
    private readonly ToolStripMenuItem _smallItem;
    private readonly ToolStripMenuItem _baseItem;
    private readonly ToolStripMenuItem _langMenu;
    private readonly ToolStripMenuItem _aiItem;

    public TrayController(DictationController controller, Settings settings)
    {
        _controller = controller;
        _settings = settings;

        _status = new ToolStripMenuItem("Starting…") { Enabled = false };

        _menu = new ContextMenuStrip();
        _menu.Items.Add(_status);
        _menu.Items.Add(new ToolStripSeparator());

        // Names match the settings window.
        var modelMenu = new ToolStripMenuItem("Speech model");
        _turboItem = new ToolStripMenuItem("Large v3 Turbo (recommended)") { Checked = settings.ModelId is "whisper-large-v3-turbo" or "parakeet_tdt_v3" or "" or null };
        _smallItem = new ToolStripMenuItem("Small (balanced)") { Checked = settings.ModelId == "whisper-small" };
        _baseItem = new ToolStripMenuItem("Base (fastest)") { Checked = settings.ModelId == "whisper-base" };

        void SetModel(string modelId)
        {
            settings.ModelId = modelId;
            SettingsStore.Save(settings);
            _turboItem.Checked = modelId == "whisper-large-v3-turbo";
            _smallItem.Checked = modelId == "whisper-small";
            _baseItem.Checked = modelId == "whisper-base";
            _controller.RestartEngine();

            // Until it is downloaded the engine keeps the best model on disk; Settings
            // opens where the Download button for it is.
            if (!ModelLibrary.IsPresent(AppPaths.Models, modelId)) OpenSettingsWindow("models");
        }

        void LabelModels()
        {
            foreach (var (item, id) in new[] { (_turboItem, "whisper-large-v3-turbo"), (_smallItem, "whisper-small"), (_baseItem, "whisper-base") })
            {
                item.Text = ModelLibrary.Find(id).Name + (ModelLibrary.IsPresent(AppPaths.Models, id) ? "" : " (not downloaded)");
            }
        }

        _turboItem.Click += (_, _) => SetModel("whisper-large-v3-turbo");
        _smallItem.Click += (_, _) => SetModel("whisper-small");
        _baseItem.Click += (_, _) => SetModel("whisper-base");

        modelMenu.DropDownItems.Add(_turboItem);
        modelMenu.DropDownItems.Add(_smallItem);
        modelMenu.DropDownItems.Add(_baseItem);

        // Filled in each time the menu opens, from the languages chosen in settings.
        _langMenu = new ToolStripMenuItem("Language");

        // Built-in rules are the default engine, so this is not always an LLM.
        _aiItem = new ToolStripMenuItem("AI cleanup") { Checked = settings.AiCleanupEnabled };
        _aiItem.Click += (_, _) =>
        {
            settings.AiCleanupEnabled = !settings.AiCleanupEnabled;
            _aiItem.Checked = settings.AiCleanupEnabled;
            SettingsStore.Save(settings);
        };


        _dictate = new ToolStripMenuItem("Dictate", null, (_, _) => _controller.Toggle(settings.PressEnterAfterPaste));

        _menu.Items.Add("Settings…", null, (_, _) => OpenSettingsWindow());
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(_dictate);
        var micItem = new ToolStripMenuItem("Microphone button") { Checked = settings.ShowMicButton };
        micItem.Click += (_, _) =>
        {
            settings.ShowMicButton = !settings.ShowMicButton;
            micItem.Checked = settings.ShowMicButton;
            SettingsStore.Save(settings);
            _controller.ShowMicButton(settings.ShowMicButton);
        };
        _menu.Items.Add(micItem);
        _menu.Items.Add(modelMenu);
        _menu.Items.Add(_langMenu);
        _menu.Items.Add(_aiItem);
        // The history hotkey does the same: the latest transcript goes to the clipboard.
        _menu.Items.Add("Copy last transcript", null, (_, _) => _controller.ShowHistory());
        _menu.Items.Add("Restart engine", null, (_, _) => _controller.RestartEngine());
        _menu.Items.Add("Support and updates…", null, (_, _) => OpenSettingsWindow("support"));
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add("Quit", null, (_, _) => System.Windows.Application.Current.Shutdown());

        // Follow the Windows app mode each time the menu opens.
        _menu.Opening += (_, _) =>
        {
            RefreshChecks();
            LabelModels();
            micItem.Checked = settings.ShowMicButton; // Settings may have changed it.
        };
        ApplyMenuTheme(modelMenu, _langMenu);

        _icon = new NotifyIcon
        {
            Icon = AppIcon.Tray(),
            Text = "SuperDictate",
            Visible = settings.ShowTrayIcon,
            ContextMenuStrip = _menu,
        };

        // A double-click opens Settings, so a single click waits out the double-click
        // time before toggling dictation; otherwise a double-click would also start recording.
        _clickTimer = new Timer { Interval = SystemInformation.DoubleClickTime };
        _clickTimer.Tick += (_, _) =>
        {
            _clickTimer.Stop();
            _controller.Toggle(settings.PressEnterAfterPaste);
        };
        _icon.MouseClick += (_, args) =>
        {
            if (args.Button == MouseButtons.Left) _clickTimer.Start();
        };
        _icon.MouseDoubleClick += (_, args) =>
        {
            if (args.Button != MouseButtons.Left) return;
            _clickTimer.Stop();
            OpenSettingsWindow();
        };

        _controller.StateChanged += OnStateChanged;
        _controller.SetupNeeded += (_, _) => OpenSettingsWindow("models");
        OnStateChanged(this, _controller.State);
    }

    /// <param name="page">Page to open on when the window isn't open yet.</param>
    public void OpenSettingsWindow(string page = "languages")
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            if (_settingsWin is { IsVisible: true })
            {
                if (_settingsWin.WindowState == System.Windows.WindowState.Minimized)
                {
                    _settingsWin.WindowState = System.Windows.WindowState.Normal;
                }
                _settingsWin.Activate();
                return;
            }
            _settingsWin = new SettingsWindow(_controller, _settings, page);
            _settingsWin.Closed += (_, _) => _settingsWin = null;
            _settingsWin.Show();
            _settingsWin.Activate();
        });
    }

    private void OnStateChanged(object? sender, DictationState state)
    {
        // Same wording as the settings window status.
        var text = state switch
        {
            DictationState.Loading => "Loading model…",
            DictationState.Recording => "Recording",
            DictationState.Transcribing => "Transcribing…",
            DictationState.Error => "Engine error. Try Restart engine.",
            DictationState.NeedsSetup => "Finish setup: Settings › Speech model",
            _ => "Ready",
        };

        _status.Text = text;
        _icon.Text = "SuperDictate: " + text;
        _dictate.Text = state == DictationState.Recording ? "Stop dictation" : "Dictate";
        _dictate.Enabled = state is DictationState.Ready or DictationState.Recording or DictationState.Error;
    }

    private void ApplyMenuTheme(params ToolStripMenuItem[] submenus)
    {
        var renderer = new MenuRenderer();
        _menu.Renderer = renderer;
        foreach (var submenu in submenus)
        {
            submenu.DropDown.Renderer = renderer;
        }
    }

    /// <summary>Checkmarks follow the settings, which the settings window changes without closing.</summary>
    private void RefreshChecks()
    {
        // Automatic, or one of the languages chosen in settings.
        _langMenu.DropDownItems.Clear();
        var current = string.IsNullOrEmpty(_settings.Language) ? "auto" : _settings.Language;
        var choices = new[] { ("auto", "Auto-detect") }
            .Concat(SpokenLanguages.All
                .Where(language => _settings.SelectedLanguages.Contains(language.Code))
                .Select(language => (language.Code, $"{language.English} only")));
        foreach (var (code, text) in choices)
        {
            var item = new ToolStripMenuItem(text) { Checked = current == code };
            item.Click += (_, _) =>
            {
                _settings.Language = code;
                SettingsStore.Save(_settings);
                _controller.RestartEngine();
            };
            _langMenu.DropDownItems.Add(item);
        }

        _langMenu.DropDownItems.Add(new ToolStripSeparator());
        _langMenu.DropDownItems.Add("Choose languages…", null, (_, _) => OpenSettingsWindow("languages"));
        _turboItem.Checked = _settings.ModelId == "whisper-large-v3-turbo";
        _smallItem.Checked = _settings.ModelId == "whisper-small";
        _baseItem.Checked = _settings.ModelId == "whisper-base";
        _aiItem.Checked = _settings.AiCleanupEnabled;
    }

    public void Dispose()
    {
        _controller.StateChanged -= OnStateChanged;
        _clickTimer.Dispose();
        _icon.Visible = false;
        _icon.Dispose();
    }

    /// <summary>The tray menu in the settings window palette.</summary>
    private sealed class MenuRenderer : ToolStripProfessionalRenderer
    {
        private readonly Color _text = Pick("Text");
        private readonly Color _muted = Pick("Muted");

        public MenuRenderer()
            : base(new MenuColors())
        {
            RoundedEdges = false;
        }

        public static Color Pick(string key) => ColorTranslator.FromHtml(Theme.Color(key));

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? _text : _muted;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = _text;
            base.OnRenderArrow(e);
        }

        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            // The stock check glyph is black, which disappears on the dark menu.
            var box = e.ImageRectangle;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var pen = new Pen(_text, 1.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
            e.Graphics.DrawLines(pen, new[]
            {
                new PointF(box.Left + (box.Width * 0.22f), box.Top + (box.Height * 0.52f)),
                new PointF(box.Left + (box.Width * 0.42f), box.Top + (box.Height * 0.72f)),
                new PointF(box.Left + (box.Width * 0.78f), box.Top + (box.Height * 0.30f)),
            });
        }
    }

    private sealed class MenuColors : ProfessionalColorTable
    {
        public MenuColors()
        {
            UseSystemColors = false;
        }

        public override Color ToolStripDropDownBackground => C("Raised");
        public override Color ImageMarginGradientBegin => C("Raised");
        public override Color ImageMarginGradientMiddle => C("Raised");
        public override Color ImageMarginGradientEnd => C("Raised");
        public override Color MenuBorder => C("StrokeHover");
        public override Color MenuItemBorder => C("AccentSoft");
        public override Color MenuItemSelected => C("AccentSoft");
        public override Color MenuItemSelectedGradientBegin => C("AccentSoft");
        public override Color MenuItemSelectedGradientEnd => C("AccentSoft");
        public override Color MenuItemPressedGradientBegin => C("AccentSoft");
        public override Color MenuItemPressedGradientEnd => C("AccentSoft");
        public override Color SeparatorDark => C("Stroke");
        public override Color SeparatorLight => C("Raised");
        public override Color CheckBackground => C("Raised");
        public override Color CheckSelectedBackground => C("AccentSoft");
        public override Color CheckPressedBackground => C("AccentSoft");

        private static Color C(string key) => MenuRenderer.Pick(key);
    }
}
