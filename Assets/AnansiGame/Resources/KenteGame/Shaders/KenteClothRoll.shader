// Cloth wound round the loom's beams: solid where the texture has cloth, see-through elsewhere, lit softly.
Shader "KenteGame/ClothRoll"
{
    Properties { _MainTex ("Texture", 2D) = "white" {} }
    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            ZWrite On Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial) float4 _MainTex_ST; CBUFFER_END
            float4 _KenteLightDir;
            struct A { float4 pos : POSITION; float3 n : NORMAL; float2 uv : TEXCOORD0; };
            struct V { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 n : TEXCOORD1; };
            V vert (A i) { V o; o.pos = TransformObjectToHClip(i.pos.xyz); o.uv = i.uv; o.n = TransformObjectToWorldNormal(i.n); return o; }
            half4 frag (V i) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv); clip(c.a - 0.5);
                float3 L = _KenteLightDir.xyz; L = dot(L, L) > 0.0001 ? normalize(L) : float3(0, 0.6, 0.8);
                c.rgb *= 0.62 + 0.38 * saturate(abs(dot(normalize(i.n), L)));
                return c;
            }
            ENDHLSL
        }
    }
    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" }
        Pass
        {
            ZWrite On Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex; float4 _KenteLightDir;
            struct A { float4 pos : POSITION; float3 n : NORMAL; float2 uv : TEXCOORD0; };
            struct V { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 n : TEXCOORD1; };
            V vert (A i) { V o; o.pos = UnityObjectToClipPos(i.pos); o.uv = i.uv; o.n = UnityObjectToWorldNormal(i.n); return o; }
            fixed4 frag (V i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv); clip(c.a - 0.5);
                float3 L = _KenteLightDir.xyz; L = dot(L, L) > 0.0001 ? normalize(L) : float3(0, 0.6, 0.8);
                c.rgb *= 0.62 + 0.38 * saturate(abs(dot(normalize(i.n), L)));
                return c;
            }
            ENDCG
        }
    }
    FallBack Off
}
