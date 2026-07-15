using System.Collections;
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
            Instance.Emit(position, color, count, 2.6f);
        }

        /// <summary>A bigger, faster burst for detonation origins.</summary>
        public static void BlastBurst(Vector3 position, Color color)
        {
            Instance.Emit(position, color, 34, 5f);
        }

        /// <summary>Bumps the camera briefly — wrapped blasts, bombs, big combos.</summary>
        public static void Shake(float amplitude = 0.12f, float duration = 0.18f)
        {
            Instance.StartShake(amplitude, duration);
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

        private void StartShake(float amplitude, float duration)
        {
            Camera camera = Camera.main;
            if (camera == null)
                return;

            if (_shake != null)
                StopCoroutine(_shake);
            _shake = StartCoroutine(ShakeRoutine(camera.transform, amplitude, duration));
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
