using System;
using System.Diagnostics;
using System.Linq;
using SuperDictate.Input;
using SuperDictate.Speech;
using SuperDictate.Storage;

namespace SuperDictate;

/// <summary>
/// Runs diagnostic checks invoked via <c>SuperDictate.exe --self-test all</c>.
/// Each test prints PASS or FAIL and the process exits with code 0 if all
/// pass, 1 otherwise.
/// </summary>
internal static class SelfTest
{
    public static int Run(string[] args)
    {
        var filter = args.Length > 1 ? args[1] : "all";
        var passed = 0;
        var failed = 0;
        var skipped = 0;

        void Assert(string name, bool condition, string detail = "")
        {
            if (filter != "all" && !string.Equals(name, filter, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (condition)
            {
                Console.WriteLine($"  PASS  {name}");
                passed++;
            }
            else
            {
                Console.WriteLine($"  FAIL  {name}  {detail}");
                failed++;
            }
        }

        Console.WriteLine("SuperDictate self-test");
        Console.WriteLine(new string('-', 40));

        // Nothing installed yet (a first install, or a clean machine): test in a throwaway
        // folder rather than creating the default install folder on the user's drive.
        string? throwaway = null;
        if (!AppPaths.HasRecordedRoot)
        {
            throwaway = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"SuperDictate-selftest-{Guid.NewGuid():N}");
            AppPaths.UseRoot(throwaway);
        }

        // --- paths ---
        // These create the folders, which also proves the install folder is writable.
        Assert("paths.models", System.IO.Directory.Exists(AppPaths.Models), $"Can't create {AppPaths.Models}");
        Assert("paths.logs", System.IO.Directory.Exists(AppPaths.Logs));

        // Settings and history go to a throwaway folder, so a crash mid-test
        // cannot leave test values or probe entries in the user's real data.
        var scratch = System.IO.Directory.CreateDirectory(System.IO.Path.Combine(
            AppPaths.Temp, $"selftest-{Guid.NewGuid():N}")).FullName;
        AppPaths.DataRoot = scratch;
        // Likewise the AI cleanup key goes to a test credential, never the real one.
        CredentialStore.AiCleanupTarget = "com.local.superdictate/self-test";

        // --- settings ---
        try
        {
            var settings = new Settings
            {
                PrimaryHotkey = "F9",
                Language = "de",
                SelectedLanguages = new System.Collections.Generic.List<string> { "en", "de", "pl" },
            };
            SettingsStore.Save(settings);
            var loaded = SettingsStore.Load();
            Assert("settings.roundtrip",
                loaded.PrimaryHotkey == "F9" && loaded.Language == "de" && loaded.SelectedLanguages.Contains("pl") && loaded.SelectedLanguages.Contains("de"),
                $"Got PrimaryHotkey={loaded.PrimaryHotkey}, Language={loaded.Language}");
        }
        catch (Exception ex)
        {
            Assert("settings.roundtrip", false, ex.Message);
        }

        // --- credentials ---
        try
        {
            const string secret = "sk-self-test-ключ-äöü";
            CredentialStore.Write(CredentialStore.AiCleanupTarget, secret);
            var readBack = CredentialStore.Read(CredentialStore.AiCleanupTarget);
            CredentialStore.Delete(CredentialStore.AiCleanupTarget);
            var afterDelete = CredentialStore.Read(CredentialStore.AiCleanupTarget);
            Assert("credentials.roundtrip", readBack == secret && afterDelete is null,
                $"read back {(readBack == secret ? "matches" : "differs")}, after delete {(afterDelete is null ? "gone" : "still present")}");
        }
        catch (Exception ex)
        {
            Assert("credentials.roundtrip", false, ex.Message);
        }

        try
        {
            System.IO.File.WriteAllText(AppPaths.SettingsFile, "{ \"Language\": \"pl\", \"AiApiKey\": \"legacy-probe\" }");
            var migrated = SettingsStore.Load();
            var movedKey = CredentialStore.Read(CredentialStore.AiCleanupTarget);
            var fileHasKey = System.IO.File.ReadAllText(AppPaths.SettingsFile).Contains("AiApiKey");
            Assert("settings.migrate_ai_key",
                migrated.Language == "pl" && movedKey == "legacy-probe" && !fileHasKey,
                $"Language={migrated.Language}, key moved={movedKey == "legacy-probe"}, settings.json still has key={fileHasKey}");
        }
        catch (Exception ex)
        {
            Assert("settings.migrate_ai_key", false, ex.Message);
        }
        finally
        {
            try { CredentialStore.Delete(CredentialStore.AiCleanupTarget); } catch (System.ComponentModel.Win32Exception) { }
        }

        // --- models folder: a model counts only once model.bin is in place ---
        try
        {
            var modelsDir = System.IO.Path.Combine(scratch, "models");
            System.IO.Directory.CreateDirectory(System.IO.Path.Combine(modelsDir, "whisper-base"));
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(modelsDir, "whisper-base", "model.bin"), new byte[1024]);
            System.IO.Directory.CreateDirectory(System.IO.Path.Combine(modelsDir, "whisper-small"));
            System.IO.File.WriteAllText(System.IO.Path.Combine(modelsDir, "whisper-small", "config.json"), "{}");
            Assert("models.presence",
                Speech.ModelLibrary.IsPresent(modelsDir, "whisper-base")
                && !Speech.ModelLibrary.IsPresent(modelsDir, "whisper-small")
                && Speech.ModelLibrary.SizeOnDisk(modelsDir, "whisper-base") == 1024
                && Speech.ModelLibrary.Find("parakeet_tdt_v3").Id == "whisper-large-v3-turbo",
                "presence, size or fallback id is wrong");
            Assert("paths.same",
                AppPaths.SamePath(modelsDir, modelsDir.ToUpperInvariant() + System.IO.Path.DirectorySeparatorChar)
                && !AppPaths.SamePath(modelsDir, scratch));
        }
        catch (Exception ex)
        {
            Assert("models.presence", false, ex.Message);
        }

        // --- installer: the new exe replaces the old one, nothing half-copied is left ---
        try
        {
            var root = System.IO.Path.Combine(scratch, "Installed");
            System.IO.Directory.CreateDirectory(root);
            var exe = System.IO.Path.Combine(root, "SuperDictate.exe");
            var source = System.IO.Path.Combine(scratch, "download.exe");
            System.IO.File.WriteAllText(source, "first build");
            Setup.Installer.StageAndReplace(source, exe);
            System.IO.File.WriteAllText(source, "new build");
            Setup.Installer.StageAndReplace(source, exe);
            Assert("installer.swap",
                System.IO.File.ReadAllText(exe) == "new build"
                && System.IO.Directory.GetFileSystemEntries(root).Length == 1,
                "the exe or leftovers are wrong");
        }
        catch (Exception ex)
        {
            Assert("installer.swap", false, ex.Message);
        }

        // --- uninstall: program files go once the uninstaller has exited; data and its folder stay ---
        try
        {
            string MakeInstall(string name, bool withData)
            {
                var root = System.IO.Path.Combine(scratch, name);
                System.IO.Directory.CreateDirectory(root);
                System.IO.File.WriteAllText(System.IO.Path.Combine(root, "SuperDictate.exe"), "exe");
                System.IO.File.WriteAllText(System.IO.Path.Combine(root, Setup.Installer.UninstallShortcutName), "lnk");
                if (withData) System.IO.File.WriteAllText(System.IO.Path.Combine(System.IO.Directory.CreateDirectory(System.IO.Path.Combine(root, "Data")).FullName, "settings.json"), "{}");
                return root;
            }

            void RemoveProgram(string root)
            {
                // Stands in for the uninstaller: the script must wait for it to exit first.
                using var stand = Process.Start(new ProcessStartInfo("cmd.exe", "/c ping 127.0.0.1 -n 2 >nul") { UseShellExecute = false, CreateNoWindow = true })!;
                using var cleanup = Process.Start(new ProcessStartInfo("powershell.exe",
                    $"-NoProfile -NonInteractive -EncodedCommand {Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(Setup.Installer.RemoveProgramScript(stand.Id, root)))}")
                    { UseShellExecute = false, CreateNoWindow = true })!;
                var waited = !cleanup.WaitForExit(300) || !stand.HasExited; // Still waiting while the stand-in runs.
                cleanup.WaitForExit(30000);
                if (!waited) throw new InvalidOperationException("the cleanup didn't wait for the uninstaller to exit");
            }

            var kept = MakeInstall("UninstallKeep", withData: true);
            var gone = MakeInstall("UninstallAll", withData: false);
            RemoveProgram(kept);
            RemoveProgram(gone);
            Assert("installer.uninstall",
                !System.IO.File.Exists(System.IO.Path.Combine(kept, "SuperDictate.exe"))
                && !System.IO.File.Exists(System.IO.Path.Combine(kept, Setup.Installer.UninstallShortcutName))
                && System.IO.File.Exists(System.IO.Path.Combine(kept, "Data", "settings.json"))
                && !System.IO.Directory.Exists(gone),
                "program files, data or the folder are wrong after uninstall");
        }
        catch (Exception ex)
        {
            Assert("installer.uninstall", false, ex.Message);
        }

        // --- installer: a chosen folder must be empty or hold only an earlier SuperDictate ---
        try
        {
            var empty = System.IO.Path.Combine(scratch, "Chosen", "SuperDictate");
            var earlier = System.IO.Path.Combine(scratch, "Earlier");
            System.IO.Directory.CreateDirectory(System.IO.Path.Combine(earlier, "Models"));
            System.IO.Directory.CreateDirectory(System.IO.Path.Combine(earlier, "App"));
            System.IO.File.WriteAllText(System.IO.Path.Combine(earlier, "SuperDictate.exe"), "old");
            var foreign = System.IO.Path.Combine(scratch, "Foreign");
            System.IO.Directory.CreateDirectory(foreign);
            System.IO.File.WriteAllText(System.IO.Path.Combine(foreign, "report.docx"), "mine");
            Assert("installer.target",
                Setup.Installer.CheckTarget(empty) is null
                && Setup.Installer.CheckTarget(earlier) is null
                && Setup.Installer.CheckTarget(foreign) is not null
                && Setup.Installer.CheckTarget("SuperDictate") is not null
                && Setup.Installer.CheckTarget(System.IO.Path.GetPathRoot(scratch)!) is not null
                && Setup.Installer.CheckTarget(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "SuperDictate")) is not null,
                "an unusable folder was accepted or a usable one refused");
        }
        catch (Exception ex)
        {
            Assert("installer.target", false, ex.Message);
        }

