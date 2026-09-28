namespace SuperDictate.Ui;

public enum DictationState
{
    Loading,
    Ready,
    Recording,
    Transcribing,
    Error,

    /// <summary>The speech runtime or the chosen model isn't installed yet: nothing records until setup is finished.</summary>
    NeedsSetup,
}
