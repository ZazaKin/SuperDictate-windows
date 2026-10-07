using System;
using System.Linq;
using System.Threading;
using SuperDictate.Setup;
using SuperDictate.Speech;
using SuperDictate.Storage;
using SuperDictate.Ui;

namespace SuperDictate;

internal static class Program
{
    /// <summary>Single installed application, single running instance.</summary>
    private const string InstanceName = @"Local\com.local.superdictate";
    private const string ShowEventName = @"Local\com.local.superdictate.show";

    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--self-test")
        {
            UseParentConsole();
            return SelfTest.Run(args);
        }

        // Pictures of the settings and the capsule for the README; see Snapshots.
        if (args.Length > 0 && args[0] == "--snapshot")
        {
            UseParentConsole();
            return Snapshots.Run(args);
        }

        // scripts\install-local.ps1 installs a fresh build through the same code as customers:
        // --install [folder]; without a folder, the current install folder or the suggested one.
        if (args.Length > 0 && args[0] == "--install")
        {
            UseParentConsole();
            try
            {
                var root = args.Length > 1 ? args[1] : Installer.SuggestedRoot();
                Console.WriteLine(Installer.Install(root, startAtSignIn: true, Console.WriteLine));
                return 0;
            }
            catch (Exception error) when (error is System.IO.IOException or UnauthorizedAccessException or InvalidOperationException
                                              or System.ComponentModel.Win32Exception or System.Runtime.InteropServices.COMException)
            {
                Console.Error.WriteLine($"Install failed: {error.Message}");
                return 1;
            }
        }

        // Only the installed copy runs. Any other copy, such as the file a customer
        // just downloaded, offers to install itself; Apps and features runs --uninstall.
        if (args.Contains("--uninstall") || !Installer.IsInstalledCopy)
        {
            var setup = new System.Windows.Application();
            ReportUnexpectedErrors(setup);
            return setup.Run(new SetupWindow(uninstall: args.Contains("--uninstall")));
        }

        using var instance = new Mutex(true, InstanceName, out var created);
        if (!created)
        {
            // The agent is already running. Signal the running instance to show its window!
            try
            {
                if (EventWaitHandle.TryOpenExisting(ShowEventName, out var showEvt))
                {
                    showEvt.Set();
                }
            }
            catch
            {
            }
            return 0;
        }

        using var showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
        using var quitEvent = new EventWaitHandle(false, EventResetMode.AutoReset, Installer.QuitEventName);

        // Data from before the single-folder layout moves into the install folder once.
        LegacyData.Adopt();
        var settings = SettingsStore.Load();
        if (LegacyData.GatherModels(settings)) SettingsStore.Save(settings);
        var application = new System.Windows.Application
        {
            ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown,
        };
        ReportUnexpectedErrors(application);

        var controller = new DictationController(settings);
        using var tray = new TrayController(controller, settings);

        // Listen for requests to show the window from second instances
        ThreadPool.RegisterWaitForSingleObject(showEvent, (_, _) =>
        {
            tray.OpenSettingsWindow();
        }, null, -1, false);

        // The installer asks a running copy to quit before replacing it.
        ThreadPool.RegisterWaitForSingleObject(quitEvent, (_, _) =>
        {
            application.Dispatcher.BeginInvoke(() => application.Shutdown());
        }, null, -1, true);

        application.Exit += (_, _) => controller.Dispose();
        controller.Start();

        // First run, or setup left unfinished: open where the runtime and a model are installed.
        var setupDone = SpeechRuntime.IsInstalled && ModelLibrary.All.Any(model => ModelLibrary.IsPresent(AppPaths.Models, model.Id));
        if (!setupDone)
        {
            tray.OpenSettingsWindow("models");
        }
        else if (args.Length > 0 && args[0] == "--show")
        {
            tray.OpenSettingsWindow();
        }

        return application.Run();
    }

    /// <summary>Command-line modes print to the console they were started from.</summary>
    private static void UseParentConsole()
    {
        Interop.NativeMethods.AttachConsole(Interop.NativeMethods.ATTACH_PARENT_PROCESS);
        try
        {
            Console.SetOut(new System.IO.StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
            Console.SetError(new System.IO.StreamWriter(Console.OpenStandardError()) { AutoFlush = true });
        }
        catch
        {
        }
    }

    private static bool _toldUser;

    private static void KeepRunning(Exception error)
    {
        AppLogger.Error("Unexpected error", error);
        if (_toldUser) return;
        _toldUser = true;
        System.Windows.MessageBox.Show(
            $"SuperDictate ran into an unexpected problem and kept running.\n\n{error.Message}\n\n" +
            $"Details are in {System.IO.Path.Combine(AppPaths.Logs, "dictation.log")}.",
            "SuperDictate", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
    }

    /// <summary>
    /// Unexpected errors go to the log instead of silently closing a background app.
    /// On the UI thread the app keeps running and says once where the details are.
    /// </summary>
    private static void ReportUnexpectedErrors(System.Windows.Application application)
    {
        application.DispatcherUnhandledException += (_, e) =>
        {
            e.Handled = true;
            KeepRunning(e.Exception);
        };

        // The tray icon and its menu run on Windows Forms, which has its own handler:
        // without this, an error there shows .NET's crash dialog instead.
        System.Windows.Forms.Application.SetUnhandledExceptionMode(System.Windows.Forms.UnhandledExceptionMode.CatchException);
        System.Windows.Forms.Application.ThreadException += (_, e) => KeepRunning(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => AppLogger.Error("Fatal error", e.ExceptionObject as Exception);
        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            AppLogger.Error("Background task error", e.Exception);
            e.SetObserved();
        };
    }
}
