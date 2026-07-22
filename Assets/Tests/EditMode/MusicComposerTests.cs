using Match3.Core;
using NUnit.Framework;

namespace Match3.Tests
{
    /// <summary>
    /// The composer's contract: deterministic bytes, exact loop length, bounded
    /// samples, and distinct moods per chapter. (Musicality is judged by ear —
    /// these tests pin the properties the runtime and the loop seam depend on.)
    /// </summary>
    public sealed class MusicComposerTests
    {
        [Test]
        public void SameChapterProducesIdenticalBytes()
        {
            byte[] first = MusicComposer.ComposeWav(1);
            byte[] second = MusicComposer.ComposeWav(1);

            Assert.AreEqual(Hash(first), Hash(second));
            Assert.AreEqual(first.Length, second.Length);
        }

        [Test]
        public void DifferentChaptersProduceDifferentMusic()
        {
            Assert.AreNotEqual(Hash(MusicComposer.ComposeWav(0)), Hash(MusicComposer.ComposeWav(1)));
            Assert.AreNotEqual(Hash(MusicComposer.ComposeWav(1)), Hash(MusicComposer.ComposeWav(2)));
        }

        [Test]
        public void ChaptersBeyondTheTableReuseTheLastMoodDeterministically()
        {
            // Chapter 99 must not throw and must stay deterministic.
            Assert.AreEqual(Hash(MusicComposer.ComposeWav(99)), Hash(MusicComposer.ComposeWav(99)));
        }

        [Test]
        public void LoopLengthIsExactlyEightBars()
        {
            for (int chapter = 0; chapter < 4; chapter++)
            {
                float[] stereo = MusicComposer.ComposeStereo(chapter);
                int expected = MusicComposer.TotalSamplesPerChannel(chapter) * 2;
                Assert.AreEqual(expected, stereo.Length, $"chapter {chapter}");

                int perBeat = MusicComposer.SamplesPerBeat(chapter);
                Assert.AreEqual(perBeat * MusicComposer.BeatsPerBar * MusicComposer.Bars,
                                MusicComposer.TotalSamplesPerChannel(chapter), $"chapter {chapter} beats");
            }
        }

        [Test]
        public void SamplesStayWithinUnitRange()
        {
            float[] stereo = MusicComposer.ComposeStereo(0);
            for (int i = 0; i < stereo.Length; i++)
            {
                if (stereo[i] < -1f || stereo[i] > 1f)
                    Assert.Fail($"sample {i} out of range: {stereo[i]}");
            }
            Assert.Pass();
        }

        [Test]
        public void WavHeaderDeclaresStereoPcm()
        {
            byte[] wav = MusicComposer.ComposeWav(0);
            Assert.AreEqual((byte)'R', wav[0]);
            Assert.AreEqual(1, wav[20]); // PCM
            Assert.AreEqual(2, wav[22]); // stereo
        }

        private static ulong Hash(byte[] data)
        {
            // FNV-1a 64 — cheap, deterministic, good enough to compare renders.
            ulong hash = 14695981039346656037UL;
            foreach (byte b in data)
            {
                hash ^= b;
                hash *= 1099511628211UL;
            }
            return hash;
        }
    }
}