        // --- install folder: suggested off the system drive whenever another local disk exists ---
        {
            var system = System.IO.Path.GetPathRoot(Environment.SystemDirectory)!;
            var otherDisk = System.IO.DriveInfo.GetDrives().Any(drive => drive.DriveType == System.IO.DriveType.Fixed && drive.IsReady
                                                                         && !string.Equals(drive.Name, system, StringComparison.OrdinalIgnoreCase));
            var first = AppPaths.Suggestions().First();
            Assert("paths.suggestion", !otherDisk || !first.StartsWith(system, StringComparison.OrdinalIgnoreCase), first);
        }

        // --- runtime: the worker and pinned packages travel inside the exe ---
        try
        {
            var worker = System.Text.Encoding.UTF8.GetString(Speech.SpeechRuntime.ReadResource("worker.py"));
            var pins = System.Text.Encoding.UTF8.GetString(Speech.SpeechRuntime.ReadResource("runtime-requirements.txt"));
            var gpu = System.Text.Encoding.UTF8.GetString(Speech.SpeechRuntime.ReadResource("runtime-requirements-gpu.txt"));
            Assert("runtime.resources",
                worker.Contains("def download") && pins.Contains("faster-whisper==") && gpu.Contains("nvidia-cudnn-cu12=="),
                "an embedded runtime file is missing or stale");
        }
        catch (Exception ex)
        {
            Assert("runtime.resources", false, ex.Message);
        }

