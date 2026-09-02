using UnityEngine;

namespace OfflineExtraction.UI
{
    [RequireComponent(typeof(Light))]
    public sealed class HubFireFlicker : MonoBehaviour
    {
        public float baseIntensity = 3.6f;
        public float baseRange = 6.2f;
        public float speed = 8f;
        private Light source;
        private float seed;

        private void Awake()
        {
            source = GetComponent<Light>();
            seed = Random.Range(0f, 100f);
        }

        private void Update()
        {
            float time = Time.time * speed;
            float slow = Mathf.PerlinNoise(seed, time * .11f) - .5f;
            float fast = Mathf.PerlinNoise(seed + 19f, time * .31f) - .5f;
            float flicker = slow * .55f + fast * .2f;
            source.intensity = Mathf.Max(.4f, baseIntensity + flicker);
            source.range = baseRange + flicker * .28f;
            source.color = Color.Lerp(new Color(1f, .2f, .035f), new Color(1f, .42f, .09f), Mathf.Clamp01(.55f + flicker));
        }
    }
}
