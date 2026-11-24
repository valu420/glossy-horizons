// Pasto natural para el GrassField del Hub (dibujado con GPU instancing).
//   - degradado por brizna: raíz verde oscuro -> punta verde-amarilla brillante
//   - cada brizna con un tono un poco distinto (según dónde está)
//   - viento: las puntas se mecen en olas que recorren el campo
//   - luz suave envolvente + luz que atraviesa la hoja + brillo satinado
//   - recibe las sombras del sol
Shader "Glossy/PastoNatural"
{
    Properties
    {
        _BaseMap ("Brizna (transparencia)", 2D) = "white" {}
        _Cutoff ("Recorte", Range(0.05, 0.95)) = 0.4
        _ColorRaiz ("Color de la raíz", Color) = (0.2, 0.42, 0.05, 1)
        _ColorPunta ("Color de la punta", Color) = (0.72, 0.95, 0.2, 1)
        _Variacion ("Variación de tono", Range(0, 1)) = 0.35
        _Alto ("Alto de la malla (lo pone la herramienta)", Float) = 0.4
        _Viento ("Fuerza del viento", Range(0, 0.6)) = 0.18
        _VelocidadViento ("Velocidad del viento", Range(0, 4)) = 1.4
        _Translucidez ("Luz a través de la hoja", Range(0, 2)) = 0.8
        _Brillo ("Brillo satinado", Range(0, 1)) = 0.3
    }

    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" "RenderPipeline"="UniversalPipeline" }
        Cull Off

        Pass
        {
            Name "Pasto"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half _Cutoff;
                half4 _ColorRaiz, _ColorPunta;
                half _Variacion;
                float _Alto;
                half _Viento, _VelocidadViento, _Translucidez, _Brillo;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float alto : TEXCOORD3;            // 0 raíz, 1 punta
                float tono : TEXCOORD4;            // variación por brizna
                float fogFactor : TEXCOORD5;
            };

            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }

            Varyings vert (Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                float h = saturate(v.positionOS.y / max(_Alto, 0.01));
                float3 origen = TransformObjectToWorld(float3(0, 0, 0));
                float3 posWS = TransformObjectToWorld(v.positionOS.xyz);
                // viento: ola que recorre el campo + ráfaga + temblor fino; solo se mueve la parte alta
                float t = _Time.y * _VelocidadViento;
                float ola = sin(t + origen.x * 0.18 + origen.z * 0.12) * 0.6 + sin(t * 0.37 + origen.z * 0.05) * 0.4;
                float temblor = sin(t * 4.3 + Hash(origen.xz) * 6.28) * 0.15;
                float empuje = (ola + temblor) * _Viento * h * h;
                posWS += float3(empuje, -abs(empuje) * 0.25, empuje * 0.55);
                o.positionWS = posWS;
                o.positionCS = TransformWorldToHClip(posWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.alto = h;
                o.tono = Hash(origen.xz + 3.7);
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag (Varyings i, bool frente : SV_IsFrontFace) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
                clip(tex.a - _Cutoff);

                half3 punta = lerp(_ColorPunta.rgb, _ColorPunta.rgb * half3(0.75, 1.0, 0.7), i.tono * _Variacion * 2);
                half3 albedo = lerp(_ColorRaiz.rgb, punta, smoothstep(0.0, 1.0, i.alto));
                albedo *= 0.85 + 0.3 * dot(tex.rgb, half3(0.33, 0.33, 0.34)) * (1 - _Variacion * 0.5);

                // el campo se ilumina como un manto (normal casi hacia arriba): briznas claras de ambos lados
                float3 n = SafeNormalize(lerp(SafeNormalize(i.normalWS) * (frente ? 1 : -1), float3(0, 1, 0), 0.85));
                float3 vista = GetWorldSpaceNormalizeViewDir(i.positionWS);
                Light sol = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half sombra = sol.shadowAttenuation;

                half difusa = saturate((dot(n, sol.direction) + 0.5) / 1.5);
                half atraviesa = pow(saturate(dot(vista, -sol.direction)), 3) * _Translucidez * i.alto;
                half3 h = SafeNormalize(sol.direction + vista);
                half brillo = pow(saturate(dot(n, h)), 30) * _Brillo * i.alto;
                half oclusion = lerp(0.68, 1.0, i.alto);                    // la raíz queda en sombra (pasto tupido)

                half3 col = albedo * sol.color * difusa * sombra * oclusion
                          + albedo * half3(1.0, 1.1, 0.6) * sol.color * atraviesa * (0.5 + 0.5 * sombra)
                          + sol.color * brillo * sombra
                          + albedo * SampleSH(n) * oclusion;
                col = MixFog(col, i.fogFactor);
                return half4(min(max(col, 0), 4), 1);
            }
            ENDHLSL
        }
    }
}
