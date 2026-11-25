// Follaje natural para arbustos hechos con tarjetas de hojas (técnica usada en los juegos):
//   - recorte por transparencia (cada hoja se ve suelta) y visible por los dos lados
//   - normales "esféricas" (vienen en la malla) -> el arbusto se ilumina como un volumen suave
//   - luz que atraviesa las hojas cuando el sol está detrás (translucidez)
//   - leve movimiento con el viento y variación de color por hoja (color de vértice)
//   - recibe y proyecta sombras
Shader "Glossy/FollajeNatural"
{
    Properties
    {
        _BaseMap ("Hojas (con transparencia)", 2D) = "white" {}
        _Color ("Tinte", Color) = (1,1,1,1)
        _Cutoff ("Recorte", Range(0.05, 0.95)) = 0.45
        _Translucidez ("Luz a través de las hojas", Range(0, 2)) = 0.7
        _ColorTranslucido ("Color de la luz que atraviesa", Color) = (0.75, 1, 0.35, 1)
        _Envoltura ("Suavidad de la luz", Range(0, 1)) = 0.45
        _Brillo ("Brillo de las hojas", Range(0, 1)) = 0.25
        _Viento ("Viento", Range(0, 0.2)) = 0.04
        _VelocidadViento ("Velocidad del viento", Range(0, 4)) = 1.3
    }

    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" "RenderPipeline"="UniversalPipeline" }
        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _Color;
            half _Cutoff;
            half _Translucidez;
            half4 _ColorTranslucido;
            half _Envoltura;
            half _Brillo;
            half _Viento;
            half _VelocidadViento;
        CBUFFER_END

        // Viento: las hojas de arriba (alfa del color de vértice) se mecen más
        float3 Viento(float3 posWS, float peso)
        {
            float t = _Time.y * _VelocidadViento;
            float onda = sin(t + posWS.x * 0.7 + posWS.z * 0.5) * 0.6 + sin(t * 2.3 + posWS.x * 1.9) * 0.4;
            return float3(onda, onda * 0.2, onda * 0.6) * _Viento * peso;
        }
        ENDHLSL

        Pass
        {
            Name "Follaje"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                half4 color : COLOR;
                float fogFactor : TEXCOORD3;
            };

            Varyings vert (Attributes v)
            {
                Varyings o;
                float3 posWS = TransformObjectToWorld(v.positionOS.xyz);
                posWS += Viento(posWS, v.color.a);
                o.positionWS = posWS;
                o.positionCS = TransformWorldToHClip(posWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.color = v.color;
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
                clip(tex.a - _Cutoff);
                half3 albedo = tex.rgb * _Color.rgb * i.color.rgb;

                float3 n = SafeNormalize(i.normalWS);
                float3 v = GetWorldSpaceNormalizeViewDir(i.positionWS);
                Light sol = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half sombra = sol.shadowAttenuation * sol.distanceAttenuation;

                // luz envolvente (suave, sin cortes duros) + translucidez cuando el sol está detrás
                half ndl = dot(n, sol.direction);
                half difusa = saturate((ndl + _Envoltura) / (1 + _Envoltura));
                half atraviesa = pow(saturate(dot(v, -sol.direction)), 4) * _Translucidez;
                half3 col = albedo * sol.color * (difusa * sombra)
                          + _ColorTranslucido.rgb * albedo * sol.color * atraviesa * (0.4 + 0.6 * sombra);
                // brillo suave de las hojas
                half3 h = SafeNormalize(sol.direction + v);
                col += sol.color * pow(saturate(dot(n, h)), 24) * _Brillo * sombra;
                // luz ambiente del cielo
                col += albedo * SampleSH(n);

                #if defined(_ADDITIONAL_LIGHTS)
                uint cuantas = GetAdditionalLightsCount();
                InputData inputData = (InputData)0;                 // LIGHT_LOOP_BEGIN usa este nombre (Forward+)
                inputData.positionWS = i.positionWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                LIGHT_LOOP_BEGIN(cuantas)
                    Light l = GetAdditionalLight(lightIndex, i.positionWS);
                    col += albedo * l.color * l.distanceAttenuation * saturate((dot(n, l.direction) + _Envoltura) / (1 + _Envoltura));
                LIGHT_LOOP_END
                #endif

                col = MixFog(col, i.fogFactor);
                return half4(min(max(col, 0), 4), 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex vertSombra
            #pragma fragment fragSombra
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            float3 _LightDirection;

            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            V vertSombra (A v)
            {
                V o;
                float3 posWS = TransformObjectToWorld(v.positionOS.xyz);
                posWS += Viento(posWS, v.color.a);
                float3 nWS = TransformObjectToWorldNormal(v.normalOS);
                float4 pos = TransformWorldToHClip(ApplyShadowBias(posWS, nWS, _LightDirection));
                #if UNITY_REVERSED_Z
                    pos.z = min(pos.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    pos.z = max(pos.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                o.positionCS = pos;
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                return o;
            }

            half4 fragSombra (V i) : SV_Target
            {
                clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).a - _Cutoff);
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex vertProf
            #pragma fragment fragProf
            struct A { float4 positionOS : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            V vertProf (A v)
            {
                V o;
                float3 posWS = TransformObjectToWorld(v.positionOS.xyz);
                posWS += Viento(posWS, v.color.a);
                o.positionCS = TransformWorldToHClip(posWS);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                return o;
            }
            half4 fragProf (V i) : SV_Target
            {
                clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).a - _Cutoff);
                return 0;
            }
            ENDHLSL
        }
    }
}
