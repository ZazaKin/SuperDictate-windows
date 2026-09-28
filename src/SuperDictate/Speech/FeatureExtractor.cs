using System;

namespace SuperDictate.Speech;

public sealed class FeatureOptions
{
    public int SampleRate { get; set; } = 16000;

    public int WindowLength { get; set; } = 400;

    public int HopLength { get; set; } = 160;

    public int FftSize { get; set; } = 512;

    public int MelBands { get; set; } = 80;

    public double Preemphasis { get; set; } = 0.97;

    public double LogGuard { get; set; } = 1e-5;

    public bool PerFeatureNormalize { get; set; } = true;

    public double LowFrequency { get; set; }

    public double HighFrequency { get; set; } = 8000;
}

/// <summary>
/// Log-mel filterbank matching the NeMo preprocessor used by the Parakeet and
/// GigaAM exports: preemphasis, periodic Hann, Slaney mel scale, natural log
/// with a guard, then per-feature mean and variance normalisation.
/// </summary>
public sealed class FeatureExtractor
{
    private readonly FeatureOptions _options;
    private readonly double[] _window;
    private readonly double[][] _filterbank;

    public FeatureExtractor(FeatureOptions options)
    {
        _options = options;
        _window = BuildHannWindow(options.WindowLength);
        _filterbank = BuildMelFilterbank(options);
    }

    public int MelBands => _options.MelBands;

    /// <summary>Returns features as a flat time-major array of shape [frames, mels].</summary>
    public (float[] Data, int Frames) Compute(float[] samples)
    {
        if (samples.Length < _options.WindowLength)
        {
            return (Array.Empty<float>(), 0);
        }

        var emphasised = new double[samples.Length];
        emphasised[0] = samples[0];
        for (var index = 1; index < samples.Length; index++)
        {
            emphasised[index] = samples[index] - (_options.Preemphasis * samples[index - 1]);
        }

        var frames = 1 + ((samples.Length - _options.WindowLength) / _options.HopLength);
        var bins = (_options.FftSize / 2) + 1;
        var data = new float[frames * _options.MelBands];

        var real = new double[_options.FftSize];
        var imaginary = new double[_options.FftSize];
        var power = new double[bins];

        for (var frame = 0; frame < frames; frame++)
        {
            Array.Clear(real);
            Array.Clear(imaginary);

            var start = frame * _options.HopLength;
            for (var index = 0; index < _options.WindowLength; index++)
            {
                real[index] = emphasised[start + index] * _window[index];
            }

            Fft(real, imaginary);

            for (var bin = 0; bin < bins; bin++)
            {
                power[bin] = (real[bin] * real[bin]) + (imaginary[bin] * imaginary[bin]);
            }

            for (var mel = 0; mel < _options.MelBands; mel++)
            {
                var weights = _filterbank[mel];
                var energy = 0.0;
                for (var bin = 0; bin < bins; bin++)
                {
                    if (weights[bin] != 0)
                    {
                        energy += weights[bin] * power[bin];
                    }
                }

                data[(frame * _options.MelBands) + mel] = (float)Math.Log(energy + _options.LogGuard);
            }
        }

        if (_options.PerFeatureNormalize)
        {
            Normalize(data, frames, _options.MelBands);
        }

        return (data, frames);
    }

    private static void Normalize(float[] data, int frames, int mels)
    {
        for (var mel = 0; mel < mels; mel++)
        {
            var sum = 0.0;
            for (var frame = 0; frame < frames; frame++)
            {
                sum += data[(frame * mels) + mel];
            }

            var mean = sum / frames;
            var variance = 0.0;
            for (var frame = 0; frame < frames; frame++)
            {
                var delta = data[(frame * mels) + mel] - mean;
                variance += delta * delta;
            }

            var deviation = Math.Sqrt(variance / Math.Max(1, frames - 1)) + 1e-5;
            for (var frame = 0; frame < frames; frame++)
            {
                data[(frame * mels) + mel] = (float)((data[(frame * mels) + mel] - mean) / deviation);
            }
        }
    }

    private static double[] BuildHannWindow(int length)
    {
        var window = new double[length];
        for (var index = 0; index < length; index++)
        {
            // Periodic, matching torch.hann_window(periodic=True).
            window[index] = 0.5 - (0.5 * Math.Cos(2.0 * Math.PI * index / length));
        }

        return window;
    }

