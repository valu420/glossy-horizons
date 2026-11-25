// Cielo Frutiger Aero: degradado azul profundo arriba -> celeste claro en el horizonte,
// sol con halo y rayos SIEMPRE donde apunta la Directional Light, y una capa de nubes
// fotográficas reales (T_NubesFrutiger, RGBA) que gira despacio: nubes en movimiento.
// La dirección del sol la publica el componente CieloFrutiger (_CieloDirSol);
// si no está, usa la luz principal de URP.
Shader "Glossy/CieloFrutiger"
{
    Properties
    {
        [Header(Degradado)]
        _ColorCenit ("Azul arriba (cénit)", Color) = (0.0, 0.27, 0.80, 1)
        _ColorMedio ("Azul medio", Color) = (0.10, 0.50, 0.95, 1)
        _ColorHorizonte ("Celeste del horizonte", Color) = (0.66, 0.85, 0.98, 1)
        _ColorSuelo ("Bajo el horizonte", Color) = (0.55, 0.75, 0.93, 1)
        _CurvaCenit ("Curva del azul (más alto = más azul oscuro)", Range(0.5, 4)) = 1.8
        _CurvaHorizonte ("Franja clara del horizonte (más alto = más fina)", Range(2, 20)) = 7

        [Header(Nubes)]
        [NoScaleOffset] _Nubes ("Nubes (RGBA)", 2D) = "black" {}
        _BrilloNubes ("Brillo de las nubes", Range(0.5, 1.5)) = 1.05
        _OpacidadNubes ("Opacidad de las nubes", Range(0, 1)) = 1
        _Velocidad ("Velocidad (grados por segundo)", Range(0, 3)) = 0.35
        _Giro ("Giro inicial", Range(0, 360)) = 0

        [Header(Sol)]
        _SolColor ("Color del sol", Color) = (1, 0.97, 0.9, 1)
        _SolTamano ("Tamaño del disco", Range(0.005, 0.1)) = 0.035
        _SolBrillo ("Brillo del disco", Range(0, 20)) = 6
        _SolHalo ("Halo", Range(0, 3)) = 1
        _SolRayos ("Rayos", Range(0, 1)) = 0.35

        _Exposicion ("Exposición", Range(0.2, 3)) = 1
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_Nubes); SAMPLER(sampler_Nubes);
            float4 _CieloDirSol; // xyz = hacia el sol, w = 1 si es válido (lo pone CieloFrutiger)

            CBUFFER_START(UnityPerMaterial)
                half4 _ColorCenit, _ColorMedio, _ColorHorizonte, _ColorSuelo, _SolColor;
                half _CurvaCenit, _CurvaHorizonte, _BrilloNubes, _OpacidadNubes, _Velocidad, _Giro;
                half _SolTamano, _SolBrillo, _SolHalo, _SolRayos, _Exposicion;
            CBUFFER_END

            struct A { float4 positionOS : POSITION; };
            struct V { float4 positionCS : SV_POSITION; float3 dir : TEXCOORD0; };

            V vert (A v)
            {
                V o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.dir = v.positionOS.xyz;
                return o;
            }

            half4 frag (V i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float3 sol = _CieloDirSol.w > 0.5 ? normalize(_CieloDirSol.xyz) : normalize(_MainLightPosition.xyz);

                // --- degradado del cielo
                float yy = saturate(d.y);
                float3 col = lerp(_ColorCenit.rgb, _ColorMedio.rgb, pow(1 - yy, _CurvaCenit));
                col = lerp(col, _ColorHorizonte.rgb, pow(1 - yy, _CurvaHorizonte));
                col = lerp(col, _ColorSuelo.rgb, smoothstep(0, 0.25, -d.y));

                // --- halo del sol
                float ang = acos(clamp(dot(d, sol), -1, 1));
                col += _SolColor.rgb * _SolHalo * (0.9 * exp(-ang * 14) + 0.25 * exp(-ang * 3.5));

                // --- nubes (giran alrededor del eje vertical)
                float giro = radians(_Giro + _Time.y * _Velocidad);
                float s, c; sincos(giro, s, c);
                float3 r = float3(d.x * c - d.z * s, d.y, d.x * s + d.z * c);
                float2 uv = float2(0.5 - atan2(r.z, r.x) / (2 * PI), 1 - acos(clamp(r.y, -1, 1)) / PI);
                half4 n = SAMPLE_TEXTURE2D_LOD(_Nubes, sampler_Nubes, uv, 0); // LOD 0: sin costura
                float a = n.a * _OpacidadNubes;
                float3 colNube = n.rgb * _BrilloNubes + _SolColor.rgb * 0.5 * exp(-ang * 5) * (1 - a); // borde plateado
                col = lerp(col, colNube, a);

                // --- disco y rayos del sol (las nubes lo tapan casi del todo)
                float disco = saturate((_SolTamano - ang) / (_SolTamano * 0.3));
                float3 t = normalize(cross(sol, float3(0.001, 1, 0)));
                float3 b = cross(sol, t);
                float phi = atan2(dot(d, b), dot(d, t));
                float rayos = pow(abs(cos(phi * 6)), 20) + 0.6 * pow(abs(cos(phi * 9 + 1)), 30);
                rayos *= exp(-ang * 8) * _SolRayos;
                col += _SolColor.rgb * (disco * _SolBrillo + rayos * 2) * (1 - a * 0.85);

                return half4(col * _Exposicion, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
