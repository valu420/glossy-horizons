// Nubes esponjosas Frutiger Aero: blancas y luminosas arriba, celeste suave abajo,
// borde un poco translúcido (se funden con el cielo) y brillo donde les da el sol.
Shader "Glossy/NubeFrutiger"
{
    Properties
    {
        _ColorArriba ("Color arriba", Color) = (1, 1, 1, 1)
        _ColorAbajo ("Color abajo", Color) = (0.68, 0.82, 0.97, 1)
        _Brillo ("Brillo del sol", Range(0, 2)) = 0.6
        _Borde ("Borde translúcido", Range(0, 1)) = 0.55
    }
    SubShader
    {
        Tags { "Queue"="Transparent-50" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite On
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _ColorArriba, _ColorAbajo; half _Brillo, _Borde;
            CBUFFER_END
            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct V { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; };
            V vert (A v)
            {
                V o; VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS; o.positionWS = p.positionWS; o.normalWS = TransformObjectToWorldNormal(v.normalOS); return o;
            }
            half4 frag (V i) : SV_Target
            {
                float3 n = SafeNormalize(i.normalWS);
                float3 vista = GetWorldSpaceNormalizeViewDir(i.positionWS);
                Light sol = GetMainLight();
                half arriba = saturate(n.y * 0.5 + 0.5);
                half luz = saturate(dot(n, sol.direction) * 0.5 + 0.5);
                half3 col = lerp(_ColorAbajo.rgb, _ColorArriba.rgb, saturate(arriba * 0.7 + luz * 0.5));
                col += sol.color * pow(luz, 6) * _Brillo * 0.4;
                half borde = pow(1 - saturate(abs(dot(n, vista))), 2);
                half alfa = saturate(1 - borde * _Borde);
                return half4(col, alfa);
            }
            ENDHLSL
        }
    }
}
