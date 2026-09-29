// Soft light shafts rising from the pot: fade with height and at the edges, and never reach the title.
Shader "Intro/Beam"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (1,0.77,0.35,0)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha One
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 _Color; float _IntroFadeTop; float _IntroFadeLen;
            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 n : TEXCOORD1; float3 v : TEXCOORD2; float4 sp : TEXCOORD3; };
            v2f vert (appdata i)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(i.vertex);
                o.uv = i.uv;
                o.n = UnityObjectToWorldNormal(i.normal);
                o.v = _WorldSpaceCameraPos - mul(unity_ObjectToWorld, i.vertex).xyz;
                o.sp = ComputeScreenPos(o.pos);
                return o;
            }
            fixed4 frag (v2f i) : SV_Target
            {
                float fromTop = (1.0 - i.sp.y / i.sp.w) * _ScreenParams.y;
                float safe = smoothstep(_IntroFadeTop, _IntroFadeTop + max(_IntroFadeLen, 1.0), fromTop);
                float h = pow(1.0 - i.uv.y, 1.6) * smoothstep(0.0, 0.08, i.uv.y);
                float e = pow(abs(dot(normalize(i.n), normalize(i.v))), 1.6);
                float al = saturate(h * e * _Color.a * 1.3 * safe);
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
