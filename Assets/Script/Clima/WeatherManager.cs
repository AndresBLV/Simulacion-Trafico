using System.Collections;
using UnityEngine;

public class WeatherManager : MonoBehaviour
{
    public static WeatherManager Instance;

    [Header("Weather Settings")]
    public bool isRaining = false;
    [Range(0f, 1f)]
    public float rainIntensity = 0.5f;
    public float rainChangeInterval = 30f;
    public float minRainDuration = 10f;
    public float maxRainDuration = 60f;

    [Header("Rain Effects")]
    public GameObject rainParticleEffect;
    public Material wetRoadMaterial;
    public Material dryRoadMaterial;
    public MeshRenderer roadMeshRenderer;

    [Header("Physics Parameters")]
    public float dryFriction = 1.0f;
    public float wetFriction = 0.7f;
    public float dryMotorForceMultiplier = 1.0f;
    public float wetMotorForceMultiplier = 0.8f;
    public float drySteeringMultiplier = 1.0f;
    public float wetSteeringMultiplier = 0.9f;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        StartCoroutine(WeatherChangeRoutine());
    }

    private IEnumerator WeatherChangeRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(rainChangeInterval);

            // Decidir si cambiar el clima
            if (Random.value > 0.7f) // 30% de probabilidad de cambio
            {
                isRaining = !isRaining;
                
                if (isRaining)
                {
                    rainIntensity = Random.Range(0.3f, 1.0f);
                    float rainDuration = Random.Range(minRainDuration, maxRainDuration);
                    StartCoroutine(RainDurationRoutine(rainDuration));
                }
                
                UpdateWeatherEffects();
                Debug.Log($"Weather changed: {(isRaining ? "Raining" : "Clear")}, Intensity: {rainIntensity}");
            }
        }
    }

    private IEnumerator RainDurationRoutine(float duration)
    {
        yield return new WaitForSeconds(duration);
        isRaining = false;
        rainIntensity = 0f;
        UpdateWeatherEffects();
        Debug.Log("Rain stopped");
    }

    private void UpdateWeatherEffects()
    {
        // Activar/desactivar efecto de partículas de lluvia
        if (rainParticleEffect != null)
        {
            rainParticleEffect.SetActive(isRaining);
            if (isRaining)
            {
                var emission = rainParticleEffect.GetComponent<ParticleSystem>().emission;
                emission.rateOverTime = 100 * rainIntensity;
            }
        }

        // Cambiar material de la carretera
        if (roadMeshRenderer != null && wetRoadMaterial != null && dryRoadMaterial != null)
        {
            roadMeshRenderer.material = isRaining ? wetRoadMaterial : dryRoadMaterial;
        }
    }

    public float GetFrictionFactor()
    {
        return Mathf.Lerp(dryFriction, wetFriction, rainIntensity);
    }

    public float GetMotorForceMultiplier()
    {
        return Mathf.Lerp(dryMotorForceMultiplier, wetMotorForceMultiplier, rainIntensity);
    }

    public float GetSteeringMultiplier()
    {
        return Mathf.Lerp(drySteeringMultiplier, wetSteeringMultiplier, rainIntensity);
    }

    public float GetVisibilityFactor()
    {
        return Mathf.Lerp(1.0f, 0.7f, rainIntensity);
    }
}