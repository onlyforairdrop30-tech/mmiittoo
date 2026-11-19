using UnityEngine;

namespace ProjectInfinity.Audio
{
    /// <summary>
    /// Synthesises all prototype audio at runtime (swept tones + noise), so the project ships no
    /// third-party sound files. Replaced by recorded/Blimb audio in the phase 5 art pass.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class ProceduralSfx : MonoBehaviour
    {
        public static ProceduralSfx Instance { get; private set; }

        private const int SampleRate = 44100;

        private AudioSource _source;
        private AudioClip _swing;
        private AudioClip _hit;
        private AudioClip _parry;
        private AudioClip _levelUp;
        private AudioClip _cast;

        private void Awake()
        {
            Instance = this;
            _source = GetComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.spatialBlend = 0f;

            _swing = BuildSweep("sfx_swing", 0.16f, 820f, 260f, 0.18f, 0.5f);
            _hit = BuildSweep("sfx_hit", 0.12f, 220f, 90f, 0.55f, 1f);
            _parry = BuildSweep("sfx_parry", 0.22f, 1400f, 1900f, 0.05f, 0.7f);
            _levelUp = BuildSweep("sfx_level", 0.55f, 480f, 960f, 0.02f, 0.35f);
            _cast = BuildSweep("sfx_cast", 0.4f, 300f, 1200f, 0.08f, 0.45f);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void PlaySwing() => Play(_swing, 0.45f);
        public void PlayHit() => Play(_hit, 0.55f);
        public void PlayParry() => Play(_parry, 0.5f);
        public void PlayLevelUp() => Play(_levelUp, 0.6f);
        public void PlayCast() => Play(_cast, 0.5f);

        private void Play(AudioClip clip, float volume)
        {
            if (_source != null && clip != null) _source.PlayOneShot(clip, volume);
        }

        private static AudioClip BuildSweep(string name, float seconds, float startHz, float endHz,
                                            float noise, float gain)
        {
            int sampleCount = Mathf.Max(64, Mathf.RoundToInt(seconds * SampleRate));
            var data = new float[sampleCount];
            var rng = new System.Random(name.GetHashCode());

            double phase = 0.0;
            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)SampleRate;
                float progress = i / (float)sampleCount;
                float frequency = Mathf.Lerp(startHz, endHz, progress);
                phase += 2.0 * System.Math.PI * frequency / SampleRate;

                float envelope = Mathf.Pow(1f - progress, 1.8f);
                float tone = (float)System.Math.Sin(phase);
                float hiss = (float)(rng.NextDouble() * 2.0 - 1.0) * noise;

                data[i] = (tone * (1f - noise) + hiss) * envelope * gain;
            }

            var clip = AudioClip.Create(name, sampleCount, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
