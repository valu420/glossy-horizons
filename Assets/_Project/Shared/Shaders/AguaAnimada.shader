// Agua animada (fuente, piscina): ondas que se mueven sin texturas (suma de olas + ondulación fina),
// reflejo del entorno según el ángulo (Fresnel), destello del sol, color de profundidad y
// anillos que salen del centro del objeto (donde caen los chorros de una fuente).
Shader "Glossy/AguaAnimada"
{
    Properties
    {
        _ColorProfundo ("Color profundo", Color) = (0.05, 0.45, 0.7, 1)
        _ColorClaro ("Color claro", Color) = (0.45, 0.9, 1, 1)
        _Opacidad ("Opacidad", Range(0, 1)) = 0.72
        _Escala ("Tamaño de las olas", Float) = 1.6
        _Velocidad ("Velocidad", Float) = 1
        _Fuerza ("Fuerza de las olas", Range(0, 1)) = 0.35
        _Anillos ("Anillos desde el centro", Range(0, 1)) = 0.6
        _Reflejo ("Reflejo", Range(0, 1.5)) = 0.9
        _Destello ("Destello del sol", Range(0, 4)) = 1.6
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _ColorProfundo, _ColorClaro;
                half _Opacidad, _Fuerza, _Anillos, _Reflejo, _Destello;
                float _Escala, _Velocidad;
            CBUFFER_END
            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct V { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 centroWS : TEXCOORD1; float fog : TEXCOORD2; };
            V vert (A v)
            {
                V o; VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS; o.positionWS = p.positionWS;
                o.centroWS = TransformObjectToWorld(float3(0, 0, 0));
                o.fog = ComputeFogFactor(p.positionCS.z); return o;
            }
            // pendiente de las olas en el punto (derivada de una suma de senos en varias direcciones)
            float2 Olas (float2 p, float t)
            {
                float2 g = 0;
                float2 dirs[4] = { float2(1, 0.3), float2(-0.4, 1), float2(0.7, -0.8), float2(-1, -0.2) };
                float frec[4] = { 1.0, 1.7, 2.9, 4.3 };
                [unroll] for (int k = 0; k < 4; k++)
                {
                    float2 d = normalize(dirs[k]);
                    float f = frec[k] / _Escala;
                    g += d * cos(dot(p, d) * f * 6.2831 + t * (1 + k * 0.6)) * (1.0 / (k + 1));
                }
                return g;
            }
            half4 frag (V i) : SV_Target
            {
                float t = _Time.y * _Velocidad;
                float2 g = Olas(i.positionWS.xz, t) * _Fuerza * 0.35;
                // anillos que se abren desde el centro (donde caen los chorros)
                float2 dc = i.positionWS.xz - i.centroWS.xz;
                float r = length(dc);
                float anillo = cos(r * 9 - t * 6) * exp(-r * 0.35) * _Anillos * 0.25;
                g += (r > 1e-3 ? dc / r : float2(0, 0)) * anillo;
                float3 n = normalize(float3(-g.x, 1, -g.y));
                float3 vista = GetWorldSpaceNormalizeViewDir(i.positionWS);
                half fresnel = pow(1 - saturate(dot(n, vista)), 4) * 0.85 + 0.08;
                float3 rf = reflect(-vista, n);
                half3 entorno = GlossyEnvironmentReflection(rf, i.positionWS, 0.02, 1, GetNormalizedScreenSpaceUV(i.positionCS));
                Light sol = GetMainLight();
                half destello = pow(saturate(dot(rf, sol.direction)), 300) * _Destello;
                half3 agua = lerp(_ColorProfundo.rgb, _ColorClaro.rgb, saturate(0.5 + g.x * 2 + g.y));
                // cáusticas: líneas de luz que bailan en el fondo (agua cristalina)
                float2 q = i.positionWS.xz * 2.2 / _Escala;
                float c1 = sin(q.x * 2.0 + sin(q.y * 1.7 + t) * 1.6 + t);
                float c2 = sin(q.y * 2.3 + sin(q.x * 1.4 - t * 1.1) * 1.6 - t * 0.8);
                half caustica = pow(saturate(1 - abs(c1 + c2) * 0.5), 8) * 0.7;
                agua += caustica;
                half3 col = lerp(agua * (0.6 + 0.4 * saturate(dot(n, sol.direction))), entorno * _Reflejo, fresnel) + sol.color * destello;
                col = MixFog(col, i.fog);
                return half4(min(max(col, 0), 4), saturate(_Opacidad + fresnel * 0.3 + destello));
            }
            ENDHLSL
        }
    }
}
