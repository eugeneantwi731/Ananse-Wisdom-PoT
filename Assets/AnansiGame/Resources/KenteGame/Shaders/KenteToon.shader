// Cartoon shading + dark ink outline for the kente loom. Works in URP and the Built-in pipeline.
// Light direction comes from the global _KenteLightDir (set by KenteWeavingGame), so the loom looks the same in any scene.
Shader "KenteGame/Toon"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Texture", 2D) = "white" {}
        _Emission ("Emission", Color) = (0,0,0,0)
        _Outline ("Outline Width", Float) = 0.028
        _OutlineColor ("Outline Color", Color) = (0.239,0.141,0.086,1)
    }

    // ---------- URP ----------
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float4 _Color; float4 _MainTex_ST; float4 _Emission; float _Outline; float4 _OutlineColor;
        CBUFFER_END
        TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
        float4 _KenteLightDir;
        ENDHLSL

        Pass
        {
            Name "Toon"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            struct A { float4 pos : POSITION; float3 n : NORMAL; float2 uv : TEXCOORD0; };
            struct V { float4 pos : SV_POSITION; float3 n : TEXCOORD1; float2 uv : TEXCOORD0; };
            V vert (A i) { V o; o.pos = TransformObjectToHClip(i.pos.xyz); o.n = TransformObjectToWorldNormal(i.n); o.uv = TRANSFORM_TEX(i.uv, _MainTex); return o; }
            half4 frag (V i) : SV_Target
            {
                float3 L = normalize(_KenteLightDir.xyz + float3(0,0.0001,0));
                float d = dot(normalize(i.n), L);
                float s = d > 0.45 ? 1.0 : (d > -0.15 ? 0.74 : 0.5);
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * _Color;
                c.rgb = c.rgb * s + _Emission.rgb;
                return c;
            }
            ENDHLSL
        }
        Pass
        {
            Name "Outline"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Front
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            struct A { float4 pos : POSITION; float3 n : NORMAL; };
            struct V { float4 pos : SV_POSITION; };
            V vert (A i) { V o; o.pos = TransformObjectToHClip(i.pos.xyz + normalize(i.n) * _Outline); return o; }
            half4 frag (V i) : SV_Target { return _OutlineColor; }
            ENDHLSL
        }
    }

    // ---------- Built-in ----------
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 _Color; sampler2D _MainTex; float4 _MainTex_ST; float4 _Emission; float4 _KenteLightDir;
            struct A { float4 pos : POSITION; float3 n : NORMAL; float2 uv : TEXCOORD0; };
            struct V { float4 pos : SV_POSITION; float3 n : TEXCOORD1; float2 uv : TEXCOORD0; };
            V vert (A i) { V o; o.pos = UnityObjectToClipPos(i.pos); o.n = UnityObjectToWorldNormal(i.n); o.uv = TRANSFORM_TEX(i.uv, _MainTex); return o; }
            fixed4 frag (V i) : SV_Target
            {
                float3 L = normalize(_KenteLightDir.xyz + float3(0,0.0001,0));
                float d = dot(normalize(i.n), L);
                float s = d > 0.45 ? 1.0 : (d > -0.15 ? 0.74 : 0.5);
                fixed4 c = tex2D(_MainTex, i.uv) * _Color;
                c.rgb = c.rgb * s + _Emission.rgb;
                return c;
            }
            ENDCG
        }
        Pass
        {
            Cull Front
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float _Outline; float4 _OutlineColor;
            struct A { float4 pos : POSITION; float3 n : NORMAL; };
            struct V { float4 pos : SV_POSITION; };
            V vert (A i) { V o; o.pos = UnityObjectToClipPos(i.pos + float4(normalize(i.n) * _Outline, 0)); return o; }
            fixed4 frag (V i) : SV_Target { return _OutlineColor; }
            ENDCG
        }
    }
    FallBack Off
}
