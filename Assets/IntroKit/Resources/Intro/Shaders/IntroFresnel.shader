// Glowing rim shell drawn over the pot body.
Shader "Intro/Fresnel"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (1,0.69,0.25,1)
        _Strength ("Strength", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha One
        ZWrite Off
        Cull Back
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 _Color; float _Strength;
            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; };
            struct v2f { float4 pos : SV_POSITION; float3 n : TEXCOORD0; float3 v : TEXCOORD1; };
            v2f vert (appdata i)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(i.vertex);
                o.n = UnityObjectToWorldNormal(i.normal);
                o.v = _WorldSpaceCameraPos - mul(unity_ObjectToWorld, i.vertex).xyz;
                return o;
            }
            fixed4 frag (v2f i) : SV_Target
            {
                float f = 1.0 - saturate(dot(normalize(i.n), normalize(i.v)));
                float rim = pow(f, 2.2);
                float al = saturate((rim * 1.6 + 0.18) * _Strength);
                #ifndef UNITY_COLORSPACE_GAMMA
                al = pow(al, 2.2);
                #endif
                float3 col = _Color.rgb;
                #ifndef UNITY_COLORSPACE_GAMMA
                col = pow(col, 2.2);
                #endif
                return fixed4(col, al);
            }
            ENDCG
        }
    }
}
