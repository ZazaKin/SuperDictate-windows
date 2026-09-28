using System;
using System.Collections.Generic;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace SuperDictate.Audio;

/// <summary>
/// Audio capture endpoint device descriptor.
/// </summary>
public sealed record AudioDeviceOption(string Id, string Name, bool IsDefault);

/// <summary>
/// WASAPI shared-mode capture, downmixed and resampled to the 16 kHz mono float
/// stream every supported model expects. Shared mode hands us the endpoint mix
/// format, so conversion happens here rather than in the audio stack.
/// </summary>
public sealed class MicrophoneCapture : IDisposable
{
    public const int TargetSampleRate = 16000;
    private static readonly Guid SubtypeIeeeFloat = new("00000003-0000-0010-8000-00aa00389b71");

    private readonly List<float> _samples = new();
    private readonly object _gate = new();

    private WasapiCapture? _capture;
    private double _carry;
    private float _previous;
    private double _lowPassState;
    private double _lowPassStateSecond;
    private double _lowPassAlpha;
    private double _step = 1;

    public string? CurrentDeviceName { get; private set; }
    public string? CurrentDeviceId { get; private set; }

    /// <summary>Root mean square of the most recent block, for the capsule.</summary>
    public event EventHandler<double>? LevelChanged;

    public bool IsCapturing => _capture is not null;

