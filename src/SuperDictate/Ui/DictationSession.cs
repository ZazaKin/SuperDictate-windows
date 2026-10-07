using System.Collections.Generic;

namespace SuperDictate.Ui;

/// <summary>
/// The rules of a dictation, from the engine loading to pasted text: what each key
/// press does in each state, when the microphone may stop, and which notice the
/// capsule gives. No audio, speech or windows here: <see cref="DictationController"/>
/// reports what happened with <see cref="Handle"/>, carries out the effects it gets
/// back and reports their outcome in turn. The Mac app's DictationSession has the
/// same shape.
/// </summary>
public sealed class DictationSession
{
    /// <summary>A recording shorter than this is a tap of the key, not a dictation.</summary>
    public const double Shortest = 0.3;

    public abstract record Event
    {
        /// <summary>(Re)load the engine. SetUp: the speech runtime and the chosen model are installed.</summary>
        public sealed record LoadEngine(bool SetUp) : Event;
        public sealed record EngineLoaded : Event;
        public sealed record EngineFailed : Event;

        /// <summary>The runtime or a model finished installing, outside a dictation's own steps.</summary>
        public sealed record Installed(bool SetUp) : Event;

        /// <summary>The hotkey, the tray or a button: start, or finish.</summary>
        public sealed record Toggle(bool PressEnter, bool SetUp, bool EngineReady) : Event;

        /// <summary>The hold released, or the alternate hotkey.</summary>
        public sealed record Finish(bool PressEnter) : Event;

        /// <summary>A minute without speech. Nobody may be watching, so it never presses Enter.</summary>
        public sealed record SilenceLimit : Event;

        /// <summary>Escape, or another key during a hold: the recording is dropped.</summary>
        public sealed record Cancel : Event;

        public sealed record MicrophoneOpened : Event;
        public sealed record MicrophoneFailed(string Message) : Event;

        /// <summary>How long the recording that just stopped is.</summary>
        public sealed record Recorded(double Seconds) : Event;
        public sealed record Transcribed(string Text) : Event;
        public sealed record TranscriptionFailed(string Message) : Event;

        /// <summary>The text is pasted and kept in the history.</summary>
        public sealed record Delivered : Event;

        /// <summary>The Capsule settings page's sample, on or off.</summary>
        public sealed record Sample(bool On) : Event;
    }

    public abstract record Effect
    {
        /// <summary>Load the engine off the UI thread; report EngineLoaded or EngineFailed.</summary>
        public sealed record LoadEngine : Effect;
        public sealed record ShowSetup : Effect;
        public sealed record Notice(string Text) : Effect;

        /// <summary>Show the capsule listening and open the microphone; report MicrophoneOpened or MicrophoneFailed.</summary>
        public sealed record Record : Effect;
        public sealed record StartLive : Effect;

        /// <summary>Stop the live preview and the microphone, keep the audio; report Recorded.</summary>
        public sealed record StopRecording(string Status) : Effect;

        /// <summary>Stop the live preview and the microphone, and throw the audio away.</summary>
        public sealed record DropRecording : Effect;
        public sealed record Hide : Effect;

        /// <summary>Turn the kept audio into text; report Transcribed or TranscriptionFailed.</summary>
        public sealed record Transcribe : Effect;

        /// <summary>Hide the capsule, paste the text and keep it in the history; report Delivered.</summary>
        public sealed record Deliver(string Text, double Seconds, bool PressEnter) : Effect;

        /// <summary>A dictation that gave no text, for the settings window's last result.</summary>
        public sealed record Failed(string Error, double Seconds) : Effect;
        public sealed record StartSample : Effect;
        public sealed record StopSample : Effect;
    }

    private static readonly IReadOnlyList<Effect> None = [];

    private bool _micOpen;
    private (bool PressEnter, string Status)? _finishWhenOpen;
    private bool _cancelWhenOpen;
    private bool _pressEnter;
    private double _seconds;

    public DictationState State { get; private set; } = DictationState.Loading;

    public bool IsBusy => State is DictationState.Recording or DictationState.Transcribing;

