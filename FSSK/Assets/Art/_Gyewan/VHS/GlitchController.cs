using UnityEngine;

public class GlitchController : MonoBehaviour
{
    public Material mat;

    [Header("Effect Settings")]
    [Range(0, 1)] public float intensity;

    public float noiseAmount;
    public float glitchStrength;
    public float scanLinesStrength;

    void Update()
    {
        if (mat == null) return;

        mat.SetFloat("_Intensity", intensity);

        mat.SetFloat("_NoiseAmount", noiseAmount);
        mat.SetFloat("_GlitchStrength", glitchStrength);
        mat.SetFloat("_ScanLineStrength", scanLinesStrength);
    }

    public void SetEffectActive(bool active)
    {
        intensity = active ? 1f : 0f;
    }
}