    /// <summary>
    /// Enumerate all active audio capture devices on the system.
    /// </summary>
    public static IReadOnlyList<AudioDeviceOption> GetAvailableDevices()
    {
        var list = new List<AudioDeviceOption>();
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            string? defaultId = null;
            try
            {
                var def = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Console);
                defaultId = def.ID;
            }
            catch
            {
                try
                {
                    var def = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Multimedia);
                    defaultId = def.ID;
                }
                catch
                {
                    // Default may not be available if no devices exist
                }
            }

            var endpoints = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);
            foreach (var ep in endpoints)
            {
                var isDefault = string.Equals(ep.ID, defaultId, StringComparison.OrdinalIgnoreCase);
                list.Add(new AudioDeviceOption(ep.ID, ep.FriendlyName, isDefault));
            }
        }
        catch
        {
            // Suppress enumeration errors
        }

        return list;
    }

    public void Start(string? deviceId)
    {
        Stop();

        using var enumerator = new MMDeviceEnumerator();
        MMDevice? device = null;

        if (!string.IsNullOrWhiteSpace(deviceId))
        {
            try
            {
                device = enumerator.GetDevice(deviceId);
            }
            catch
            {
                device = null;
            }
        }

        if (device is null)
        {
            // Prefer Console (Standard Windows default recording device for speech recognition and commands)
            try
            {
                device = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Console);
            }
            catch
            {
                try
                {
                    device = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Multimedia);
                }
                catch
                {
                    try
                    {
                        device = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications);
                    }
                    catch
                    {
                        // Fallback to first available active capture endpoint
                        var endpoints = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);
                        foreach (var ep in endpoints)
                        {
                            device = ep;
                            break;
                        }
                    }
                }
            }
        }

        if (device is null)
        {
            throw new InvalidOperationException("No active audio capture device found on this system.");
        }

        CurrentDeviceName = device.FriendlyName;
        CurrentDeviceId = device.ID;

        _capture = new WasapiCapture(device);
        var format = _capture.WaveFormat;

        _step = (double)format.SampleRate / TargetSampleRate;
        _carry = 0;
        _previous = 0;
        _lowPassState = 0;
        _lowPassStateSecond = 0;

        // Gentle anti-alias ahead of decimation. Two one-pole sections at 7 kHz
        // keep the transition band out of the 8 kHz Nyquist of the target rate.
        _lowPassAlpha = _step > 1.05
            ? Math.Exp(-2.0 * Math.PI * 7000.0 / format.SampleRate)
            : 0;

        lock (_gate)
        {
            _samples.Clear();
        }

        _capture.DataAvailable += OnData;
        _capture.StartRecording();
    }

    /// <summary>Stops capture and returns everything recorded so far.</summary>
    public float[] Stop()
    {
        if (_capture is not null)
        {
            try
            {
                _capture.StopRecording();
            }
            catch
            {
                // Ignore errors during stop
            }

            // Brief wait for any in-flight buffer to be delivered by WASAPI thread
            System.Threading.Thread.Sleep(30);

            _capture.DataAvailable -= OnData;
            _capture.Dispose();
            _capture = null;
        }

        lock (_gate)
        {
            var recorded = _samples.ToArray();
            _samples.Clear();
            return recorded;
        }
    }

    public double RecordedSeconds
    {
        get
        {
            lock (_gate)
            {
                return (double)_samples.Count / TargetSampleRate;
            }
        }
    }

    private void OnData(object? sender, WaveInEventArgs args)
    {
        var format = _capture?.WaveFormat;
        if (format is null || args.BytesRecorded == 0)
        {
            return;
        }

        var mono = ToMono(args.Buffer, args.BytesRecorded, format);
        if (mono.Length == 0)
        {
            return;
        }

        var sum = 0.0;
        foreach (var sample in mono)
        {
            sum += sample * sample;
        }

        LevelChanged?.Invoke(this, Math.Sqrt(sum / mono.Length));

        if (_lowPassAlpha > 0)
        {
            for (var index = 0; index < mono.Length; index++)
            {
                _lowPassState = ((1 - _lowPassAlpha) * mono[index]) + (_lowPassAlpha * _lowPassState);
                _lowPassStateSecond = ((1 - _lowPassAlpha) * _lowPassState) + (_lowPassAlpha * _lowPassStateSecond);
                mono[index] = (float)_lowPassStateSecond;
            }
        }

        var resampled = Resample(mono);

        lock (_gate)
        {
            _samples.AddRange(resampled);
        }
    }

    private static float[] ToMono(byte[] buffer, int count, WaveFormat format)
    {
        var channels = Math.Max(1, format.Channels);
        var bytesPerSample = format.BitsPerSample / 8;
        if (bytesPerSample <= 0) return Array.Empty<float>();

        var frames = count / (bytesPerSample * channels);
        var mono = new float[frames];

        for (var frame = 0; frame < frames; frame++)
        {
            var total = 0f;
            for (var channel = 0; channel < channels; channel++)
            {
                var offset = ((frame * channels) + channel) * bytesPerSample;
                total += ReadSample(buffer, offset, format);
            }

            mono[frame] = total / channels;
        }

        return mono;
    }

    private static float ReadSample(byte[] buffer, int offset, WaveFormat format)
    {
        if (offset + (format.BitsPerSample / 8) > buffer.Length)
        {
            return 0f;
        }

        if (format.Encoding == WaveFormatEncoding.IeeeFloat)
        {
            return BitConverter.ToSingle(buffer, offset);
        }

        if (format is WaveFormatExtensible wfe && wfe.SubFormat == SubtypeIeeeFloat)
        {
            return BitConverter.ToSingle(buffer, offset);
        }

        return format.BitsPerSample switch
        {
            16 => BitConverter.ToInt16(buffer, offset) / 32768f,
            24 => ((buffer[offset] | (buffer[offset + 1] << 8) | ((sbyte)buffer[offset + 2] << 16)) / 8388608f),
            32 => BitConverter.ToInt32(buffer, offset) / 2147483648f,
            _ => 0f,
        };
    }

    /// <summary>Linear resampling with state carried across capture buffers.</summary>
    private float[] Resample(float[] mono)
    {
        if (Math.Abs(_step - 1) < 1e-9)
        {
            _previous = mono[^1];
            return mono;
        }

        var extended = new float[mono.Length + 1];
        extended[0] = _previous;
        Array.Copy(mono, 0, extended, 1, mono.Length);

        var output = new List<float>((int)(mono.Length / _step) + 2);
        var position = _carry;

        while (position + 1 < extended.Length)
        {
            var index = (int)position;
            var fraction = position - index;
            output.Add((float)((extended[index] * (1 - fraction)) + (extended[index + 1] * fraction)));
            position += _step;
        }

        _carry = position - (extended.Length - 1);
        _previous = mono[^1];
        return output.ToArray();
    }

    public void Dispose() => Stop();
}