        // --- runtime install progress follows pip's real output order ---
        {
            var pip = new Speech.PipProgress(3);
            var shares = new System.Collections.Generic.List<double>();
            foreach (var line in new[]
            {
                "Collecting av==18.1.0 (from -r req.txt (line 3))",
                "  Downloading av-18.1.0-cp313-cp313-win_amd64.whl.metadata (4.6 kB)",
                "Collecting ctranslate2==4.8.2 (from -r req.txt (line 4))",
                "Collecting numpy==2.5.3 (from -r req.txt (line 5))",
                "Downloading av-18.1.0-cp313-cp313-win_amd64.whl (27.6 MB)",
                "Progress 13800000 of 27600000",
                "Downloading ctranslate2-4.8.2-cp313-cp313-win_amd64.whl (19.2 MB)",
                "Using cached numpy-2.5.3-cp313-cp313-win_amd64.whl (12.6 MB)",
                "Installing collected packages: numpy, ctranslate2, av",
            })
            {
                pip.Feed(line);
                shares.Add(pip.Share);
            }

            var rising = shares.Zip(shares.Skip(1), (a, b) => b >= a).All(up => up);
            Assert("runtime.pip_progress",
                rising && Math.Abs(shares[3] - 0.1) < 1e-9 && shares[5] > shares[4] && shares[^1] == 0.9 && pip.Status == "Installing…",
                $"shares: {string.Join(", ", shares.Select(v => v.ToString("0.00")))}");
        }

