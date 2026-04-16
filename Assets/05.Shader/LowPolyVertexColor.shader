// LowPolyVertexColor.shader
// Drop in any folder in your project (Assets/Shaders/ recommended).
// Uses vertex colors baked by the Mountain Sculptor tool.
// Supports both Built-in Pipeline and can be adapted for URP/HDRP.

Shader "Custom/LowPolyVertexColor"
{
    Properties
    {
        _Glossiness ("Smoothness", Range(0,1)) = 0.1
        _Metallic    ("Metallic",  Range(0,1)) = 0.0
        _AmbientOcc  ("Ambient Occlusion", Range(0,1)) = 0.5
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        struct Input
        {
            float4 color : COLOR;  // vertex color from mesh
        };

        half _Glossiness;
        half _Metallic;
        half _AmbientOcc;

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            o.Albedo     = IN.color.rgb;
            o.Metallic   = _Metallic;
            o.Smoothness = _Glossiness;
            o.Occlusion  = _AmbientOcc;
            o.Alpha      = 1.0;
        }
        ENDCG
    }

    FallBack "Diffuse"
}
