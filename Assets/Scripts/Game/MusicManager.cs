using System.Collections;
using Match3.Core;
using UnityEngine;

namespace Match3.Game
{
    /// <summary>
    /// Plays the chapter's generated loop (Resources/Audio/Music/chapterN) and
    /// crossfades when the campaign moves into a new chapter — the audio half of the
    /// ThemeCurve ambience drift. Lazy DontDestroyOnLoad singleton like AudioManager;
    /// no scene wiring. Missing clips (music not generated yet) just mean silence.
    /// Volume comes from <see cref="Prefs.MusicVolume"/>; win/lose stingers call
    /// <see cref="Duck"/> so the jingle reads over the bed.
    /// </summary>
    public sealed class MusicManager : MonoBehaviour
    {
        private const float CrossfadeSeconds = 1.5f;

        private static MusicManager _instance;

        private AudioSource _a;
        private AudioSource _b;
        private AudioSource _active;
        private int _chapter = -1;
        private Coroutine _fade;
        private Coroutine _duckRoutine;
        private float _cachedVolume;
        private float _duckScale = 1f;

        /// <summary>Starts (or keeps) the loop for the chapter this level belongs to.</summary>
        public static void PlayForLevel(int level)
        {
            int chapter = Mathf.Max(0, (Mathf.Max(1, level) - 1) / ThemeCurve.ChapterLength);
            Instance.PlayChapter(chapter);
        }

        /// <summary>Briefly lowers the music under a jingle (win/lose stingers).</summary>
        public static void Duck(float seconds = 2.2f)
        {
            if (_instance != null)
                _instance.StartDuck(seconds);
        }

        private static MusicManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject(nameof(MusicManager));
                    DontDestroyOnLoad(go);
                    _instance = go.AddComponent<MusicManager>();
                    _instance.Build();
                }
                return _instance;
            }
        }

        private void Build()
        {
            _a = BuildSource();
            _b = BuildSource();
            _active = _a;
            _cachedVolume = Prefs.MusicVolume;
            Prefs.Changed += RefreshVolume;
        }

        private AudioSource BuildSource()
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            return source;
        }

        private void OnDestroy()
        {
            Prefs.Changed -= RefreshVolume;
        }

        private void RefreshVolume()
        {
            _cachedVolume = Prefs.MusicVolume; // cached: Update must not hit PlayerPrefs per frame
        }

        private float CurrentVolume => _cachedVolume * _duckScale;

        private void Update()
        {
            // Steady-state volume tracking (slider drags, duck envelope). The
            // crossfade coroutine owns volumes while it runs.
            if (_fade == null && _active != null && _active.clip != null)
                _active.volume = CurrentVolume;
        }

        private void PlayChapter(int chapter)
        {
            if (chapter == _chapter)
                return;

            AudioClip clip = LoadChapterClip(chapter);
            if (clip == null)
                return; // nothing generated yet — stay as we are

            _chapter = chapter;
            if (_active.clip == clip)
                return;

            AudioSource next = _active == _a ? _b : _a;
            next.clip = clip;
            next.volume = 0f;
            next.Play();

            if (_fade != null)
                StopCoroutine(_fade);
            _fade = StartCoroutine(Crossfade(_active, next));
            _active = next;
        }

        /// <summary>The chapter's loop, falling back to the highest generated one below it.</summary>
        private static AudioClip LoadChapterClip(int chapter)
        {
            for (int c = chapter; c >= 0; c--)
            {
                var clip = Resources.Load<AudioClip>($"Audio/Music/chapter{c}");
                if (clip != null)
                    return clip;
            }
            return null;
        }

        private IEnumerator Crossfade(AudioSource from, AudioSource to)
        {
            // Unscaled time: a pause (timeScale 0) must not freeze the fade mid-way.
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / CrossfadeSeconds)
            {
                float volume = CurrentVolume;
                from.volume = (1f - t) * volume;
                to.volume = t * volume;
                yield return null;
            }

            from.Stop();
            from.clip = null;
            to.volume = CurrentVolume;
            _fade = null;
        }

        private void StartDuck(float seconds)
        {
            if (_duckRoutine != null)
                StopCoroutine(_duckRoutine);
            _duckRoutine = StartCoroutine(DuckRoutine(seconds));
        }

        private IEnumerator DuckRoutine(float seconds)
        {
            _duckScale = 0.25f;
            yield return new WaitForSecondsRealtime(seconds * 0.6f);

            float recover = seconds * 0.4f;
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / recover)
            {
                _duckScale = Mathf.Lerp(0.25f, 1f, t);
                yield return null;
            }
            _duckScale = 1f;
            _duckRoutine = null;
        }
    }
}
