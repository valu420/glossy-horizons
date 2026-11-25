// Gotas y chorros de agua (partículas): gota redonda suave con brillo, sin textura.
// Se dibuja con el color de cada partícula (vertex color), transparente y un poco luminosa.
Shader "Glossy/ParticulaAgua"
{
    Properties
    {
        _Brillo ("Brillo", Range(0, 3)) = 1.3
        _Suavidad ("Borde suave", Range(0.05, 1)) = 0.55
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half _Brillo, _Suavidad;
            CBUFFER_END
            struct A { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; float fog : TEXCOORD1; };
            V vert (A v)
            {
                V o; o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv; o.color = v.color; o.fog = ComputeFogFactor(o.positionCS.z); return o;
            }
            half4 frag (V i) : SV_Target
            {
                float2 d = i.uv * 2 - 1;
                float r = length(d);
                half alfa = saturate((1 - r) / _Suavidad);
                half brillo = exp(-dot(d - float2(-0.3, 0.3), d - float2(-0.3, 0.3)) * 10) * 0.6;   // reflejo de la gota
                half3 col = i.color.rgb * _Brillo + brillo;
                col = MixFog(col, i.fog);
                return half4(min(col, 3), alfa * i.color.a);
            }
            ENDHLSL
        }
    }
}