        // --- updates: only a strictly newer, well-formed version counts ---
        Assert("updates.version_compare",
            Support.Updates.IsNewer("1.0.1", "1.0.0") && Support.Updates.IsNewer("1.10.0", "1.9.0")
            && !Support.Updates.IsNewer("1.0.0", "1.0.0") && !Support.Updates.IsNewer("0.9.9", "1.0.0")
            && !Support.Updates.IsNewer("latest", "1.0.0"));

        // --- the licence and notices ship inside the binary ---
        try
        {
            var license = System.Text.Encoding.UTF8.GetString(Speech.SpeechRuntime.ReadResource("LICENSE"));
            var notice = System.Text.Encoding.UTF8.GetString(Speech.SpeechRuntime.ReadResource("NOTICE.md"));
            Assert("support.license",
                license.Contains("Commons Clause") && license.Contains("Richard Courtman") && notice.Contains("Whisper"),
                "LICENSE or NOTICE.md is missing or outdated in the build");
        }
        catch (Exception ex)
        {
            Assert("support.license", false, ex.Message);
        }

        // --- release-settings.ini: empty donation lines are hidden, links and addresses told apart ---
        {
            var (repository, donations) = Support.Project.Parse(
                "; comment\r\n[release]\r\nversion = 1.0.0\r\nrepository = https://github.com/me/app/\r\n"
                + "[donations]\r\nKo-fi | Card = https://ko-fi.com/me?a=1\r\nEmpty | Card =\r\nUSDT (TRC20) | TRON only = TXyz\r\nBitcoin = bc1q\r\n");
            Assert("support.settings",
                repository == "https://github.com/me/app"
                && donations.Count == 3
                && donations[0] == new Support.Project.Donation("Ko-fi", Support.Project.DonationKind.Link, "https://ko-fi.com/me?a=1", "Card")
                && donations[1] == new Support.Project.Donation("USDT (TRC20)", Support.Project.DonationKind.Address, "TXyz", "TRON only")
                && donations[2].Note is null
                && Support.Project.UpdateManifest.StartsWith("https://raw.githubusercontent.com/", StringComparison.Ordinal),
                "release-settings.ini is read wrongly");
        }

        // --- mic button stays fully on screen; Ollama names ---
        Assert("micbutton.clamp",
            Ui.MicButton.ClampToArea(1900, 1030, 64, 0, 0, 1920, 1040) == (1856, 976)
            && Ui.MicButton.ClampToArea(-50, 10, 64, 0, 0, 1920, 1040) == (0, 10)
            && Ui.MicButton.ClampToArea(2500, 300, 64, 1920, 0, 3840, 1040) == (2500, 300));
        Assert("ollama.names",
            Speech.Ollama.ServerUrl("http://localhost:11434/v1/") == "http://localhost:11434"
            && Speech.Ollama.FullName("llama3.2") == "llama3.2:latest"
            && Speech.Ollama.FullName("qwen2.5:1.5b") == "qwen2.5:1.5b");

        // --- history ---
        try
        {
            var entry = new HistoryEntry(DateTimeOffset.UtcNow, "self-test-probe", 1.5, "stub");
            HistoryStore.Append(entry);
            var recent = HistoryStore.Recent(5);
            Assert("history.append", recent.Any(e => e.Text == "self-test-probe"),
                "Probe entry not found in recent history");
        }
        catch (Exception ex)
        {
            Assert("history.append", false, ex.Message);
        }

        // --- hotkey ---
        Assert("hotkey.parse_modifier",
            Hotkey.TryParse("RightAlt", out var h1) && h1!.IsModifierOnly);
        Assert("hotkey.parse_chord",
            Hotkey.TryParse("RightShift+RightAlt", out var h2) && h2!.IsModifierOnly && h2.Modifiers.Count == 2);
        Assert("hotkey.parse_key",
            Hotkey.TryParse("RightCtrl+A", out var h3) && !h3!.IsModifierOnly);
        Assert("hotkey.parse_empty",
            !Hotkey.TryParse("", out _));
        Assert("hotkey.parse_invalid",
            !Hotkey.TryParse("FakeKey", out _));