    private static double[][] BuildMelFilterbank(FeatureOptions options)
    {
        var bins = (options.FftSize / 2) + 1;
        var points = new double[options.MelBands + 2];
        var lowMel = HzToMel(options.LowFrequency);
        var highMel = HzToMel(options.HighFrequency);

        for (var index = 0; index < points.Length; index++)
        {
            points[index] = MelToHz(lowMel + ((highMel - lowMel) * index / (options.MelBands + 1)));
        }

        var frequencies = new double[bins];
        for (var bin = 0; bin < bins; bin++)
        {
            frequencies[bin] = (double)bin * options.SampleRate / options.FftSize;
        }

        var filterbank = new double[options.MelBands][];
        for (var mel = 0; mel < options.MelBands; mel++)
        {
            filterbank[mel] = new double[bins];
            var left = points[mel];
            var centre = points[mel + 1];
            var right = points[mel + 2];

            // Slaney normalisation: equal area per filter.
            var scale = 2.0 / (right - left);

            for (var bin = 0; bin < bins; bin++)
            {
                var frequency = frequencies[bin];
                double weight = 0;

                if (frequency >= left && frequency <= centre && centre > left)
                {
                    weight = (frequency - left) / (centre - left);
                }
                else if (frequency > centre && frequency <= right && right > centre)
                {
                    weight = (right - frequency) / (right - centre);
                }

                filterbank[mel][bin] = weight * scale;
            }
        }

        return filterbank;
    }

    // Slaney mel scale, as used by librosa with htk=False.
    private static double HzToMel(double hz)
    {
        const double LinearStep = 200.0 / 3.0;
        const double BreakHz = 1000.0;
        var breakMel = BreakHz / LinearStep;
        var logStep = Math.Log(6.4) / 27.0;

        return hz < BreakHz ? hz / LinearStep : breakMel + (Math.Log(hz / BreakHz) / logStep);
    }

    private static double MelToHz(double mel)
    {
        const double LinearStep = 200.0 / 3.0;
        const double BreakHz = 1000.0;
        var breakMel = BreakHz / LinearStep;
        var logStep = Math.Log(6.4) / 27.0;

        return mel < breakMel ? mel * LinearStep : BreakHz * Math.Exp(logStep * (mel - breakMel));
    }

    /// <summary>In-place iterative radix-2 Cooley-Tukey transform.</summary>
    private static void Fft(double[] real, double[] imaginary)
    {
        var length = real.Length;

        for (int i = 1, j = 0; i < length; i++)
        {
            var bit = length >> 1;
            for (; (j & bit) != 0; bit >>= 1)
            {
                j ^= bit;
            }

            j ^= bit;

            if (i < j)
            {
                (real[i], real[j]) = (real[j], real[i]);
                (imaginary[i], imaginary[j]) = (imaginary[j], imaginary[i]);
            }
        }

        for (var size = 2; size <= length; size <<= 1)
        {
            var angle = -2.0 * Math.PI / size;
            var stepReal = Math.Cos(angle);
            var stepImaginary = Math.Sin(angle);

            for (var start = 0; start < length; start += size)
            {
                double twiddleReal = 1;
                double twiddleImaginary = 0;

                for (var offset = 0; offset < size / 2; offset++)
                {
                    var evenIndex = start + offset;
                    var oddIndex = evenIndex + (size / 2);

                    var oddReal = (real[oddIndex] * twiddleReal) - (imaginary[oddIndex] * twiddleImaginary);
                    var oddImaginary = (real[oddIndex] * twiddleImaginary) + (imaginary[oddIndex] * twiddleReal);

                    real[oddIndex] = real[evenIndex] - oddReal;
                    imaginary[oddIndex] = imaginary[evenIndex] - oddImaginary;
                    real[evenIndex] += oddReal;
                    imaginary[evenIndex] += oddImaginary;

                    var nextReal = (twiddleReal * stepReal) - (twiddleImaginary * stepImaginary);
                    twiddleImaginary = (twiddleReal * stepImaginary) + (twiddleImaginary * stepReal);
                    twiddleReal = nextReal;
                }
            }
        }
    }
}
