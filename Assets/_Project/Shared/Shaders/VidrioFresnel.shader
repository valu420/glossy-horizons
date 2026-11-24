// Vidrio arquitectónico Frutiger Aero: casi invisible de frente y brillante en los bordes (efecto Fresnel),
// con un toque cian, el reflejo del entorno SOLO en ángulos rasantes y el destello del sol.
// Así una cúpula o una ventana se lee como vidrio limpio, no como una burbuja que refleja todo.
Shader "Glossy/VidrioFresnel"
{
    Properties
    {
        _Tinte ("Tinte del vidrio", Color) = (0.75, 0.95, 1, 1)
        _Opacidad ("Opacidad de frente", Range(0, 0.5)) = 0.06
        _ColorBorde ("Color del borde", Color) = (0.85, 1, 1, 1)
        _FuerzaBorde ("Brillo del borde", Range(0, 2)) = 0.7
        _PotenciaBorde ("Qué tan fino es el borde", Range(1, 8)) = 3.5
        _Reflejo ("Reflejo del entorno (en los bordes)", Range(0, 1)) = 0.35
        _Destello ("Destello del sol", Range(0, 4)) = 1.5
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }

        Pass
        {
            Name "Vidrio"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

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
                half4 _Tinte;
                half _Opacidad;
                half4 _ColorBorde;
                half _FuerzaBorde;
                half _PotenciaBorde;
                half _Reflejo;
                half _Destello;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
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
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.fogFactor = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half4 frag (Varyings i, bool frente : SV_IsFrontFace) : SV_Target
            {
                float3 n = SafeNormalize(i.normalWS);
                float3 v = GetWorldSpaceNormalizeViewDir(i.positionWS);
                half fresnel = pow(1.0 - saturate(abs(dot(n, v))), _PotenciaBorde);

                // Reflejo del entorno (sondas) solo donde el vidrio se ve de lado
                float3 r = reflect(-v, n);
                half3 entorno = GlossyEnvironmentReflection(r, i.positionWS, 0.05, 1.0,
                                                            GetNormalizedScreenSpaceUV(i.positionCS));

                // Destello del sol (luz principal)
                Light sol = GetMainLight();
                half brillo = pow(saturate(dot(r, sol.direction)), 220.0) * _Destello;

                half3 col = _Tinte.rgb * _Opacidad
                          + _ColorBorde.rgb * fresnel * _FuerzaBorde
                          + entorno * fresnel * _Reflejo
                          + sol.color * brillo;
                half alfa = saturate(_Opacidad + fresnel * (_FuerzaBorde * 0.5 + _Reflejo) + brillo);
                col = MixFog(col, i.fogFactor);
                col = min(max(col, 0), 3);                           // el destello no puede encandilar
                return half4(col, alfa);
            }
            ENDHLSL
        }
    }
}
