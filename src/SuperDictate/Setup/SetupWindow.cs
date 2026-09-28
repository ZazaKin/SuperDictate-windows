using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using SuperDictate.Storage;
using SuperDictate.Ui;

namespace SuperDictate.Setup;

/// <summary>
/// What customers see when they run the downloaded exe (install or update), or
/// uninstall from Apps and features. Same look as the settings window.
/// </summary>
public sealed class SetupWindow : Window
{
    private readonly bool _uninstall;
    private readonly bool _update;
    private readonly TextBlock _status;
    private readonly TextBlock _location;
    private readonly Button _change;
    private readonly Button _go;
    private readonly Button _cancel;
    private readonly CheckBox _option;
    private string _root;

    public SetupWindow(bool uninstall)
    {
        _uninstall = uninstall;
        Resources = Theme.Create();
        Theme.UseDarkTitleBar(this);
        Title = uninstall ? "Uninstall SuperDictate" : "Install SuperDictate";
        Icon = AppIcon.Window;
        Width = 520;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ResizeMode = ResizeMode.NoResize;
        FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI");
        FontSize = 13;
        Background = Br("Card");
        Foreground = Br("Text");

        var installed = Installer.InstalledVersion;
        _update = installed is not null;
        _root = Installer.SuggestedRoot();
        var heading = uninstall ? "Uninstall SuperDictate"
            : installed is null ? "Install SuperDictate"
            : installed == Installer.Version ? "Reinstall SuperDictate"
            : $"Update SuperDictate from {installed}";

        var body = uninstall
            ? "Removes SuperDictate from this PC. Your settings, history and downloaded models stay unless you choose to delete them."
            : $"Speech to text for any app. Your voice is transcribed on this PC.\n\nInstalls for {Environment.UserName}. No administrator rights needed. Everything SuperDictate keeps goes into the folder below: the program, its speech engine, models, settings and logs. After installing, it walks you through a one-time download of the speech engine and a model.";

        // Where the program goes. An update stays where the installed copy is.
        _location = new TextBlock { Text = _root, TextWrapping = TextWrapping.Wrap, Foreground = Br("Muted"), FontSize = 12, Margin = new Thickness(0, 3, 12, 0) };
        _change = new Button { Content = "Change…", MinWidth = 96, VerticalAlignment = VerticalAlignment.Center, IsEnabled = !_update };
        AutomationProperties.SetName(_change, "Change install folder");
        _change.Click += (_, _) => ChooseFolder();
        var where = new Grid
        {
            Margin = new Thickness(0, 20, 0, 0),
            ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition { Width = GridLength.Auto } },
            Children =
            {
                new StackPanel
                {
                    Children =
                    {
                        new TextBlock { Text = _update ? "Updates the copy in" : "Install to", FontWeight = FontWeights.SemiBold },
                        _location,
                    },
                },
                _change,
            },
        };
        Grid.SetColumn(_change, 1);

        _option = new CheckBox
        {
            IsChecked = !uninstall,
            Content = new TextBlock { Text = uninstall ? "Also delete my settings, history and downloaded models" : "Start SuperDictate when I sign in", TextWrapping = TextWrapping.Wrap },
            Margin = new Thickness(0, 14, 0, 0),
        };

        _status = new TextBlock { Foreground = Br("Muted"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 0), MinHeight = 16 };
        _go = new Button { Content = uninstall ? "Uninstall" : installed is null ? "Install" : "Update", Style = (Style)Resources["AccentButton"], MinWidth = 112, Height = 38, Margin = new Thickness(0, 0, 8, 0) };
        _cancel = new Button { Content = "Cancel", MinWidth = 96, Height = 38 };
        _go.Click += async (_, _) => await RunAsync();
        _cancel.Click += (_, _) => Close();

        var content = new StackPanel
        {
            Margin = new Thickness(32, 28, 32, 24),
            Children =
            {
                Logo(),
                new TextBlock { Text = heading, FontSize = 22, FontWeight = FontWeights.SemiBold },
                new TextBlock { Text = $"Version {Installer.Version}", Foreground = Br("Muted"), Margin = new Thickness(0, 2, 0, 14) },
                new TextBlock { Text = body, TextWrapping = TextWrapping.Wrap },
            },
        };
        if (!uninstall) content.Children.Add(where);
        content.Children.Add(_option);
        content.Children.Add(_status);
        content.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 18, 0, 0), Children = { _go, _cancel } });
        Content = content;

        if (!uninstall) ShowTargetProblem();
    }

    private Brush Br(string key) => (Brush)Resources[key];

    private static Image Logo()
    {
        var logo = new Image { Source = AppIcon.Large, Width = 56, Height = 56, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 16) };
        RenderOptions.SetBitmapScalingMode(logo, BitmapScalingMode.HighQuality);
        return logo;
    }

    /// <summary>A picked folder gets a SuperDictate subfolder, the way installers usually do.</summary>
    private void ChooseFolder()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Choose where to install SuperDictate",
            InitialDirectory = Directory.Exists(Path.GetDirectoryName(_root)) ? Path.GetDirectoryName(_root) : null,
        };
        if (dialog.ShowDialog(this) != true) return;

        var picked = dialog.FolderName.TrimEnd(Path.DirectorySeparatorChar);
        _root = Path.GetFileName(picked).Equals("SuperDictate", StringComparison.OrdinalIgnoreCase)
            ? picked
            : Path.Combine(picked, "SuperDictate");
        _location.Text = _root;
        ShowTargetProblem();
    }

    /// <summary>Checks the folder before anything is copied; Install stays off until it is usable.</summary>
    private void ShowTargetProblem()
    {
        var problem = Installer.CheckTarget(_root);
        _status.Foreground = Br(problem is null ? "Muted" : "Bad");
        _status.Text = problem ?? "";
        _go.IsEnabled = problem is null;
    }

    private async Task RunAsync()
    {
        _go.IsEnabled = _cancel.IsEnabled = _option.IsEnabled = _change.IsEnabled = false;
        _status.Foreground = Br("Muted");
        var option = _option.IsChecked == true;
        var root = _root;
        void Report(string text) => Dispatcher.Invoke(() => _status.Text = text);

        try
        {
            if (_uninstall)
            {
                await Task.Run(() => Installer.Uninstall(option, Report));
                _status.Text = "SuperDictate has been removed.";
                _go.Visibility = Visibility.Collapsed;
                _cancel.Content = "Close";
                _cancel.IsEnabled = true;
                return;
            }

            var installed = await Task.Run(() => Installer.Install(root, option, Report));
            Process.Start(new ProcessStartInfo(installed, "--show") { UseShellExecute = false, WorkingDirectory = root });
            Close();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException or System.ComponentModel.Win32Exception or System.Runtime.InteropServices.COMException)
        {
            AppLogger.Error(_uninstall ? "Uninstall failed" : "Install failed", error);
            _status.Foreground = Br("Bad");
            _status.Text = $"{(_uninstall ? "Uninstall" : "Install")} failed: {error.Message}";
            _go.IsEnabled = _cancel.IsEnabled = _option.IsEnabled = true;
            _change.IsEnabled = !_update;
        }
    }
}
