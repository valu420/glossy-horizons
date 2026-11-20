// Glossy Horizons - material de gel estilo Frutiger (HU-01, HU-02)
// La iluminación viene de una textura "matcap", por eso el personaje
// se ve igual en cualquier escena, sin depender del fondo ni de las luces.
//   Matcap R = cuerpo (0 = borde -> Color Base, 1 = centro -> Color Claro)
//   Matcap G = reflejos blancos
//   Matcap B = brillo del borde inferior (luz que atraviesa el gel)
// Para cambiar el color desde un script (HU-02):
//   material.SetColor("_ColorBase", ...);  material.SetColor("_ColorClaro", ...);
// Enojo (HU-06, emote Enojado): degradado rojo desde la mitad hasta la parte de arriba de la pieza.
//   Se activa por pieza con un MaterialPropertyBlock: _Enojo (0..1) y la altura en el mundo
//   donde empieza (_EnojoDesde) y donde es más fuerte (_EnojoHasta).
Shader "Glossy/GelMatcap"
{
    Properties
    {
        _ColorBase ("Color Base (bordes)", Color) = (0.098, 0.373, 0.843, 1)
        _ColorClaro ("Color Claro (centro)", Color) = (0.47, 0.88, 1, 1)
        [NoScaleOffset] _Matcap ("Matcap", 2D) = "black" {}
        _Brillo ("Intensidad de reflejos", Range(0, 1.5)) = 1
        _Borde ("Brillo del borde inferior", Range(0, 1)) = 0.6
        _ColorContorno ("Color del contorno", Color) = (0.04, 0.1, 0.25, 1)
        _GrosorContorno ("Grosor del contorno (0 = sin contorno)", Range(0, 0.03)) = 0.008
        [Header(Enojo controlado por AnimacionGel)]
        _Enojo ("Enojo", Range(0, 1)) = 0
        _ColorEnojo ("Color del enojo", Color) = (1, 0.12, 0.15, 1)
        _EnojoDesde ("Enojo: altura donde empieza", Float) = 0
        _EnojoHasta ("Enojo: altura donde es mas fuerte", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "GelMatcap"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_Matcap);
            SAMPLER(sampler_Matcap);

            CBUFFER_START(UnityPerMaterial)
                half4 _ColorBase;
                half4 _ColorClaro;
                half _Brillo;
                half _Borde;
                half4 _ColorContorno;
                half _GrosorContorno;
                half _Enojo;
                half4 _ColorEnojo;
                float _EnojoDesde;
                float _EnojoHasta;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalVS   : TEXCOORD0;
                float  fogFactor  : TEXCOORD1;
                float  alturaWS   : TEXCOORD2;
            };

            Varyings vert (Attributes v)
            {
                Varyings o;
                VertexPositionInputs pos = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = pos.positionCS;
                float3 normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.normalVS = TransformWorldToViewDir(normalWS);
                o.fogFactor = ComputeFogFactor(pos.positionCS.z);
                o.alturaWS = pos.positionWS.y;
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalVS);
                float2 uv = n.xy * 0.5 + 0.5;
                half3 m = SAMPLE_TEXTURE2D(_Matcap, sampler_Matcap, uv).rgb;

                half3 col = lerp(_ColorBase.rgb, _ColorClaro.rgb, m.r);   // borde azul -> centro claro
                col += _ColorClaro.rgb * m.b * _Borde;                    // luz que atraviesa abajo
                // Enojo: el gel se pone rojo de la mitad hacia arriba (conserva su volumen y sus reflejos)
                half enojo = _Enojo * smoothstep(_EnojoDesde, _EnojoHasta, i.alturaWS);
                half3 rojo = _ColorEnojo.rgb * lerp(0.55, 1.15, m.r);
                col = lerp(col, rojo, saturate(enojo));
                col = lerp(col, half3(1, 1, 1), saturate(m.g * _Brillo)); // reflejos blancos
                col = MixFog(col, i.fogFactor);
                return half4(col, 1);
            }
            ENDHLSL
        }

        // Contorno oscuro (técnica "inverted hull"): se dibuja la pieza un poco
        // inflada y solo por dentro (Cull Front), así asoma como una silueta.
        Pass
        {
            Name "Contorno"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vertContorno
            #pragma fragment fragContorno
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ColorBase;
                half4 _ColorClaro;
                half _Brillo;
                half _Borde;
                half4 _ColorContorno;
                half _GrosorContorno;
                half _Enojo;
                half4 _ColorEnojo;
                float _EnojoDesde;
                float _EnojoHasta;
            CBUFFER_END

            struct AttributesContorno
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            float4 vertContorno (AttributesContorno v) : SV_POSITION
            {
                float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(v.normalOS);
                positionWS += normalWS * _GrosorContorno;
                return TransformWorldToHClip(positionWS);
            }

            half4 fragContorno () : SV_Target
            {
                return half4(_ColorContorno.rgb, 1);
            }
            ENDHLSL
        }

        // Sombras y profundidad: se reutilizan las del material Lit de URP
        UsePass "Universal Render Pipeline/Lit/SHADOWCASTER"
        UsePass "Universal Render Pipeline/Lit/DEPTHONLY"
    }
}
