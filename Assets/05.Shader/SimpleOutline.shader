Shader "Custom/CenterOutline"
{
    Properties
    {
        _Outline ("Outline Width", Range(0, 0.2)) = 0.03
        _OutlineColor ("Outline Color", Color) = (0,0,0,1)
    }

    SubShader
    {
        Tags { "Queue"="Geometry+1" "RenderType"="Opaque" }

        Pass
        {
            Cull Front

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float3 vertex : POSITION;
                float4 color : COLOR;
                float3 center : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 color : COLOR;
                float3 center : TEXCOORD0;
            };

            float _Outline;
            float4 _OutlineColor;

            v2f vert(appdata v)
            {
                v2f o;

                float3 dir = v.vertex - v.center;
                dir *= (1 + _Outline);
                float3 expanded = dir + v.center;

                o.pos = UnityObjectToClipPos(expanded);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                return _OutlineColor;
            }
            ENDCG
        }
    }
}