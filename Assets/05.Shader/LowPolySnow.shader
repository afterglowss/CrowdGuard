Shader "Custom/LowPolySnow"
{
    Properties { _Color ("Tint", Color) = (1,1,1,1) }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Cull Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 pos : POSITION; float4 color : COLOR; };
            struct Varyings   { float4 pos : SV_POSITION; float4 color : COLOR; };
            float4 _Color;

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.pos   = TransformObjectToHClip(IN.pos.xyz);
                OUT.color = IN.color * _Color;
                return OUT;
            }
            half4 frag(Varyings IN) : SV_Target { return IN.color; }
            ENDHLSL
        }
    }
}