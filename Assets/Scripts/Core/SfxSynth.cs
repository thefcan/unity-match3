using System;
using System.IO;

namespace Match3.Core
{
    /// <summary>
    /// Procedural sound-effect synthesizer — pure math, no UnityEngine, shared by the
    /// editor menu and CLI tooling exactly like <see cref="CandyArtist"/>. Every clip
    /// is built from sine partials, deterministic noise and simple envelopes, then
    /// packed to 16-bit mono WAV with <see cref="ToWav"/>. Deterministic: same call,
    /// same bytes.
    /// </summary>
    public static class SfxSynth
    {
        public const int SampleRate = 44100;

        public static float[] Swap() =>
            Render(0.09f, t => Sine(Slide(300f, 380f, t, 0.09f), t) * Envelope(t, 0.005f, 0.09f));

        public static float[] Pop() =>
            Render(0.14f, t => (Sine(Slide(650f, 320f, t, 0.14f), t) * 0.9f + Noise(t) * 0.1f) * Envelope(t, 0.004f, 0.14f));

        public static float[] SpecialCreate() =>
            Render(0.3f, t =>
            {
                float note = t < 0.1f ? 440f : t < 0.2f ? 587f : 784f;
                return Sine(note, t) * 0.8f * Envelope(t % 0.1f, 0.005f, 0.1f) * Envelope(t, 0.005f, 0.3f);
            });

        public static float[] LineClear() =>
            Render(0.32f, t => (Noise(t) * 0.5f * (1f - t / 0.32f) + Sine(Slide(900f, 200f, t, 0.32f), t) * 0.5f)
                               * Envelope(t, 0.01f, 0.32f));

        public static float[] WrappedBlast() =>
            Render(0.4f, t => (Sine(Slide(130f, 55f, t, 0.4f), t) * 0.75f + Noise(t) * 0.45f * (1f - t / 0.4f))
                              * Envelope(t, 0.003f, 0.4f));

        public static float[] ColorBomb() =>
            Render(0.65f, t =>
            {
                float vibrato = 1f + 0.01f * Sine(6f, t);
                float chord = Sine(523f * vibrato, t) + Sine(659f * vibrato, t) + Sine(784f * vibrato, t);
                return chord / 3f * Envelope(t, 0.02f, 0.65f);
            });

        public static float[] Shuffle() =>
            Render(0.3f, t =>
            {
                float burst = (t % 0.1f) < 0.04f ? 1f : 0f;
                return Noise(t) * burst * 0.7f * Envelope(t, 0.005f, 0.3f);
            });

        public static float[] Win() =>
            Render(0.85f, t =>
            {
                float note = t < 0.18f ? 523f : t < 0.36f ? 659f : t < 0.54f ? 784f : 1047f;
                float segment = t < 0.54f ? t % 0.18f : t - 0.54f;
                float segmentLength = t < 0.54f ? 0.18f : 0.31f;
                return Sine(note, t) * 0.85f * Envelope(segment, 0.01f, segmentLength) * Envelope(t, 0.01f, 0.85f);
            });

        public static float[] Lose() =>
            Render(0.7f, t =>
            {
                float note = t < 0.25f ? 392f : t < 0.5f ? 311f : 233f;
                float segment = t % 0.25f;
                return Sine(note, t) * 0.8f * Envelope(segment, 0.02f, 0.25f) * Envelope(t, 0.02f, 0.7f);
            });

        public static float[] Button() =>
            Render(0.05f, t => Sine(820f, t) * Envelope(t, 0.002f, 0.05f));

        /// <summary>Packs mono samples into a complete 16-bit PCM WAV file.</summary>
        public static byte[] ToWav(float[] samples, int sampleRate = SampleRate)
        {
            if (samples == null) throw new ArgumentNullException(nameof(samples));

            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);

            int dataBytes = samples.Length * 2;
            writer.Write(new[] { 'R', 'I', 'F', 'F' });
            writer.Write(36 + dataBytes);
            writer.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
            writer.Write(16);              // fmt chunk size
            writer.Write((short)1);        // PCM
            writer.Write((short)1);        // mono
            writer.Write(sampleRate);
            writer.Write(sampleRate * 2);  // byte rate
            writer.Write((short)2);        // block align
            writer.Write((short)16);       // bits per sample
            writer.Write(new[] { 'd', 'a', 't', 'a' });
            writer.Write(dataBytes);

            foreach (float sample in samples)
            {
                float clamped = sample < -1f ? -1f : sample > 1f ? 1f : sample;
                writer.Write((short)(clamped * short.MaxValue));
            }

            writer.Flush();
            return stream.ToArray();
        }

        // ---- Building blocks ----------------------------------------------------------

        private static float[] Render(float seconds, Func<float, float> wave)
        {
            var samples = new float[(int)(seconds * SampleRate)];
            for (int i = 0; i < samples.Length; i++)
                samples[i] = wave(i / (float)SampleRate) * 0.8f;
            return samples;
        }

        private static float Sine(float frequency, float t) =>
            (float)Math.Sin(2.0 * Math.PI * frequency * t);

        /// <summary>Linear frequency slide from <paramref name="from"/> to <paramref name="to"/> over the clip.</summary>
        private static float Slide(float from, float to, float t, float duration) =>
            from + (to - from) * (t / duration);

        /// <summary>Deterministic white-ish noise (hash of the sample index — no RNG state).</summary>
        private static float Noise(float t)
        {
            int n = (int)(t * SampleRate);
            n = (n << 13) ^ n;
            return 1f - ((n * (n * n * 15731 + 789221) + 1376312589) & 0x7fffffff) / 1073741824f;
        }

        /// <summary>Attack-then-linear-decay envelope.</summary>
        private static float Envelope(float t, float attack, float duration)
        {
            if (t < 0f || t > duration) return 0f;
            if (t < attack) return t / attack;
            return 1f - (t - attack) / (duration - attack);
        }
    }
}
