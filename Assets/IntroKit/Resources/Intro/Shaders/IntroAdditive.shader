// Additive glow. Optional "title fade": hides anything above a screen line (set globally by the intro).
Shader "Intro/Additive"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        [HDR] _Color ("Color", Color) = (1,1,1,1)
        _TitleFade ("Title Fade", Float) = 0
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
            sampler2D _MainTex; float4 _MainTex_ST; float4 _Color; float _TitleFade;
            float _IntroFadeTop; float _IntroFadeLen;
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; float4 sp : TEXCOORD1; };
            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color * _Color;
                o.sp = ComputeScreenPos(o.pos);
                return o;
            }
            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv) * i.color;
                float fromTop = (1.0 - i.sp.y / i.sp.w) * _ScreenParams.y;
                float safe = smoothstep(_IntroFadeTop, _IntroFadeTop + max(_IntroFadeLen, 1.0), fromTop);
                c.a *= lerp(1.0, safe, _TitleFade);
                #ifndef UNITY_COLORSPACE_GAMMA
                c.a = pow(saturate(c.a), 2.2);
                #endif
                return c;
            }
            ENDCG
        }
    }
}
