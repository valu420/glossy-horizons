// Aurora Frutiger: cinta de luz en arco (como un arcoíris azul) que pasa por encima de la ciudad.
//   - Suma luz (aditivo): brilla, se mezcla con el cielo y el bloom la hace resplandecer.
//   - Viva: pulsos de luz que viajan a lo largo del arco, hebras finas que se mueven y una ondulación
//     suave de la cinta (en el vértice), todo con el tiempo, sin scripts.
//   - Se desvanece en las puntas, en los bordes y con la niebla de la distancia.
// Malla (la arma LookFrutiger.Auroras): uv.x = a lo largo del arco (0..1), uv.y = a lo ancho (0..1),
// normal = dirección "a lo ancho" (para ondular), uv2.x = fase propia de cada hebra.
Shader "Glossy/AuroraArco"
{
    Properties
    {
        _ColorA ("Cian (centro)", Color) = (0.40, 0.95, 1.0, 1)
        _ColorB ("Azul (bordes)", Color) = (0.12, 0.42, 1.0, 1)
        _Intensidad ("Intensidad", Range(0, 4)) = 1.1
        _Velocidad ("Velocidad de los pulsos", Range(0, 2)) = 0.35
        _Hebras ("Hebras finas", Range(1, 8)) = 3
        _Ondulacion ("Ondulación (m)", Range(0, 20)) = 4
    }
    SubShader
    {
        Tags { "Queue"="Transparent+20" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Blend One One
        ZWrite Off
        Cull Off
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ColorA, _ColorB;
                half _Intensidad, _Velocidad, _Hebras, _Ondulacion;
            CBUFFER_END

            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; float2 uv2 : TEXCOORD1; };
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float fase : TEXCOORD1; float niebla : TEXCOORD2; };

            // "ruido" suave hecho con senos (barato y continuo)
            float Ruido(float x)
            {
                return sin(x) * 0.5 + sin(x * 2.13 + 1.7) * 0.3 + sin(x * 4.71 + 0.3) * 0.2;
            }

            V vert (A v)
            {
                V o;
                float t = _Time.y;
                float fase = v.uv2.x;
                float3 p = v.positionOS.xyz;
                // la cinta ondula a lo ancho, más en el centro del arco que en las puntas
                float onda = Ruido(v.uv.x * 9.0 + t * 0.5 + fase) * _Ondulacion * sin(v.uv.x * PI);
                p += normalize(v.normalOS) * onda;
                o.positionCS = TransformObjectToHClip(p);
                o.uv = v.uv;
                o.fase = fase;
                o.niebla = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag (V i) : SV_Target
            {
                float t = _Time.y;
                float u = i.uv.x, w = i.uv.y;
                float bordes = smoothstep(0.0, 0.38, w) * smoothstep(1.0, 0.62, w);
                float puntas = smoothstep(0.0, 0.14, u) * smoothstep(1.0, 0.86, u);
                // hebras finas que se mueven dentro de la cinta
                float hebras = pow(abs(sin((w * _Hebras + Ruido(u * 6.0 + t * 0.3 + i.fase) * 0.45) * PI)), 6.0);
                // pulsos de luz que viajan a lo largo del arco
                float flujo = 0.5 + 0.5 * Ruido(u * 14.0 - t * _Velocidad * 6.0 + i.fase);
                float latido = 0.85 + 0.15 * sin(t * 0.7 + i.fase * 3.0);
                float brillo = bordes * puntas * (0.3 + hebras * 0.9) * (0.45 + 0.75 * flujo) * latido;
                float mezcla = saturate(hebras * 0.7 + 0.25 + 0.2 * sin(u * 10.0 + t * 0.4 + i.fase));
                float3 col = lerp(_ColorB.rgb, _ColorA.rgb, mezcla) * brillo * _Intensidad;
                col = MixFogColor(col, half3(0, 0, 0), i.niebla);   // aditivo: en la niebla se apaga
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
