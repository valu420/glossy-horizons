// Piso de baldosas Frutiger Aero con iluminación real de URP (luces, sombras, reflejos, niebla).
// Las baldosas se ubican según la posición en el mundo (X, Z), así se ven del mismo tamaño
// en cualquier piso sin importar cómo vengan las coordenadas UV del modelo.
Shader "Glossy/BaldosasMundo"
{
    Properties
    {
        _BaseMap ("Baldosas (color)", 2D) = "white" {}
        [Normal] _BumpMap ("Baldosas (relieve)", 2D) = "bump" {}
        _BumpScale ("Fuerza del relieve", Range(0, 2)) = 1
        _Color ("Tinte", Color) = (1,1,1,1)
        _Tamano ("Metros que ocupa la textura (2 x 2 baldosas)", Float) = 2
        _Smoothness ("Brillo del esmalte", Range(0, 1)) = 0.86
        _BrilloJunta ("Brillo de las juntas", Range(0, 1)) = 0.35
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _Color;
                float _Tamano;
                half _Smoothness;
                half _BrilloJunta;
                half _BumpScale;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float  fogFactor  : TEXCOORD2;
            };

            Varyings vert (Attributes v)
            {
                Varyings o;
                VertexPositionInputs pos = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = pos.positionCS;
                o.positionWS = pos.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.fogFactor = ComputeFogFactor(pos.positionCS.z);
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                float2 uv = i.positionWS.xz / max(_Tamano, 0.01);
                half4 color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv) * _Color;
                half3 n = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uv), _BumpScale);

                // Piso: tangente = X del mundo, bitangente = Z del mundo
                // Tangente segura: en las caras de los costados (normal hacia X) la cuenta anterior daba
                // cero, y normalizar cero produce píxeles inválidos (NaN) que el bloom convertía en un brillo gigante.
                float3 normalBase = SafeNormalize(i.normalWS);
                float3 refEje = abs(normalBase.x) < 0.9 ? float3(1, 0, 0) : float3(0, 0, 1);
                float3 t = SafeNormalize(refEje - normalBase * dot(refEje, normalBase));
                float3 b = cross(normalBase, t);
                float3 normalWS = SafeNormalize(n.x * t + n.y * b + n.z * normalBase);

                // las juntas (más oscuras) brillan menos que el esmalte
                half esmalte = saturate((dot(color.rgb, half3(0.33, 0.33, 0.34)) - 0.72) * 6.0);
                half suavidad = lerp(_BrilloJunta, _Smoothness, esmalte);

                InputData d = (InputData)0;
                d.positionWS = i.positionWS;
                d.positionCS = i.positionCS;
                d.normalWS = normalWS;
                d.viewDirectionWS = GetWorldSpaceNormalizeViewDir(i.positionWS);
                d.shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                d.fogCoord = i.fogFactor;
                d.bakedGI = SampleSH(normalWS);                     // luz ambiente del cielo
                d.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                d.shadowMask = half4(1, 1, 1, 1);

                SurfaceData s = (SurfaceData)0;
                s.albedo = color.rgb;
                s.metallic = 0;
                s.specular = 0;
                s.smoothness = suavidad;
                s.normalTS = half3(0, 0, 1);
                s.occlusion = 1;
                s.alpha = 1;

                half4 c = UniversalFragmentPBR(d, s);
                c.rgb = MixFog(c.rgb, i.fogFactor);
                c.rgb = min(max(c.rgb, 0), 8);                       // nunca valores inválidos ni exagerados
                return c;
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/SHADOWCASTER"
        UsePass "Universal Render Pipeline/Lit/DEPTHONLY"
        UsePass "Universal Render Pipeline/Lit/DEPTHNORMALS"
    }
}
