using System;

namespace Match3.Core
{
    /// <summary>
    /// Procedural background-music composer — pure math, no UnityEngine, in the same
    /// spirit as <see cref="SfxSynth"/> (which stays untouched: SFX are mono one-shots,
    /// music is a stereo sequenced loop, so they share no useful surface).
    ///
    /// Each chapter gets an 8-bar loop in its own key/tempo/mood: a bass line walking
    /// an i–VI–III–VII progression, a slow detuned pad, a seeded pentatonic arpeggio
    /// and off-beat hats. Everything is DETERMINISTIC (fixed per-chapter seed, no
    /// clock, no shared state) — same chapter, same bytes — and LOOP-PERFECT: note
    /// tails wrap around the buffer end back to the start, so bar 1 already contains
    /// the reverb-ish tail of bar 8.
    /// </summary>
    public static class MusicComposer
    {
        public const int SampleRate = 44100;
        public const int Bars = 8;
        public const int BeatsPerBar = 4;

        private readonly struct ChapterStyle
        {
            public readonly int RootMidi;      // bass root note
            public readonly int Bpm;
            public readonly float Brightness;  // 0..1 — hat level + arp overtones
            public readonly float ArpDensity;  // chance an 8th-note arp step sounds

            public ChapterStyle(int rootMidi, int bpm, float brightness, float arpDensity)
            {
                RootMidi = rootMidi;
                Bpm = bpm;
                Brightness = brightness;
                ArpDensity = arpDensity;
            }
        }

        // One entry per campaign chapter (purple night, ocean teal, dusk plum, ember
        // tail, ...). Chapters beyond the table reuse the last mood.
        private static readonly ChapterStyle[] Styles =
        {
            new ChapterStyle(45, 84, 0.45f, 0.55f), // A2  — purple night: warm, unhurried
            new ChapterStyle(50, 90, 0.70f, 0.65f), // D3  — ocean teal: brighter, flowing
            new ChapterStyle(42, 86, 0.40f, 0.50f), // F#2 — dusk plum: darker, sparse
            new ChapterStyle(48, 94, 0.80f, 0.70f), // C3  — ember tail: lively, glinting
        };

        /// <summary>Minor-pentatonic offsets — every arp/chime note comes from here.</summary>
        private static readonly int[] Pentatonic = { 0, 3, 5, 7, 10, 12 };

        /// <summary>Bar chords: i, VI, III, VII (semitone root offset, is-major).</summary>
        private static readonly (int offset, bool major)[] Progression =
        {
            (0, false), (8, true), (3, true), (10, true),
        };

        public static int SamplesPerBeat(int chapter) =>
            (int)Math.Round(SampleRate * 60.0 / StyleFor(chapter).Bpm);

        public static int TotalSamplesPerChannel(int chapter) =>
            SamplesPerBeat(chapter) * BeatsPerBar * Bars;

        /// <summary>The complete loop as interleaved stereo floats in [-1, 1].</summary>
        public static float[] ComposeStereo(int chapter)
        {
            ChapterStyle style = StyleFor(chapter);
            int total = TotalSamplesPerChannel(chapter);
            int beat = SamplesPerBeat(chapter);
            int bar = beat * BeatsPerBar;
            var mix = new float[total * 2];

            uint seed = (uint)(0x9E3779B9 ^ (Math.Max(0, chapter) * 2654435761u + 12345u));

            for (int barIndex = 0; barIndex < Bars; barIndex++)
            {
                (int chordOffset, bool major) = Progression[barIndex % Progression.Length];
                int barStart = barIndex * bar;
                int chordRoot = style.RootMidi + chordOffset;

                // Bass: root held for the bar, sub-heavy, dead centre.
                AddNote(mix, total, barStart, MidiToFreq(chordRoot - 12), bar,
                        volume: 0.20f, pan: 0f, attack: 0.012f, release: 0.35f, timbre: 0.25f);

                // Pad: a triad sustained through the bar, softly detuned left/right.
                int third = chordRoot + (major ? 4 : 3) + 12;
                int fifth = chordRoot + 7 + 12;
                AddNote(mix, total, barStart, MidiToFreq(chordRoot + 12) - 0.6f, bar, 0.075f, -0.55f, 0.45f, 0.6f, 0.5f);
                AddNote(mix, total, barStart, MidiToFreq(third) + 0.6f, bar, 0.065f, 0.55f, 0.5f, 0.6f, 0.5f);
                AddNote(mix, total, barStart, MidiToFreq(fifth), bar, 0.055f, 0f, 0.55f, 0.6f, 0.5f);

                // Chime: one long high chord tone at the top of every other bar.
                if (barIndex % 2 == 0)
                {
                    AddNote(mix, total, barStart, MidiToFreq(chordRoot + 24 + (major ? 4 : 3)),
                            (int)(bar * 0.9f), 0.055f, barIndex % 4 == 0 ? 0.3f : -0.3f,
                            attack: 0.004f, release: 0.85f, timbre: 0.7f);
                }

                // Arpeggio: seeded pentatonic 8th notes over the chord, ping-pong panned.
                for (int step = 0; step < BeatsPerBar * 2; step++)
                {
                    uint roll = NextRandom(ref seed);
                    if (roll % 1000 >= (uint)(style.ArpDensity * 1000f))
                        continue;

                    int degree = Pentatonic[(int)(NextRandom(ref seed) % (uint)Pentatonic.Length)];
                    float freq = MidiToFreq(chordRoot + 24 + degree);
                    int start = barStart + step * (beat / 2);
                    float pan = (step % 2 == 0) ? -0.35f : 0.35f;
                    AddNote(mix, total, start, freq, (int)(beat * 0.9f), 0.11f, pan,
                            attack: 0.004f, release: 0.7f, timbre: 0.35f + 0.3f * style.Brightness);
                }

                // Hats: a whisper of noise on the off-beats keeps the pulse readable.
                for (int b = 0; b < BeatsPerBar; b++)
                {
                    int start = barStart + b * beat + beat / 2;
                    AddHat(mix, total, start, (int)(0.05f * SampleRate), 0.035f * style.Brightness);
                }
            }

            Normalize(mix, 0.88f);
            return mix;
        }

