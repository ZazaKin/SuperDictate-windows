namespace SuperDictate.Storage;

/// <summary>
/// Mirrors the macOS settings surface. Hotkeys are stored as text specs such as
/// "RightAlt" or "RightShift+RightAlt" and parsed by <see cref="Input.Hotkey"/>.
/// </summary>
public sealed class Settings
{
    // Right Alt replaces the macOS right Command. See README for the AltGr
    // caveat on layouts where right Alt produces characters.
    public string PrimaryHotkey { get; set; } = "RightAlt";

    public string AlternateHotkey { get; set; } = "RightCtrl+RightAlt";

    public string HistoryHotkey { get; set; } = "RightShift+RightAlt";

    public bool AlternateHotkeyEnabled { get; set; } = true;

    /// <summary>Hold to dictate, release to transcribe. Off by default.</summary>
    public bool PressAndHold { get; set; }

    /// <summary>Press Enter after pasting the transcript.</summary>
    public bool PressEnterAfterPaste { get; set; }

    public string Language { get; set; } = "auto";

    /// <summary>Active languages to recognize when Language is "auto". Excluded languages will not be evaluated.</summary>
    public System.Collections.Generic.List<string> SelectedLanguages { get; set; } = new() { "en", "ru", "de", "pl" };

    public string ModelId { get; set; } = "whisper-large-v3-turbo";

    /// <summary>
    /// Only read from settings of earlier versions, which let models live elsewhere:
    /// on start they move into the install folder's Models and this is cleared.
    /// </summary>
    public string? ModelsFolder { get; set; }

    /// <summary>WASAPI endpoint id, or null for the default capture device.</summary>
    public string? MicrophoneId { get; set; }

    public bool ShowTrayIcon { get; set; } = true;

    /// <summary>Floating button that starts and stops dictation with a click, for people who skip hotkeys.</summary>
    public bool ShowMicButton { get; set; } = true;

    /// <summary>Where the button was dragged to, in screen pixels; null puts it bottom right.</summary>
    public int? MicButtonX { get; set; }

    public int? MicButtonY { get; set; }

    public double CapsuleScale { get; set; } = 1.0;

    public string CapsuleAccent { get; set; } = "#5B8DEF";

    /// <summary>One of Ui.CapsuleSkin.All: midnight, graphite, paper, glass, aurora, telegram, contrast.</summary>
    public string CapsuleSkin { get; set; } = "midnight";

    /// <summary>0.5 to 1: how see-through the whole capsule is.</summary>
    public double CapsuleOpacity { get; set; } = 1.0;

    /// <summary>The voice meter: bars, wave, dots, scope, or none.</summary>
    public string CapsuleMeter { get; set; } = "bars";

    /// <summary>Show the words as they're spoken (the live preview).</summary>
    public bool CapsuleLiveText { get; set; } = true;

    /// <summary>Show how long the dictation has been running.</summary>
    public bool CapsuleTimer { get; set; }

    /// <summary>How wide the capsule may grow as words arrive, in points at size 1×.</summary>
    public double CapsuleMaxWidth { get; set; } = 460;

    /// <summary>Liquid Glass shows the live screen behind it (see Ui.LiveGlass); off, it is painted.</summary>
    public bool CapsuleLiveGlass { get; set; } = true;

    /// <summary>
    /// Where the capsule sits (see Ui.CapsulePlacement): the edge it keeps to on each
    /// axis (start, center or end) and how far across the screen, 0 to 1. Top center
    /// unless the user moved it.
    /// </summary>
    public string CapsuleHorizontal { get; set; } = "center";

    public double CapsuleX { get; set; } = 0.5;

    public string CapsuleVertical { get; set; } = "start";

    public double CapsuleY { get; set; }

    /// <summary>"active": the screen the user is working on; "primary": always the main screen.</summary>
    public string CapsuleScreen { get; set; } = "active";

    public int MaxRecordingMinutes { get; set; } = 20;

    public int HistoryLimit { get; set; } = 100;

    public string? ExportFolder { get; set; }

    // Optional AI cleanup. Disabled by default.
    public bool AiCleanupEnabled { get; set; }

    /// <summary>
    /// AI Cleanup mode: "local_smart" (built-in zero-latency local engine),
    /// "local_llm" (Ollama / LocalAI / LM Studio), or "cloud" (Groq / OpenAI).
    /// </summary>
    public string AiCleanupMode { get; set; } = "local_smart";

    /// <summary>Local LLM server endpoint (e.g. http://localhost:11434/v1).</summary>
    public string LocalLlmEndpoint { get; set; } = "http://localhost:11434/v1";

    /// <summary>Local model name in Ollama / LM Studio (e.g. llama3.2:1b, qwen2.5:1.5b).</summary>
    public string LocalLlmModel { get; set; } = "llama3.2:1b";

    /// <summary>Strip verbal filler words ("um", "эээ", "ну", "типа", "äh", "yyy").</summary>
    public bool AiRemoveFillers { get; set; } = true;

    /// <summary>Automatically format spoken punctuation words and fix sentence casing.</summary>
    public bool AiFormatPunctuation { get; set; } = true;

    /// <summary>Remove accidental stuttering or duplicated words ("the the", "я я").</summary>
    public bool AiRemoveDuplicates { get; set; } = true;

    /// <summary>Custom comma-separated words to strip during cleanup.</summary>
    public string CustomFillerWords { get; set; } = "";

    public string AiBaseUrl { get; set; } = "https://api.groq.com/openai/v1";

    public string AiModel { get; set; } = "openai/gpt-oss-20b";

    // The API key lives in Windows Credential Manager (CredentialStore.AiCleanupTarget).

    public int AiTimeoutSeconds { get; set; } = 8;
}
