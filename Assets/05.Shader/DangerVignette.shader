Shader "Custom/DangerVignette"
{
    Properties
    {
        _Color       ("Color",        Color)         = (1, 0, 0, 1)
        _Intensity   ("Intensity",    Range(0, 1))   = 0
        _InnerRadius ("Inner Radius", Range(0, 1))   = 0.35
        _OuterRadius ("Outer Radius", Range(0, 1))   = 0.80
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"  = "UniversalPipeline"
            "Queue"           = "Transparent"
            "RenderType"      = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float  _Intensity;
                float  _InnerRadius;
                float  _OuterRadius;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // UV 중심(0.5, 0.5)에서 거리. Edge 중점 = 1.0
                float dist    = distance(IN.uv, float2(0.5, 0.5)) * 2.0;
                // innerRadius 이하 → 완전 투명 / outerRadius 이상 → 완전 불투명
                float vignette = smoothstep(_InnerRadius, _OuterRadius, dist);
                return half4(_Color.rgb, vignette * _Intensity);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