        /// <summary>The loop packed as a 16-bit stereo PCM WAV.</summary>
        public static byte[] ComposeWav(int chapter) => ToWavStereo(ComposeStereo(chapter));

        public static byte[] ToWavStereo(float[] interleaved, int sampleRate = SampleRate)
        {
            if (interleaved == null) throw new ArgumentNullException(nameof(interleaved));

            using var stream = new System.IO.MemoryStream();
            using var writer = new System.IO.BinaryWriter(stream);

            int dataBytes = interleaved.Length * 2;
            writer.Write(new[] { 'R', 'I', 'F', 'F' });
            writer.Write(36 + dataBytes);
            writer.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
            writer.Write(16);                  // fmt chunk size
            writer.Write((short)1);            // PCM
            writer.Write((short)2);            // stereo
            writer.Write(sampleRate);
            writer.Write(sampleRate * 4);      // byte rate: 2 ch x 2 bytes
            writer.Write((short)4);            // block align
            writer.Write((short)16);           // bits per sample
            writer.Write(new[] { 'd', 'a', 't', 'a' });
            writer.Write(dataBytes);

            foreach (float sample in interleaved)
            {
                float clamped = sample < -1f ? -1f : sample > 1f ? 1f : sample;
                writer.Write((short)(clamped * short.MaxValue));
            }

            writer.Flush();
            return stream.ToArray();
        }

        // ---- Voices -----------------------------------------------------------------

        /// <summary>
        /// Renders one note into the mix. Indices wrap modulo the loop length, which is
        /// what makes the loop seamless. <paramref name="timbre"/> blends in overtones
        /// (0 = pure sine, 1 = bright). Attack/release are fractions of the duration
        /// for release, seconds for attack.
        /// </summary>
        private static void AddNote(float[] mix, int totalPerChannel, int start, float freq, int duration,
                                    float volume, float pan, float attack, float release, float timbre)
        {
            float left = volume * (0.5f * (1f - pan) + 0.5f);
            float right = volume * (0.5f * (1f + pan) + 0.5f);
            int attackSamples = Math.Max(1, (int)(attack * SampleRate));
            int releaseStart = (int)(duration * (1f - release));
            float releaseLength = Math.Max(1, duration - releaseStart);

            for (int i = 0; i < duration; i++)
            {
                double t = i / (double)SampleRate;
                double phase = 2.0 * Math.PI * freq * t;
                float wave = (float)(Math.Sin(phase)
                                     + timbre * 0.5 * Math.Sin(phase * 2.0)
                                     + timbre * 0.2 * Math.Sin(phase * 3.0));
                wave /= 1f + timbre * 0.7f;

                float env = i < attackSamples
                    ? i / (float)attackSamples
                    : i > releaseStart
                        ? Math.Max(0f, 1f - (i - releaseStart) / releaseLength)
                        : 1f;

                int idx = ((start + i) % totalPerChannel) * 2;
                mix[idx] += wave * env * left;
                mix[idx + 1] += wave * env * right;
            }
        }

        private static void AddHat(float[] mix, int totalPerChannel, int start, int duration, float volume)
        {
            for (int i = 0; i < duration; i++)
            {
                int n = start + i;
                n = (n << 13) ^ n;
                float noise = 1f - ((n * (n * n * 15731 + 789221) + 1376312589) & 0x7fffffff) / 1073741824f;
                float env = 1f - i / (float)duration;

                int idx = ((start + i) % totalPerChannel) * 2;
                float value = noise * env * env * volume;
                mix[idx] += value;
                mix[idx + 1] += value;
            }
        }

        private static void Normalize(float[] mix, float target)
        {
            float peak = 0f;
            for (int i = 0; i < mix.Length; i++)
            {
                float abs = Math.Abs(mix[i]);
                if (abs > peak) peak = abs;
            }
            if (peak <= target || peak <= 0f)
                return;

            float scale = target / peak;
            for (int i = 0; i < mix.Length; i++)
                mix[i] *= scale;
        }

        private static ChapterStyle StyleFor(int chapter)
        {
            int index = chapter < 0 ? 0 : chapter >= Styles.Length ? Styles.Length - 1 : chapter;
            return Styles[index];
        }

        private static float MidiToFreq(int midi) => (float)(440.0 * Math.Pow(2.0, (midi - 69) / 12.0));

        private static uint NextRandom(ref uint state)
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return state;
        }
    }
}