        // --- hotkey recorder: modifiers first, one key besides modifiers ---
        var recordProblem = Ui.SettingsWindow.ChordFromKeys(
            new[] { System.Windows.Input.Key.RightAlt, System.Windows.Input.Key.RightCtrl }, out var recorded);
        Assert("hotkey.record_chord", recordProblem is null && recorded == "RightCtrl+RightAlt",
            $"Got {recorded ?? recordProblem}");
        Assert("hotkey.record_rejects",
            Ui.SettingsWindow.ChordFromKeys(new[] { System.Windows.Input.Key.LeftCtrl, System.Windows.Input.Key.Left }, out _) is not null
            && Ui.SettingsWindow.ChordFromKeys(new[] { System.Windows.Input.Key.A, System.Windows.Input.Key.B }, out _) is not null);

        // --- capsule placement: top centre of the active monitor, never left of it ---
        var primary = Ui.CapsuleOverlay.CenteredLeft(0, 1920, 200);
        var secondary = Ui.CapsuleOverlay.CenteredLeft(1920, 3840, 200);
        var tooWide = Ui.CapsuleOverlay.CenteredLeft(-1280, -1180, 200);
        Assert("capsule.placement", primary == 860 && secondary == 2780 && tooWide == -1280,
            $"primary={primary}, secondary={secondary}, tooWide={tooWide}");

        // --- features ---
        try
        {
            // 1 second of 440 Hz sine at 16 kHz.
            var samples = new float[16000];
            for (var i = 0; i < samples.Length; i++)
            {
                samples[i] = (float)Math.Sin(2.0 * Math.PI * 440.0 * i / 16000.0) * 0.5f;
            }

            var extractor = new FeatureExtractor(new FeatureOptions());
            var (data, frames) = extractor.Compute(samples);
            Assert("features.shape", frames > 0 && data.Length == frames * extractor.MelBands,
                $"frames={frames}, data.Length={data.Length}, mels={extractor.MelBands}");
            Assert("features.finite", data.All(float.IsFinite),
                "Non-finite values in feature output");
        }
        catch (Exception ex)
        {
            Assert("features.shape", false, ex.Message);
        }

        // --- stub engine ---
        try
        {
            var engine = new StubSpeechEngine();
            engine.LoadAsync(default).Wait();
            Assert("engine.loaded", engine.IsLoaded);
            var text = engine.TranscribeAsync(new float[16000], default).Result;
            Assert("engine.transcribe", text.Contains("1.0s"), $"Got: {text}");
        }
        catch (Exception ex)
        {
            Assert("engine.transcribe", false, ex.Message);
        }

        // --- whisper engine: needs the speech runtime and a model in the install folder ---
        if (!SpeechRuntime.IsInstalled || !ModelLibrary.All.Any(model => ModelLibrary.IsPresent(AppPaths.Models, model.Id)))
        {
            // A new install folder has neither until the app sets them up, so this skips
            // rather than blocking the install; build-release.ps1 refuses a build with a SKIP.
            if (filter is "all" or "whisper.loaded")
            {
                Console.WriteLine($"  SKIP  whisper.loaded  no speech runtime or model in {AppPaths.Root} yet");
                skipped++;
            }
        }
        else
        {
            try
            {
                using var whisper = new WhisperSpeechEngine();
                whisper.LoadAsync(default).Wait();
                Assert("whisper.loaded", whisper.IsLoaded);
                var text = whisper.TranscribeAsync(new float[16000], default).Result;
                Assert("whisper.transcribe", text is not null);
            }
            catch (Exception ex)
            {
                Assert("whisper.loaded", false, ex.Message);
            }
        }

        // --- ai cleanup service ---
        try
        {
            var cleaned = Speech.AiCleanupService.CleanAsync("test text", new Settings { AiCleanupEnabled = false }).Result;
            Assert("ai_cleanup.passthrough", cleaned == "test text");

            var smartSettings = new Settings
            {
                AiCleanupEnabled = true,
                AiFormatPunctuation = true,
                AiRemoveFillers = true,
                AiRemoveDuplicates = true,
            };
            var smartCleaned = Speech.AiCleanupService.CleanLocalSmart(
                "ну короче мы должны отправить этот файл запятая чтобы успеть точка", smartSettings);
            Assert("ai_cleanup.local_smart",
                smartCleaned == "Мы должны отправить этот файл, чтобы успеть.",
                $"Got: {smartCleaned}");
        }
        catch (Exception ex)
        {
            Assert("ai_cleanup.local_smart", false, ex.Message);
        }

