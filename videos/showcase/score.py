"""
The tour's sound, made from nothing but sine waves and noise, so it carries no
one else's rights: a soft pad in D major, a slow pluck once the tour gets
going, and effects on the film's own cues (whooshes on the whip
pans, a riser into the zoom-through, a tick as each skin reaches the front, a
pop and a shimmer on the name). It resolves on the name and fades.

    python score.py assets/score.wav

Every time below is the film's (index.html and compositions/); move a shot and
move its cue here too.
"""
import math
import sys
import wave

import numpy as np

RATE = 48000
LENGTH = 25.0
N = int(RATE * LENGTH)
t_all = np.arange(N) / RATE
rng = np.random.default_rng(7)


def midi(note):
    return 440.0 * 2 ** ((note - 69) / 12)


def span(start, seconds):
    """Indices and local time for a sound starting at `start`."""
    a = max(0, int(start * RATE))
    b = min(N, int((start + seconds) * RATE))
    return slice(a, b), (np.arange(b - a) / RATE)


def add(bus, start, sound, pan=0.0, gain=1.0):
    """Mixes a mono sound into a stereo bus at `start`, panned -1 (left) to 1 (right)."""
    first = int(start * RATE)
    where = slice(first, min(N, first + len(sound)))
    sound = sound[: where.stop - where.start] * gain
    left, right = math.cos((pan + 1) * math.pi / 4), math.sin((pan + 1) * math.pi / 4)
    bus[where, 0] += sound * left
    bus[where, 1] += sound * right


# ---- Music -----------------------------------------------------------------

BEAT = 60 / 90
BAR = 4 * BEAT
# D major: Dmaj9, Bm9, Gmaj9, A6sus, then home to D on the name.
CHORDS = [
    (0.0, [50, 57, 62, 66, 69, 73, 76]),
    (2 * BAR, [47, 54, 59, 62, 66, 69, 73]),
    (4 * BAR, [43, 50, 55, 59, 62, 66, 69]),
    (6 * BAR, [45, 52, 57, 62, 64, 66, 71]),
    (20.7, [38, 50, 57, 62, 66, 69, 74, 78]),
]

pad = np.zeros((N, 2))
for index, (start, notes) in enumerate(CHORDS):
    end = CHORDS[index + 1][0] if index + 1 < len(CHORDS) else LENGTH
    seconds = end - start + 1.6
    where, t = span(start, seconds)
    # Slow in, slow out, overlapping the next chord.
    envelope = np.minimum(1, t / 1.2) * np.clip((seconds - t) / 1.6, 0, 1)
    for note in notes:
        for detune, pan in ((-0.07, -0.6), (0.0, 0.0), (0.07, 0.6)):
            frequency = midi(note + detune)
            voice = np.zeros_like(t)
            for harmonic in range(1, 9):
                if frequency * harmonic > 9000:
                    break
                voice += np.sin(2 * math.pi * frequency * harmonic * t + rng.uniform(0, 6.28)) / harmonic * math.exp(-harmonic / 2.6)
            # A slow shimmer, so the pad breathes.
            voice *= envelope * (0.85 + 0.15 * np.sin(2 * math.pi * 0.23 * t + note))
            left, right = math.cos((pan + 1) * math.pi / 4), math.sin((pan + 1) * math.pi / 4)
            pad[where, 0] += voice * left
            pad[where, 1] += voice * right
pad *= 0.018
# The pad swells in over the first two seconds and opens up when the tour does.
pad *= np.clip(t_all / 2.0, 0, 1)[:, None] * (0.8 + 0.2 * np.clip((t_all - 6.4) / 0.6, 0, 1))[:, None]


def pluck(frequency, seconds=0.9):
    _, t = span(0, seconds)
    tone = np.sin(2 * math.pi * frequency * t) + 0.35 * np.sin(4 * math.pi * frequency * t) + 0.12 * np.sin(6 * math.pi * frequency * t)
    return tone * np.exp(-t / 0.22) * np.minimum(1, t / 0.004)


# A slow pluck every two beats, rising through the chord: calm, never busy.
arp = np.zeros((N, 2))
step = 2 * BEAT
moment = 6.85
while moment < 20.7 - 0.05:
    chord = [notes for start, notes in CHORDS if start <= moment][-1]
    beat = int(round((moment - 6.85) / step))
    note = chord[3:][beat % len(chord[3:])]
    add(arp, moment, pluck(midi(note), seconds=1.6), pan=0.35 * math.sin(beat * 1.3), gain=0.03)
    moment += step
# One soft echo, a beat behind, to the other side.
echo = int(RATE * BEAT)
arp[echo:, 0] += arp[:-echo, 1] * 0.18
arp[echo:, 1] += arp[:-echo, 0] * 0.18

# ---- Effects ---------------------------------------------------------------

effects = np.zeros((N, 2))


def noise(seconds):
    return rng.standard_normal(int(RATE * seconds))


def band(signal, low, high):
    """Keeps the band between low and high Hz (a brick wall in the frequency domain, softened at the edges)."""
    spectrum = np.fft.rfft(signal)
    frequencies = np.fft.rfftfreq(len(signal), 1 / RATE)
    gate = np.clip((frequencies - low) / (low * 0.5 + 1), 0, 1) * np.clip((high - frequencies) / (high * 0.5), 0, 1)
    return np.fft.irfft(spectrum * gate, len(signal))


