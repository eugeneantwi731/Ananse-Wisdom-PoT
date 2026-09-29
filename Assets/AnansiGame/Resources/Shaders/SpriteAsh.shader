// Sprite shader that turns a pin to ash grey (used for locked pins). _Ash 0 = full colour, 1 = ash.
Shader "AnansiGame/SpriteAsh"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Ash ("Ash amount", Range(0,1)) = 1
        _AshColor ("Ash colour", Color) = (0.50, 0.49, 0.48, 1)
        _AshAlpha ("Ash opacity", Range(0,1)) = 0.7
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex; float _Ash; fixed4 _AshColor; float _AshAlpha;
            struct a2v { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            v2f vert (a2v v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.uv; o.color = v.color; return o; }
            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv);
                float g = dot(c.rgb, float3(0.299, 0.587, 0.114));
                float3 ash = lerp(float3(0.16, 0.15, 0.15), _AshColor.rgb, saturate(g * 1.15)); // dark lines stay dark, fills go soft ash
                c.rgb = lerp(c.rgb * i.color.rgb, ash, _Ash);
                c.a *= i.color.a * lerp(1, _AshAlpha, _Ash);
                return c;
            }
            ENDCG
        }
    }
}
