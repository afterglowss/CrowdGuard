#ifndef AMBIENT_SH_INCLUDED
#define AMBIENT_SH_INCLUDED

void GetAmbientSH_float(out float3 AmbientColor)
{
#if defined(SHADERGRAPH_PREVIEW)
    AmbientColor = float3(0.5, 0.55, 0.6);
#else
    AmbientColor = SampleSH(float3(0.0, 1.0, 0.0));
#endif
}

void GetAmbientSH_half(out half3 AmbientColor)
{
#if defined(SHADERGRAPH_PREVIEW)
    AmbientColor = half3(0.5, 0.55, 0.6);
#else
    AmbientColor = SampleSH(half3(0.0, 1.0, 0.0));
#endif
}

#endif