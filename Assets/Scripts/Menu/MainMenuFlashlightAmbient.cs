using System.Collections;
using UnityEngine;

public class MainMenuFlashlightAmbient : MonoBehaviour
{
    [Header("References")]
    [SerializeField] Light flashlight;
    [SerializeField] AudioSource flickerAudioSource;

    [Header("Baseline")]
    [SerializeField] bool forceLightEnabled = true;
    [SerializeField] bool forceNoShadows = true;
    [SerializeField] float baseIntensity = 1f;

    [Header("Random Flicker")]
    [SerializeField] bool enableRandomFlicker = true;
    [SerializeField] Vector2 gapBetweenFlickersSeconds = new Vector2(8f, 20f);
    [SerializeField] Vector2 flickerDurationSeconds = new Vector2(0.08f, 0.35f);
    [SerializeField, Min(1f)] float flickerSpeed = 35f;
    [SerializeField, Range(0f, 1f)] float minIntensityMultiplier = 0.2f;
    [SerializeField, Range(0f, 1f)] float maxIntensityMultiplier = 1f;
    [SerializeField, Range(0f, 1f)] float briefOffChancePerSample = 0.08f;

    Coroutine _flickerRoutine;

    void Awake()
    {
        if (flashlight == null)
        {
            Transform path = transform.Find("Player/PlayerCamera/Flashlight");
            if (path != null)
                flashlight = path.GetComponent<Light>();
        }

        if (flashlight == null)
            flashlight = GetComponentInChildren<Light>(true);

        if (flickerAudioSource == null && flashlight != null)
            flickerAudioSource = flashlight.GetComponent<AudioSource>();

        if (flashlight == null)
            return;

        if (forceNoShadows)
            flashlight.shadows = LightShadows.None;

        flashlight.intensity = Mathf.Max(0f, baseIntensity);
        flashlight.enabled = forceLightEnabled;
    }

    void OnEnable()
    {
        if (!enableRandomFlicker || flashlight == null)
            return;

        _flickerRoutine = StartCoroutine(FlickerLoopRoutine());
    }

    void OnDisable()
    {
        if (_flickerRoutine != null)
        {
            StopCoroutine(_flickerRoutine);
            _flickerRoutine = null;
        }

        if (flashlight != null)
        {
            flashlight.intensity = Mathf.Max(0f, baseIntensity);
            flashlight.enabled = forceLightEnabled;
        }
    }

    IEnumerator FlickerLoopRoutine()
    {
        while (true)
        {
            float gap = Random.Range(
                Mathf.Max(0f, Mathf.Min(gapBetweenFlickersSeconds.x, gapBetweenFlickersSeconds.y)),
                Mathf.Max(0f, Mathf.Max(gapBetweenFlickersSeconds.x, gapBetweenFlickersSeconds.y)));

            if (gap > 0f)
                yield return new WaitForSecondsRealtime(gap);

            if (flashlight == null || !forceLightEnabled)
                continue;

            float duration = Random.Range(
                Mathf.Max(0.01f, Mathf.Min(flickerDurationSeconds.x, flickerDurationSeconds.y)),
                Mathf.Max(0.01f, Mathf.Max(flickerDurationSeconds.x, flickerDurationSeconds.y)));

            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;

                float noise = Mathf.PerlinNoise(Time.unscaledTime * flickerSpeed, 0.17f);
                float multiplier = Mathf.Lerp(minIntensityMultiplier, maxIntensityMultiplier, noise);

                bool forceOff = Random.value < briefOffChancePerSample;
                flashlight.enabled = !forceOff;
                flashlight.intensity = Mathf.Max(0f, baseIntensity * multiplier);

                yield return null;
            }

            flashlight.enabled = forceLightEnabled;
            flashlight.intensity = Mathf.Max(0f, baseIntensity);
        }
    }
}
