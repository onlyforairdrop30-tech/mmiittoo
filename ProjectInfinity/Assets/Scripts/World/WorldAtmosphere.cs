using UnityEngine;

namespace ProjectInfinity.World
{
    /// <summary>
    /// Day/night cycle plus a light rain/storm weather state machine. Cheap enough for mobile:
    /// it drives light colour/intensity, ambient and fog only — no particle systems by default.
    /// </summary>
    public class WorldAtmosphere : MonoBehaviour
    {
        [Header("Sun")]
        public Light sun;
        public float dayLengthSeconds = 480f;
        [Range(0f, 24f)] public float startHour = 9f;

        [Header("Sky colours")]
        public Color daySky = new Color(0.45f, 0.68f, 0.95f);
        public Color duskSky = new Color(0.85f, 0.5f, 0.35f);
        public Color nightSky = new Color(0.04f, 0.06f, 0.14f);

        [Header("Weather")]
        public bool weatherEnabled = true;
        public float weatherCheckInterval = 45f;
        [Range(0f, 1f)] public float rainIntensity;

        public float Hour { get; private set; }
        public bool IsStorm { get; private set; }

        private float _timer;

        private void Start()
        {
            Hour = startHour;
            ApplyLighting();
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            Hour += 24f * (dt / Mathf.Max(30f, dayLengthSeconds));
            if (Hour >= 24f) Hour -= 24f;

            ApplyLighting();
            if (weatherEnabled) TickWeather(dt);
        }

        private void ApplyLighting()
        {
            if (sun != null)
            {
                float sunAngle = (Hour / 24f) * 360f - 90f;
                sun.transform.rotation = Quaternion.Euler(sunAngle, 170f, 0f);

                float daylight = Mathf.Clamp01(Mathf.Sin((Hour - 5.5f) / 13f * Mathf.PI));
                sun.intensity = Mathf.Lerp(0.05f, 1.15f, daylight) * (IsStorm ? 0.55f : 1f);
                sun.color = Color.Lerp(new Color(1f, 0.72f, 0.5f), Color.white, daylight);
            }

            Color sky = Hour < 6f || Hour > 20f
                ? nightSky
                : Color.Lerp(duskSky, daySky, Mathf.Clamp01(Mathf.Sin((Hour - 6f) / 12f * Mathf.PI)));

            if (IsStorm) sky = Color.Lerp(sky, new Color(0.22f, 0.24f, 0.28f), 0.65f);

            RenderSettings.ambientLight = sky * 0.85f;
            RenderSettings.fogColor = Color.Lerp(sky, Color.black, 0.15f);
        }

        private void TickWeather(float dt)
        {
            _timer += dt;
            if (_timer < weatherCheckInterval) return;
            _timer = 0f;

            float roll = Random.value;
            IsStorm = roll > 0.78f;
            rainIntensity = IsStorm ? Random.Range(0.55f, 0.9f) : (roll > 0.55f ? Random.Range(0.1f, 0.35f) : 0f);

            RenderSettings.fog = rainIntensity > 0.4f || Hour < 6f;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = Mathf.Lerp(60f, 20f, rainIntensity);
            RenderSettings.fogEndDistance = Mathf.Lerp(180f, 70f, rainIntensity);
        }

        public void SetHour(float hour)
        {
            Hour = Mathf.Repeat(hour, 24f);
            ApplyLighting();
        }
    }
}
