using UnityEngine;

// Put on the Glow child (MeshRenderer or SpriteRenderer using an additive URP material).
// Pulses an HDR color so Bloom makes the pin look like it emits light.
public class PinGlow : MonoBehaviour
{
    [ColorUsage(true, true)] public Color glowColor = new Color(1f, 0.75f, 0.35f) * 2.5f;
    public float minIntensity = 0.6f, maxIntensity = 1.2f, pulseSpeed = 2f;
    public float minScale = 0.95f, maxScale = 1.1f;

    Renderer rend;
    MaterialPropertyBlock mpb;
    Vector3 baseScale;
    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

    void Start()
    {
        rend = GetComponent<Renderer>();
        mpb = new MaterialPropertyBlock();
        baseScale = transform.localScale;
    }

    void Update()
    {
        float t = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
        mpb.SetColor(BaseColor, glowColor * Mathf.Lerp(minIntensity, maxIntensity, t));
        rend.SetPropertyBlock(mpb);
        transform.localScale = baseScale * Mathf.Lerp(minScale, maxScale, t);
    }
}