        // --- mic capture ---
        try
        {
            using var enumerator = new NAudio.CoreAudioApi.MMDeviceEnumerator();
            var devConsole = enumerator.GetDefaultAudioEndpoint(NAudio.CoreAudioApi.DataFlow.Capture, NAudio.CoreAudioApi.Role.Console);
            var devComm = enumerator.GetDefaultAudioEndpoint(NAudio.CoreAudioApi.DataFlow.Capture, NAudio.CoreAudioApi.Role.Communications);
            Console.WriteLine($"      Default Console device: {devConsole.FriendlyName}");
            Console.WriteLine($"      Default Comm device: {devComm.FriendlyName}");

            using var mic = new Audio.MicrophoneCapture();
            mic.Start(null);
            System.Threading.Thread.Sleep(1000);
            var samples = mic.Stop();
            float maxAmp = samples.Length > 0 ? samples.Max(Math.Abs) : 0f;
            double rms = samples.Length > 0 ? Math.Sqrt(samples.Average(s => s * s)) : 0.0;
            Console.WriteLine($"      Default mic ({mic.CurrentDeviceName}) samples: {samples.Length} (duration: {(double)samples.Length / 16000:0.00}s), Max: {maxAmp:F4}, RMS: {rms:F4}");
            Assert("mic.capture.default", samples.Length > 8000, $"Recorded {samples.Length} samples");
        }
        catch (Exception ex)
        {
            Assert("mic.capture.default", false, ex.Message);
        }

        // --- settings window ---
        try
        {
            var testSettings = SettingsStore.Load();
            using var testController = new Ui.DictationController(testSettings);
            var win = new Ui.SettingsWindow(testController, testSettings);
            Assert("ui.settings_window", win != null);

            // Without the runtime or the chosen model the engine doesn't start, so nothing can record.
            if (!testController.IsSetUp)
            {
                testController.RestartEngine();
                Assert("setup.gate", testController.State == Ui.DictationState.NeedsSetup, testController.State.ToString());
            }
        }
        catch (Exception ex)
        {
            Assert("ui.settings_window", false, ex.Message);
        }

        // --- theme contrast: text 4.5:1, indicators and outlines 3:1 ---
        static double Luminance(string hex)
        {
            static double Channel(byte value)
            {
                var s = value / 255.0;
                return s <= 0.04045 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
            }

            var color = System.Drawing.ColorTranslator.FromHtml(hex);
            return (0.2126 * Channel(color.R)) + (0.7152 * Channel(color.G)) + (0.0722 * Channel(color.B));
        }

        static double Contrast(string a, string b)
        {
            var (x, y) = (Luminance(a), Luminance(b));
            return (Math.Max(x, y) + 0.05) / (Math.Min(x, y) + 0.05);
        }

        {
            string Hex(string key) => key == "White" ? "#FFFFFF" : Ui.Theme.Color(key);
            var weak = new[]
                {
                    ("Text", "Card", 4.5), ("Text", "Side", 4.5), ("Text", "AccentSoft", 4.5), ("Text", "BadSoft", 4.5),
                    ("Muted", "Card", 4.5), ("Muted", "Side", 4.5), ("Warn", "Card", 4.5),
                    ("White", "Accent", 4.5), ("White", "BadFill", 4.5),
                    ("Accent", "Card", 3.0), ("Muted", "Field", 3.0), ("Ok", "Card", 3.0),
                    // Blue text: card titles, and soft buttons on their own tint.
                    ("Link", "Card", 4.5), ("Link", "AccentSoft", 4.5), ("Muted", "Bg", 4.5), ("Bad", "BadSoft", 4.5),
                }
                .Where(pair => Contrast(Hex(pair.Item1), Hex(pair.Item2)) < pair.Item3)
                .Select(pair => $"{pair.Item1} on {pair.Item2}")
                .ToList();
            Assert("theme.contrast", weak.Count == 0, string.Join(", ", weak));
        }

        try
        {
            System.IO.Directory.Delete(scratch, recursive: true);
            if (throwaway is not null) System.IO.Directory.Delete(throwaway, recursive: true);
        }
        catch (System.IO.IOException)
        {
            // A leftover folder in %TEMP% is harmless.
        }

        Console.WriteLine(new string('-', 40));
        Console.WriteLine($"Results: {passed} passed, {failed} failed" + (skipped > 0 ? $", {skipped} skipped" : ""));

        return failed == 0 ? 0 : 1;
    }
}
