using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Match3.View
{
    /// <summary>
    /// Runtime-built juice: a shared ParticleSystem for candy-coloured bursts and a
    /// camera-shake coroutine. Lazy singleton, no scene wiring — the first call
    /// constructs everything, and the instance dies with its scene (no DontDestroy:
    /// particles are per-board, and a fresh scene rebuilds cheaply).
    /// </summary>
    public sealed class EffectsView : MonoBehaviour
    {
        private static EffectsView _instance;

        private ParticleSystem _burst;
        private Coroutine _shake;

        private static EffectsView Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject(nameof(EffectsView));
                    _instance = go.AddComponent<EffectsView>();
                    _instance.Build();
                }
                return _instance;
            }
        }

        /// <summary>A small confetti burst in the candy's colour where it popped.</summary>
        public static void TileBurst(Vector3 position, Color color, int count = 12)
        {
            if (Match3.Game.Prefs.ReducedMotionOn)
                count = Mathf.Max(1, count / 3);
            Instance.Emit(position, color, count, 2.6f);
        }

        /// <summary>A bigger, faster burst for detonation origins.</summary>
        public static void BlastBurst(Vector3 position, Color color)
        {
            int count = Match3.Game.Prefs.ReducedMotionOn ? 10 : 34;
            Instance.Emit(position, color, count, 5f);
        }

        /// <summary>Bumps the camera briefly — wrapped blasts, bombs, big combos.</summary>
        public static void Shake(float amplitude = 0.12f, float duration = 0.18f)
        {
            if (Match3.Game.Prefs.ReducedMotionOn)
                return; // accessibility: camera stays still
            Instance.StartShake(amplitude, duration);
        }

        // ---- Detonation connective tissue (pooled sprite quads, sorting order 8:
        // over the tiles, under the particles). All skipped under reduced motion —
        // the existing (already reduced) bursts remain the cue. ---------------------

        /// <summary>
        /// A beam sweeping a lane from the detonation origin, its tip riding the
        /// same cells-per-stagger speed as the staggered pops so front and pop
        /// always arrive together.
        /// </summary>
        public static void LaneBeam(Vector3 origin, Vector3 direction, float worldLength, float worldSpeed, float width, Color color)
        {
            if (Match3.Game.Prefs.ReducedMotionOn || worldLength <= 0.01f || worldSpeed <= 0f)
                return;
            Instance.StartCoroutine(Instance.BeamRoutine(origin, direction.normalized, worldLength, worldSpeed, width, color));
        }

        /// <summary>An expanding ring under a wrapped blast (radius in world units).</summary>
        public static void BlastRing(Vector3 origin, float worldRadius, Color color)
        {
            if (Match3.Game.Prefs.ReducedMotionOn)
                return;
            Instance.StartCoroutine(Instance.RingRoutine(origin, worldRadius, color));
        }

        /// <summary>Thin tendrils from a colour bomb to (a sample of) its victims.</summary>
        public static void Streaks(Vector3 origin, IReadOnlyList<Vector3> targets, Color color)
        {
            if (Match3.Game.Prefs.ReducedMotionOn || targets.Count == 0)
                return;
            Instance.StartCoroutine(Instance.StreaksRoutine(origin, targets, color));
        }

        private readonly Stack<SpriteRenderer> _quadPool = new Stack<SpriteRenderer>();
        private Sprite _pillSprite;
        private Sprite _ringSprite;

        private SpriteRenderer GetQuad(Sprite sprite, Color color)
        {
            SpriteRenderer quad = null;
            while (_quadPool.Count > 0 && quad == null)
                quad = _quadPool.Pop(); // drain destroyed instances
            if (quad == null)
            {
                var go = new GameObject("EffectQuad", typeof(SpriteRenderer));
                go.transform.SetParent(transform, false);
                quad = go.GetComponent<SpriteRenderer>();
                quad.sortingOrder = 8;
            }
            quad.sprite = sprite;
            quad.color = color;
            quad.gameObject.SetActive(true);
            return quad;
        }

        private void ReleaseQuad(SpriteRenderer quad)
        {
            if (quad == null)
                return;
            quad.gameObject.SetActive(false);
            _quadPool.Push(quad);
        }

        private Sprite PillSprite => _pillSprite != null ? _pillSprite : _pillSprite = Resources.Load<Sprite>("UI/ui_pill");
        private Sprite RingSprite => _ringSprite != null ? _ringSprite : _ringSprite = Resources.Load<Sprite>("UI/ui_round_outline");

        private IEnumerator BeamRoutine(Vector3 origin, Vector3 dir, float length, float speed, float width, Color color)
        {
            Sprite sprite = PillSprite;
            if (sprite == null)
                yield break; // chrome not generated yet — bursts alone carry the beat
            SpriteRenderer quad = GetQuad(sprite, color);
            Vector2 spriteSize = sprite.bounds.size;
            quad.transform.rotation = Quaternion.FromToRotation(Vector3.right, dir);

            float duration = length / speed;
            float fadeStart = 0.6f;
            for (float t = 0f; t < 1f; t += Time.deltaTime / duration)
            {
                float tip = length * t;
                quad.transform.position = origin + dir * (tip * 0.5f);
                quad.transform.localScale = new Vector3(
                    Mathf.Max(0.01f, tip) / spriteSize.x, width / spriteSize.y, 1f);
                float alpha = t < fadeStart ? 0.85f : Mathf.Lerp(0.85f, 0f, (t - fadeStart) / (1f - fadeStart));
                quad.color = new Color(color.r, color.g, color.b, alpha);
                yield return null;
            }
            ReleaseQuad(quad);
        }

        private IEnumerator RingRoutine(Vector3 origin, float radius, Color color)
        {
            Sprite sprite = RingSprite;
            if (sprite == null)
                yield break;
            SpriteRenderer quad = GetQuad(sprite, color);
            quad.transform.rotation = Quaternion.identity;
            quad.transform.position = origin;
            float spriteExtent = sprite.bounds.size.x;

            const float duration = 0.22f;
            for (float t = 0f; t < 1f; t += Time.deltaTime / duration)
            {
                float scale = radius * 2f * Mathf.SmoothStep(0f, 1f, t) / spriteExtent;
                quad.transform.localScale = new Vector3(scale, scale, 1f);
                quad.color = new Color(color.r, color.g, color.b, 0.8f * (1f - t));
                yield return null;
            }
            ReleaseQuad(quad);
        }

        private IEnumerator StreaksRoutine(Vector3 origin, IReadOnlyList<Vector3> targets, Color color)
        {
            Sprite sprite = PillSprite;
            if (sprite == null)
                yield break;
            for (int i = 0; i < targets.Count; i++)
            {
                StartCoroutine(SingleStreak(origin, targets[i], color, sprite));
                if (i < targets.Count - 1)
                    yield return null; // ~one frame apart reads as a crackle
            }
        }

        private IEnumerator SingleStreak(Vector3 origin, Vector3 target, Color color, Sprite sprite)
        {
            SpriteRenderer quad = GetQuad(sprite, color);
            Vector3 delta = target - origin;
            float length = delta.magnitude;
            Vector2 spriteSize = sprite.bounds.size;
            quad.transform.rotation = Quaternion.FromToRotation(Vector3.right, delta.normalized);
            quad.transform.position = origin + delta * 0.5f;
            quad.transform.localScale = new Vector3(length / spriteSize.x, 0.12f / spriteSize.y, 1f);

            const float duration = 0.16f; // the converge vocabulary
            for (float t = 0f; t < 1f; t += Time.deltaTime / duration)
            {
                quad.color = new Color(color.r, color.g, color.b, 0.75f * (1f - t));
                yield return null;
            }
            ReleaseQuad(quad);
        }

        private void Build()
        {
            var go = new GameObject("Burst", typeof(ParticleSystem));
            go.transform.SetParent(transform, false);
            _burst = go.GetComponent<ParticleSystem>();

            ParticleSystem.MainModule main = _burst.main;
            main.playOnAwake = false;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
            main.startSpeed = 0f; // speed comes from Emit's velocity
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.18f);
            main.gravityModifier = 1.6f;
            main.maxParticles = 512;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            ParticleSystem.EmissionModule emission = _burst.emission;
            emission.enabled = false; // Emit() only

            // Sprites/Default renders tinted quads correctly under URP 2D.
            var renderer = _burst.GetComponent<ParticleSystemRenderer>();
            renderer.material = new Material(Shader.Find("Sprites/Default"));
            renderer.sortingOrder = 10;
        }

        private void Emit(Vector3 position, Color color, int count, float speed)
        {
            var emitParams = new ParticleSystem.EmitParams();
            for (int i = 0; i < count; i++)
            {
                float angle = Random.value * Mathf.PI * 2f;
                float magnitude = speed * (0.4f + Random.value * 0.6f);
                emitParams.position = position;
                emitParams.velocity = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * magnitude;
                emitParams.startColor = Color.Lerp(color, Color.white, Random.value * 0.35f);
                _burst.Emit(emitParams, 1);
            }
        }

        private Camera _camera;

        private void StartShake(float amplitude, float duration)
        {
            // Cached: Camera.main is a tag search. The instance dies with its scene,
            // so the cache can never point at a previous scene's camera.
            if (_camera == null)
                _camera = Camera.main;
            if (_camera == null)
                return;

            if (_shake != null)
                StopCoroutine(_shake);
            _shake = StartCoroutine(ShakeRoutine(_camera.transform, amplitude, duration));
        }

        private static IEnumerator ShakeRoutine(Transform target, float amplitude, float duration)
        {
            Vector3 origin = target.localPosition;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float falloff = 1f - t / duration;
                target.localPosition = origin + (Vector3)(Random.insideUnitCircle * amplitude * falloff);
                yield return null;
            }
            target.localPosition = origin;
        }
    }
}