    public IReadOnlyList<Effect> Handle(Event happened)
    {
        switch (happened)
        {
            case Event.LoadEngine(var setUp):
                // A dictation under way finishes on the engine it started with.
                if (!setUp)
                {
                    if (!IsBusy) State = DictationState.NeedsSetup;
                    return None;
                }

                if (!IsBusy) State = DictationState.Loading;
                return [new Effect.LoadEngine()];
            case Event.EngineLoaded:
                // A load overtaken by a missing model, or by a dictation, changes nothing.
                if (State == DictationState.Loading) State = DictationState.Ready;
                return None;
            case Event.EngineFailed:
                if (State == DictationState.Loading) State = DictationState.Error;
                return None;
            case Event.Installed(var setUp):
                return State is DictationState.Ready or DictationState.Error or DictationState.NeedsSetup
                    ? Handle(new Event.LoadEngine(setUp))
                    : None;

            case Event.Toggle(var pressEnter, var setUp, var engineReady):
                if (State == DictationState.Recording) return Stop(pressEnter, "Processing…");
                if (State == DictationState.Transcribing) return None;

                // Nothing records until the speech runtime and the chosen model are in place.
                if (!setUp)
                {
                    State = DictationState.NeedsSetup;
                    return [new Effect.Notice("Finish setup in Settings"), new Effect.ShowSetup()];
                }

                // Setup just finished outside the app's own buttons.
                if (State == DictationState.NeedsSetup) return Handle(new Event.LoadEngine(true));
                if (State == DictationState.Loading) return None;

                // Don't let the user talk into a recording nothing can transcribe. A reload
                // recovers a crashed worker; with no model it just reports Error again.
                if (!engineReady) return [new Effect.Notice("Speech model unavailable"), .. Handle(new Event.LoadEngine(true))];

                State = DictationState.Recording;
                _micOpen = false;
                _finishWhenOpen = null;
                _cancelWhenOpen = false;
                return [new Effect.Record()];
            case Event.Finish(var pressEnter):
                return Stop(pressEnter, "Processing…");
            case Event.SilenceLimit:
                return Stop(pressEnter: false, "No speech for a minute");
            case Event.Cancel:
                if (State != DictationState.Recording || _cancelWhenOpen) return None;
                if (!_micOpen)
                {
                    // The microphone is still opening: it closes as soon as it is open.
                    _cancelWhenOpen = true;
                    return [new Effect.Notice("Cancelled")];
                }

                State = DictationState.Ready;
                return [new Effect.DropRecording(), new Effect.Notice("Cancelled")];

            case Event.MicrophoneOpened:
                if (State != DictationState.Recording) return None;
                _micOpen = true;
                if (_cancelWhenOpen)
                {
                    State = DictationState.Ready;
                    return [new Effect.DropRecording()];
                }

                return _finishWhenOpen is (var enter, var status) ? Stop(enter, status) : [new Effect.StartLive()];
            case Event.MicrophoneFailed(var message):
                if (State != DictationState.Recording) return None;
                State = DictationState.Error;
                return [new Effect.Notice("Microphone unavailable"), new Effect.Failed($"Microphone error: {message}", 0)];

            case Event.Recorded(var seconds):
                if (State != DictationState.Transcribing) return None;
                _seconds = seconds;
                if (seconds >= Shortest) return [new Effect.Transcribe()];
                // A tap rather than a dictation.
                State = DictationState.Ready;
                return [new Effect.Hide()];
            case Event.Transcribed(var text):
                if (State != DictationState.Transcribing) return None;
                if (!string.IsNullOrWhiteSpace(text)) return [new Effect.Deliver(text, _seconds, _pressEnter)];
                // The notice hides itself, so the next dictation can start right away.
                State = DictationState.Ready;
                return [new Effect.Notice("No speech detected"), new Effect.Failed("No speech recognized.", _seconds)];
            case Event.TranscriptionFailed(var message):
                if (State != DictationState.Transcribing) return None;
                State = DictationState.Error;
                return [new Effect.Notice("Transcription failed"), new Effect.Failed(message, _seconds)];
            case Event.Delivered:
                if (State == DictationState.Transcribing) State = DictationState.Ready;
                return None;

            case Event.Sample(var on):
                // The capsule on screen belongs to a dictation while there is one.
                if (on && IsBusy) return None;
                return on ? [new Effect.StartSample()] : [new Effect.StopSample()];
            default:
                return None;
        }
    }

    private IReadOnlyList<Effect> Stop(bool pressEnter, string status)
    {
        if (State != DictationState.Recording || _cancelWhenOpen) return None;

        // A stop pressed while the microphone is still opening waits for it.
        if (!_micOpen)
        {
            _finishWhenOpen = (pressEnter, status);
            return None;
        }

        _pressEnter = pressEnter;
        State = DictationState.Transcribing;
        return [new Effect.StopRecording(status)];
    }
}
