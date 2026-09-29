using UnityEngine;

// Add to any object with a Sprite Renderer that uses the AR/PinOverlay material.
// Makes the material show THIS object's sprite (so Icon shows the pin, Label shows the pill).
[ExecuteAlways]
[RequireComponent(typeof(SpriteRenderer))]
public class OverlaySprite : MonoBehaviour
{
    static readonly int MainTex = Shader.PropertyToID("_MainTex");
    SpriteRenderer sr;
    MaterialPropertyBlock mpb;

    void OnEnable() { Apply(); }
    void OnValidate() { Apply(); }
    void LateUpdate() { Apply(); }

    void Apply()
    {
        if (!sr) sr = GetComponent<SpriteRenderer>();
        if (!sr || !sr.sprite) return;
        if (mpb == null) mpb = new MaterialPropertyBlock();
        sr.GetPropertyBlock(mpb);
        mpb.SetTexture(MainTex, sr.sprite.texture);
        sr.SetPropertyBlock(mpb);
    }
}
