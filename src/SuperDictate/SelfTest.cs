using System;
using System.Diagnostics;
using System.Linq;
using SuperDictate.Input;
using SuperDictate.Speech;
using SuperDictate.Storage;
using SessionEffect = SuperDictate.Ui.DictationSession.Effect;
using SessionEvent = SuperDictate.Ui.DictationSession.Event;

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

        // --- capsule placement: the edge it keeps to, growth away from it, always on screen ---
        {
            var area = new System.Windows.Rect(0, 0, 1920, 1040);
            var small = new System.Windows.Size(168, 48);
            var top = Ui.CapsulePlacement.TopCenter.Place(small, 460, area);
            // Dropped against the left edge it keeps to the left; right, the right; mid-screen, its centre.
            var left = Ui.CapsulePlacement.FromRect(new System.Windows.Rect(12, 400, 168, 48), area);
            var right = Ui.CapsulePlacement.FromRect(new System.Windows.Rect(1740, 980, 168, 48), area);
            var middle = Ui.CapsulePlacement.FromRect(new System.Windows.Rect(860, 500, 168, 48), area);
            // A placement puts the capsule back where it was dropped, on any screen size.
            var dropped = new System.Windows.Rect(300, 200, 168, 48);
            var back = Ui.CapsulePlacement.FromRect(dropped, area).Place(small, 460, area);
            // Grown to its widest, a right-edge capsule extends to the left and stays on screen.
            var grown = right.Place(new System.Windows.Size(460, 48), 460, area);
            // A centred capsule near an edge is pulled in so its widest still fits.
            var squeezed = new Ui.CapsulePlacement(Ui.CapsuleEdge.Center, 0.36, Ui.CapsuleEdge.Start, 0)
                .Place(small, 460, new System.Windows.Rect(0, 0, 800, 600));
            Assert("capsule.placement",
                top.Left == 876 && top.Top == 12
                && left.Horizontal == Ui.CapsuleEdge.Start && left.X == 0
                && right.Horizontal == Ui.CapsuleEdge.End && right.Vertical == Ui.CapsuleEdge.End && right.AtBottom
                && middle.Horizontal == Ui.CapsuleEdge.Center && middle.Vertical == Ui.CapsuleEdge.Center
                && Math.Abs(back.Left - dropped.Left) < 0.01 && Math.Abs(back.Top - dropped.Top) < 0.01
                && Math.Abs(grown.Right - 1908) < 0.01 && grown.Left >= 12
                && squeezed.Left + (squeezed.Width / 2) - 230 >= 12,
                $"top={top}, left={left}, right={right}, middle={middle}, back={back}, grown={grown}, squeezed={squeezed}");

            // Dragged near an edge or the centre, it snaps onto it and says which.
            var nearLeft = Ui.CapsulePlacement.Snap(new System.Windows.Rect(20, 300, 168, 48), area, out var leftGuides);
            var nearCenter = Ui.CapsulePlacement.Snap(new System.Windows.Rect(870, 300, 168, 48), area, out var centerGuides);
            var offScreen = Ui.CapsulePlacement.Snap(new System.Windows.Rect(-500, 2000, 168, 48), area, out _);
            Assert("capsule.snap",
                nearLeft.Left == 12 && leftGuides.HasFlag(Ui.CapsulePlacement.Guides.Left)
                && nearCenter.Left == 876 && centerGuides.HasFlag(Ui.CapsulePlacement.Guides.CenterX)
                && offScreen.Left == 12 && offScreen.Bottom == 1028,
                $"nearLeft={nearLeft}, nearCenter={nearCenter}, offScreen={offScreen}");
        }

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

        // --- live session: driven with made-up audio, 100 ms per tick, like its timer ---
        {
            var random = new Random(7);
            float Noise(double amplitude) => (float)(((random.NextDouble() * 2) - 1) * amplitude);
            float Quiet(double t) => Noise(0.002);
            // Syllables: a quarter second of voice, then a short gap.
            float Talk(double t) => t * 1000 % 350 < 250 ? (float)(Math.Sin(2 * Math.PI * 220 * t) * 0.12) + Noise(0.002) : Quiet(t);

            (double? LimitAt, string Settled, string Tail, int Passes, bool SawTail) Drive(Func<double, float> signal, double seconds)
            {
                var audio = new System.Collections.Generic.List<float>();
                double? limitAt = null;
                string settled = "", tail = "";
                var passes = 0;
                var sawTail = false;
                using var live = new LiveSession(
                    start => start >= audio.Count ? Array.Empty<float>() : audio.GetRange(start, audio.Count - start).ToArray(),
                    _ =>
                    {
                        passes++;
                        return System.Threading.Tasks.Task.FromResult("phrase");
                    });
                live.SilenceLimitReached += () => limitAt ??= audio.Count / 16000.0;
                live.DraftChanged += (s, t) =>
                {
                    (settled, tail) = (s, t);
                    sawTail |= s == "" && t == "phrase";
                };
                for (var tick = 0; tick < seconds * 10; tick++)
                {
                    for (var i = 0; i < 1600; i++) audio.Add(signal(audio.Count / 16000.0));
                    live.Tick();
                }

                return (limitAt, settled, tail, passes, sawTail);
            }

            var quiet = Drive(Quiet, 61);
            var spoke = Drive(t => t < 3 ? Talk(t) : Quiet(t), 61);
            var fan = Drive(_ => Noise(0.05), 61);
            var clicks = Drive(t => t * 1000 % 500 < 15 ? Noise(0.3) : Quiet(t), 61);
            Assert("live.silence_limit",
                quiet.LimitAt is >= 59.9 and <= 60.3 && spoke.LimitAt is null && fan.LimitAt is not null && clicks.LimitAt is not null,
                $"quiet={quiet.LimitAt}, after speech={spoke.LimitAt}, fan={fan.LimitAt}, clicks={clicks.LimitAt}");

            // Two phrases with a pause after each: each shows as a tail first, then settles.
            // The long silence between them runs no passes (it would take about 30).
            var phrases = Drive(t => t is < 2 or (>= 20 and < 22) ? Talk(t) : Quiet(t), 25);
            Assert("live.draft",
                phrases.Settled == "phrase phrase" && phrases.Tail == "" && phrases.SawTail && phrases.Passes <= 10,
                $"settled='{phrases.Settled}', tail='{phrases.Tail}', passes={phrases.Passes}, sawTail={phrases.SawTail}");
            Assert("live.no_speech", Drive(Quiet, 5).Passes == 0 && Drive(_ => Noise(0.05), 5).Passes == 0);
        }

        // --- live caption: unchanged words stay, settled ones are marked, old ones are let go ---
        try
        {
            var caption = new Ui.LiveCaption();
            caption.Apply(12.5, 300, System.Windows.Media.Brushes.White);
            caption.Show("", "hello");
            caption.Show("hello there", "");
            caption.Show("hello there", "how are");
            caption.Show("hello there", "how is it");
            var revised = caption.Shown == "hello there how is it" && caption.SettledShown == 2;
            caption.Show(string.Join(" ", Enumerable.Range(0, 100).Select(i => $"w{i}")), "tail");
            var trimmed = caption.Shown.EndsWith("w99 tail") && caption.Shown.Split(' ').Length <= 48;
            Assert("live.caption", revised && trimmed, caption.Shown);
        }
        catch (Exception ex)
        {
            Assert("live.caption", false, ex.Message);
        }

        // --- dictation session: the rules from key press to pasted text (the Mac app's DictationSessionTests) ---
        {
            static bool Gives(System.Collections.Generic.IReadOnlyList<SessionEffect> effects, params SessionEffect[] expected) =>
                effects.SequenceEqual(expected);

            static Ui.DictationSession Ready()
            {
                var ready = new Ui.DictationSession();
                ready.Handle(new SessionEvent.LoadEngine(true));
                ready.Handle(new SessionEvent.EngineLoaded());
                return ready;
            }

            // Recording, with the microphone open.
            static Ui.DictationSession Recording()
            {
                var recording = Ready();
                recording.Handle(new SessionEvent.Toggle(false, true, true));
                recording.Handle(new SessionEvent.MicrophoneOpened());
                return recording;
            }

            var session = new Ui.DictationSession();
            var gate = Gives(session.Handle(new SessionEvent.Toggle(false, false, false)),
                           new SessionEffect.Notice("Finish setup in Settings"), new SessionEffect.ShowSetup())
                       && session.State == Ui.DictationState.NeedsSetup;
            var loads = Gives(session.Handle(new SessionEvent.Toggle(false, true, false)), new SessionEffect.LoadEngine())
                        && session.State == Ui.DictationState.Loading
                        && Gives(session.Handle(new SessionEvent.Toggle(false, true, true)))
                        && session.State == Ui.DictationState.Loading;
            Assert("session.setup_gate", gate && loads, session.State.ToString());

            session = new Ui.DictationSession();
            session.Handle(new SessionEvent.LoadEngine(true));
            session.Handle(new SessionEvent.LoadEngine(false));
            session.Handle(new SessionEvent.EngineLoaded());
            var overtaken = session.State == Ui.DictationState.NeedsSetup;
            session = Ready();
            var unloaded = Gives(session.Handle(new SessionEvent.Toggle(false, true, false)),
                               new SessionEffect.Notice("Speech model unavailable"), new SessionEffect.LoadEngine());
            session.Handle(new SessionEvent.EngineFailed());
            Assert("session.engine", overtaken && unloaded && session.State == Ui.DictationState.Error, session.State.ToString());

            session = Ready();
            var record = Gives(session.Handle(new SessionEvent.Toggle(true, true, true)), new SessionEffect.Record())
                         && session.State == Ui.DictationState.Recording;
            // A stop pressed while the microphone is still opening waits for it, then stops without a live preview.
            var waits = Gives(session.Handle(new SessionEvent.Toggle(true, true, true)))
                        && Gives(session.Handle(new SessionEvent.MicrophoneOpened()), new SessionEffect.StopRecording("Processing…"));
            var busy = Gives(session.Handle(new SessionEvent.Toggle(true, true, true)))
                       && Gives(session.Handle(new SessionEvent.Sample(true)));
            var transcribe = Gives(session.Handle(new SessionEvent.Recorded(4)), new SessionEffect.Transcribe());
            var deliver = Gives(session.Handle(new SessionEvent.Transcribed("Hello there")),
                              new SessionEffect.Deliver("Hello there", 4, true))
                          && session.State == Ui.DictationState.Transcribing;
            session.Handle(new SessionEvent.Delivered());
            Assert("session.dictation", record && waits && busy && transcribe && deliver && session.State == Ui.DictationState.Ready,
                $"record={record}, waits={waits}, busy={busy}, transcribe={transcribe}, deliver={deliver}, state={session.State}");

            session = Recording();
            var silence = Gives(session.Handle(new SessionEvent.SilenceLimit()),
                              new SessionEffect.StopRecording("No speech for a minute"));
            session.Handle(new SessionEvent.Recorded(60));
            Assert("session.silence_limit",
                silence && Gives(session.Handle(new SessionEvent.Transcribed("Done")), new SessionEffect.Deliver("Done", 60, false)));

            session = Ready();
            session.Handle(new SessionEvent.Toggle(false, true, true));
            var micFailed = Gives(session.Handle(new SessionEvent.MicrophoneFailed("busy")),
                                new SessionEffect.Notice("Microphone unavailable"), new SessionEffect.Failed("Microphone error: busy", 0))
                            && session.State == Ui.DictationState.Error
                            && Gives(session.Handle(new SessionEvent.MicrophoneOpened()));
            Assert("session.microphone", micFailed, session.State.ToString());

            session = Recording();
            session.Handle(new SessionEvent.Finish(false));
            session.Handle(new SessionEvent.Recorded(2));
            var noSpeech = Gives(session.Handle(new SessionEvent.Transcribed("  ")),
                               new SessionEffect.Notice("No speech detected"), new SessionEffect.Failed("No speech recognized.", 2))
                           && session.State == Ui.DictationState.Ready;
            session = Recording();
            session.Handle(new SessionEvent.Finish(false));
            session.Handle(new SessionEvent.Recorded(2));
            var broke = Gives(session.Handle(new SessionEvent.TranscriptionFailed("worker exited")),
                             new SessionEffect.Notice("Transcription failed"), new SessionEffect.Failed("worker exited", 2))
                         && session.State == Ui.DictationState.Error;
            Assert("session.no_text", noSpeech && broke, $"noSpeech={noSpeech}, failed={broke}");

            // A tap of the key rather than a dictation: the capsule goes, nothing is reported.
            session = Recording();
            session.Handle(new SessionEvent.Finish(false));
            var tap = Gives(session.Handle(new SessionEvent.Recorded(0.2)), new SessionEffect.Hide())
                      && session.State == Ui.DictationState.Ready;
            Assert("session.tap", tap, session.State.ToString());

            session = Ready();
            var idle = Gives(session.Handle(new SessionEvent.Cancel()));
            session = Recording();
            var dropped = Gives(session.Handle(new SessionEvent.Cancel()), new SessionEffect.DropRecording(), new SessionEffect.Notice("Cancelled"))
                          && session.State == Ui.DictationState.Ready;
            // Cancelled while the microphone is still opening: it closes once open, and a stop no longer counts.
            session = Ready();
            session.Handle(new SessionEvent.Toggle(false, true, true));
            var early = Gives(session.Handle(new SessionEvent.Cancel()), new SessionEffect.Notice("Cancelled"))
                        && Gives(session.Handle(new SessionEvent.Finish(false)))
                        && Gives(session.Handle(new SessionEvent.MicrophoneOpened()), new SessionEffect.DropRecording())
                        && session.State == Ui.DictationState.Ready;
            Assert("session.cancel", idle && dropped && early, $"idle={idle}, dropped={dropped}, early={early}");

            // A dictation under way finishes on the engine it started with; a reload doesn't strand the microphone.
            session = Recording();
            var reload = Gives(session.Handle(new SessionEvent.LoadEngine(true)), new SessionEffect.LoadEngine())
                         && session.State == Ui.DictationState.Recording
                         && Gives(session.Handle(new SessionEvent.Finish(true)), new SessionEffect.StopRecording("Processing…"));
            Assert("session.reload_while_recording", reload, session.State.ToString());

            session = Ready();
            var sample = Gives(session.Handle(new SessionEvent.Sample(true)), new SessionEffect.StartSample())
                         && Gives(session.Handle(new SessionEvent.Sample(false)), new SessionEffect.StopSample());
            Assert("session.sample", sample);
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
                // A draft of silence stays empty: the live preview must never invent words.
                var draft = whisper.TranscribeAsync(new float[16000], default, draft: true).Result;
                Assert("whisper.draft", draft == "", $"Got: {draft}");
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

            // Every capsule skin's text, and its dimmer status text, on the skin's own fill.
            string Rgb(System.Windows.Media.Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";
            var faint = Ui.CapsuleSkin.All
                .SelectMany(skin => new[] { (skin, skin.Text, skin.Fill), (skin, skin.Muted, skin.Fill), (skin, skin.Muted, skin.FillEnd) })
                .Where(pair => Contrast(Rgb(pair.Item2), Rgb(pair.Item3)) < 4.5)
                .Select(pair => pair.skin.Name)
                .Distinct()
                .ToList();
            Assert("capsule.skins", faint.Count == 0, string.Join(", ", faint));
        }

        // --- theme templates: each applies, and every animation in it finds its target ---
        // A broken name or property path would otherwise only throw when someone hovers or clicks.
        {
            var resources = Ui.Theme.Create();
            System.Windows.Controls.Control Styled(System.Windows.Controls.Control control, string? key)
            {
                if (key is not null) control.Style = (System.Windows.Style)resources[key];
                return control;
            }

            var controls = new (string Name, System.Windows.Controls.Control Control)[]
            {
                ("Button", Styled(new System.Windows.Controls.Button(), null)),
                ("GhostButton", Styled(new System.Windows.Controls.Button(), "GhostButton")),
                ("Switch", Styled(new System.Windows.Controls.CheckBox(), null)),
                ("Chip", Styled(new System.Windows.Controls.CheckBox(), "Chip")),
                ("Nav", Styled(new System.Windows.Controls.RadioButton(), "Nav")),
                ("Slider", Styled(new System.Windows.Controls.Slider(), null)),
                ("SkinTile", Styled(new System.Windows.Controls.RadioButton(), "SkinTile")),
                ("LanguageTile", Styled(new System.Windows.Controls.CheckBox(), "LanguageTile")),
            };
            var host = new System.Windows.Controls.StackPanel { Resources = resources };
            var broken = new System.Collections.Generic.List<string>();
            foreach (var (name, control) in controls)
            {
                try
                {
                    host.Children.Add(control);
                    if (!control.ApplyTemplate()) throw new InvalidOperationException("no template");
                    var parts = new System.Collections.Generic.List<(System.Windows.FrameworkElement Owner, System.Windows.FrameworkTemplate Template)> { (control, control.Template) };
                    if (control is System.Windows.Controls.Slider slider)
                    {
                        // The thumb's own template carries the drag animation.
                        var thumb = ((System.Windows.Controls.Primitives.Track)slider.Template.FindName("PART_Track", slider)).Thumb;
                        thumb.ApplyTemplate();
                        parts.Add((thumb, thumb.Template));
                    }

                    foreach (var (owner, template) in parts)
                    {
                        var triggers = template is System.Windows.Controls.ControlTemplate t ? t.Triggers : null;
                        foreach (var trigger in triggers ?? Enumerable.Empty<System.Windows.TriggerBase>())
                        {
                            var actions = trigger is System.Windows.EventTrigger events
                                ? events.Actions
                                : trigger.EnterActions.Concat(trigger.ExitActions);
                            foreach (var begin in actions.OfType<System.Windows.Media.Animation.BeginStoryboard>())
                            {
                                begin.Storyboard.Begin(owner, template);
                            }
                        }
                    }
                }
                catch (Exception error)
                {
                    broken.Add($"{name}: {error.Message}");
                }
            }

            Assert("theme.templates", broken.Count == 0, string.Join("; ", broken));

            // The capsule in every skin and meter, and the position editor, build without throwing.
            try
            {
                var view = new Ui.CapsuleView();
                foreach (var skin in Ui.CapsuleSkin.All)
                {
                    foreach (var meter in new[] { "bars", "wave", "pulse" })
                    {
                        view.Apply(new Ui.CapsuleStyle(skin.Id, "#5B8DEF", 1.2, 0.8, meter, true));
                        view.ShowText("Listening…", meter: true);
                        view.ShowDraft("hello there", "how are");
                        view.Elapsed = TimeSpan.FromSeconds(75);
                        view.SetLevel(0.05);
                        view.Render(1.5);
                    }
                }

                _ = new Ui.CapsuleLayoutEditor(view.Current, Ui.CapsulePlacement.TopCenter, null);
                Assert("capsule.view", true);

                // Every offered language has its own picture, and each one draws.
                var bare = SpokenLanguages.All.Where(language => !Ui.LanguageIcon.HasPicture(language.Code)).Select(language => language.Code).ToList();
                foreach (var language in SpokenLanguages.All)
                {
                    var icon = Ui.LanguageIcon.Create(language.Code, 32);
                    icon.Measure(new System.Windows.Size(32, 32));
                    icon.Arrange(new System.Windows.Rect(0, 0, 32, 32));
                    new System.Windows.Media.Imaging.RenderTargetBitmap(32, 32, 96, 96, System.Windows.Media.PixelFormats.Pbgra32).Render(icon);
                }

                Assert("languages.icons", bare.Count == 0, string.Join(", ", bare));

                // The widest setting, kept between its limits.
                view.Apply(new Ui.CapsuleStyle("midnight", "#5B8DEF", 1.5, 1, "bars", false, 700));
                var wide = view.Widest;
                view.Apply(new Ui.CapsuleStyle("midnight", "#5B8DEF", 1, 1, "bars", false, 50));
                Assert("capsule.max_width", Math.Abs(wide - 1050) < 0.01 && Math.Abs(view.Widest - Ui.CapsuleView.LeastWidest) < 0.01,
                    $"wide={wide}, narrow={view.Widest}");

                // Live glass copies the screen; this part needs no window.
                using var glass = new Ui.LiveGlass();
                Assert("capsule.live_glass", glass.Copy(new Interop.NativeMethods.RECT { Left = 0, Top = 0, Right = 64, Bottom = 32 }));
            }
            catch (Exception error)
            {
                Assert("capsule.view", false, error.Message);
            }
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
