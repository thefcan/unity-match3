using System.Collections.Generic;
using UnityEngine;

namespace Match3.Game
{
    /// <summary>Every sound the game can make — names match the WAVs in Resources/Audio.</summary>
    public enum Sfx
    {
        Swap,
        Pop,
        SpecialCreate,
        LineClear,
        WrappedBlast,
        ColorBomb,
        Shuffle,
        Win,
        Lose,
        Button,
    }

    /// <summary>
    /// Lazy, self-building audio player: the first Play() creates a DontDestroyOnLoad
    /// host with a small round-robin pool of AudioSources (overlapping pops never cut
    /// each other off) and loads all clips from Resources/Audio. No scene wiring.
    /// Cascade pops pass a rising pitch for the classic combo feel.
    /// </summary>
    public sealed class AudioManager : MonoBehaviour
    {
        private const int SourceCount = 6;

        private static AudioManager _instance;

        private readonly Dictionary<Sfx, AudioClip> _clips = new Dictionary<Sfx, AudioClip>();
        private AudioSource[] _sources;
        private int _nextSource;

        /// <summary>Settings gate — initialized from <see cref="Prefs.SfxOn"/> at boot.</summary>
        public static bool SfxEnabled = true;

        public static void Play(Sfx sfx, float pitch = 1f, float volume = 1f)
        {
            if (!SfxEnabled)
                return; // early-out before Instance so a muted app never even builds the pool
            Instance.PlayClip(sfx, pitch, volume);
        }

        private static AudioManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject(nameof(AudioManager));
                    DontDestroyOnLoad(go);
                    _instance = go.AddComponent<AudioManager>();
                    _instance.Build();
                }
                return _instance;
            }
        }

        private void Build()
        {
            _sources = new AudioSource[SourceCount];
            for (int i = 0; i < SourceCount; i++)
            {
                _sources[i] = gameObject.AddComponent<AudioSource>();
                _sources[i].playOnAwake = false;
            }

            _clips[Sfx.Swap] = Resources.Load<AudioClip>("Audio/swap");
            _clips[Sfx.Pop] = Resources.Load<AudioClip>("Audio/pop");
            _clips[Sfx.SpecialCreate] = Resources.Load<AudioClip>("Audio/special_create");
            _clips[Sfx.LineClear] = Resources.Load<AudioClip>("Audio/line_clear");
            _clips[Sfx.WrappedBlast] = Resources.Load<AudioClip>("Audio/wrapped_blast");
            _clips[Sfx.ColorBomb] = Resources.Load<AudioClip>("Audio/color_bomb");
            _clips[Sfx.Shuffle] = Resources.Load<AudioClip>("Audio/shuffle");
            _clips[Sfx.Win] = Resources.Load<AudioClip>("Audio/win");
            _clips[Sfx.Lose] = Resources.Load<AudioClip>("Audio/lose");
            _clips[Sfx.Button] = Resources.Load<AudioClip>("Audio/button");
        }

        private void PlayClip(Sfx sfx, float pitch, float volume)
        {
            if (!_clips.TryGetValue(sfx, out AudioClip clip) || clip == null)
                return; // clips not generated yet — stay silent rather than throw

            AudioSource source = _sources[_nextSource];
            _nextSource = (_nextSource + 1) % _sources.Length;

            source.pitch = pitch;
            source.PlayOneShot(clip, volume);
        }
    }
}