def whoosh(seconds, peak, rising=False):
    """Air rushing past, loudest at `peak` seconds in; brighter as it rises when `rising`."""
    _, t = span(0, seconds)
    low, high = band(noise(seconds), 200, 1800), band(noise(seconds), 1500, 7000)
    shape = np.exp(-((t - peak) / (seconds * 0.22)) ** 2)
    mix = np.clip(t / seconds, 0, 1) if rising else shape
    return (low * (1 - 0.6 * mix) + high * mix * 0.8) * shape


def tick(brightness=1.0):
    _, t = span(0, 0.06)
    click = band(noise(0.06), 2000, 9000) * np.exp(-t / 0.006) * 0.5
    tone = np.sin(2 * math.pi * 2600 * brightness * t) * np.exp(-t / 0.012)
    return click + tone * 0.6


def pop():
    _, t = span(0, 0.18)
    frequency = 220 + 500 * np.exp(-t / 0.02)
    return np.sin(2 * math.pi * np.cumsum(frequency) / RATE) * np.exp(-t / 0.05)


def shimmer(seconds, notes):
    out = np.zeros(int(RATE * seconds))
    for index, note in enumerate(notes):
        first = int(index * seconds / (len(notes) + 2) * RATE)
        t = np.arange(len(out) - first) / RATE
        tone = np.sin(2 * math.pi * midi(note) * t) + 0.25 * np.sin(2 * math.pi * midi(note) * 2.01 * t)
        out[first:] += tone * np.exp(-t / 0.6) * np.minimum(1, t / 0.01)
    return out


def riser(seconds):
    _, t = span(0, seconds)
    sweep = band(noise(seconds), 600, 6000) * (t / seconds) ** 2
    frequency = 300 + 900 * (t / seconds) ** 2
    tone = np.sin(2 * math.pi * np.cumsum(frequency) / RATE) * (t / seconds) ** 3 * 0.3
    return sweep + tone


def thud():
    _, t = span(0, 0.6)
    frequency = 48 + 60 * np.exp(-t / 0.05)
    return np.sin(2 * math.pi * np.cumsum(frequency) / RATE) * np.exp(-t / 0.25)


# 01 Dictate: the capsule arrives, the words land at the cursor, the capsule leaves.
add(effects, 0.05, pop(), gain=0.10, pan=0.0)
add(effects, 0.4, shimmer(2.2, [86, 90, 93]), gain=0.035, pan=0.2)
add(effects, 4.6, tick(0.8), gain=0.32, pan=-0.1)
add(effects, 4.62, pop(), gain=0.08)
# Whip pan into the glass, at its fastest on the cut.
add(effects, 6.25, whoosh(0.9, 0.6), gain=0.09, pan=0.3)
# 02 Glass: a light crosses it; the push-in rises into the zoom-through.
add(effects, 7.55, shimmer(1.6, [81, 85, 88, 93]), gain=0.03, pan=-0.3)
add(effects, 9.6, riser(1.95), gain=0.06)
add(effects, 11.2, whoosh(0.7, 0.35, rising=True), gain=0.07)
add(effects, 11.5, thud(), gain=0.20)
# 03 Skins: a tick as each skin reaches the front.
for index in range(1, 10):
    add(effects, 11.45 + 0.95 + (index - 1) * 0.5 + 0.21, tick(1.0 + index * 0.02), gain=0.12, pan=0.35)
# Whip pan into the languages.
add(effects, 16.7, whoosh(0.9, 0.6), gain=0.09, pan=0.3)
# 04 Languages: the count lands; each language comes in.
add(effects, 19.0, pop(), gain=0.10, pan=-0.3)
for index in range(3):
    add(effects, 17.3 + index * 3.5 / 3, tick(0.7), gain=0.08, pan=0.4)
# 05 Close: the icon pops, a light passes through the name.
add(effects, 21.1, pop(), gain=0.14)
add(effects, 22.3, shimmer(1.6, [86, 90, 93, 98]), gain=0.045, pan=0.25)

# ---- Mix -------------------------------------------------------------------

# The music, and the effects a touch above it: present, never sharp.
MUSIC = 0.4
EFFECTS = 0.35
dry = (pad + arp) * MUSIC + effects * EFFECTS
# A small room: decaying stereo noise as the impulse response.
_, ir_t = span(0, 2.2)
impulse = rng.standard_normal((len(ir_t), 2)) * np.exp(-ir_t / 0.55)[:, None]
impulse /= np.sqrt((impulse ** 2).sum(axis=0))
size = 1 << int(math.ceil(math.log2(N + len(ir_t))))
wet = np.stack([np.fft.irfft(np.fft.rfft(dry[:, c], size) * np.fft.rfft(impulse[:, c], size), size)[:N] for c in range(2)], axis=1)
mix = dry + 0.3 * wet
# Fade out over the last 1.5 seconds; a gentle limiter; peaks at -1 dBFS.
mix *= np.clip((LENGTH - t_all) / 1.5, 0, 1)[:, None]
mix = np.tanh(mix * 1.4) / 1.4
mix *= 10 ** (-1 / 20) / np.abs(mix).max()

with wave.open(sys.argv[1] if len(sys.argv) > 1 else "score.wav", "wb") as out:
    out.setnchannels(2)
    out.setsampwidth(2)
    out.setframerate(RATE)
    out.writeframes((mix * 32767).astype("<i2").tobytes())
print("score:", LENGTH, "s")
