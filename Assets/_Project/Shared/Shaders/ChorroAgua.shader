// Chorros de una fuente: el agua "corre" por la forma del modelo (arcos) con franjas que se
// desplazan desde el centro hacia afuera y hacia abajo, borde brillante (Fresnel) y destellos.
// No necesita UVs: usa la distancia al centro de la fuente y la altura.
Shader "Glossy/ChorroAgua"
{
    Properties
    {
        _Color ("Color del agua", Color) = (0.7, 0.92, 1, 1)
        _Opacidad ("Opacidad", Range(0, 1)) = 0.55
        _Velocidad ("Velocidad del agua", Float) = 2.2
        _Franjas ("Cantidad de franjas", Float) = 7
        _Borde ("Brillo del borde", Range(0, 2)) = 0.9
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Color; half _Opacidad, _Borde; float _Velocidad, _Franjas;
            CBUFFER_END
            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct V { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; float3 centroWS : TEXCOORD2; float fog : TEXCOORD3; };
            V vert (A v)
            {
                V o; VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS; o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.centroWS = TransformObjectToWorld(float3(0, 0, 0));
                o.fog = ComputeFogFactor(p.positionCS.z); return o;
            }
            half4 frag (V i, bool frente : SV_IsFrontFace) : SV_Target
            {
                float3 n = SafeNormalize(i.normalWS) * (frente ? 1 : -1);
                float3 vista = GetWorldSpaceNormalizeViewDir(i.positionWS);
                // recorrido del agua: hacia afuera y hacia abajo
                float recorrido = length(i.positionWS.xz - i.centroWS.xz) - i.positionWS.y * 0.6;
                float t = _Time.y * _Velocidad;
                float f1 = frac(recorrido * _Franjas * 0.5 - t);
                float f2 = frac(recorrido * _Franjas * 1.3 - t * 1.7 + 0.37);
                half franjas = smoothstep(0.0, 0.15, f1) * (1 - smoothstep(0.55, 0.75, f1)) * 0.6
                             + smoothstep(0.0, 0.1, f2) * (1 - smoothstep(0.3, 0.45, f2)) * 0.4;
                half fresnel = pow(1 - saturate(abs(dot(n, vista))), 2.5);
                Light sol = GetMainLight();
                half destello = pow(saturate(dot(reflect(-vista, n), sol.direction)), 60) * 1.5;
                half3 col = _Color.rgb * (0.75 + 0.5 * franjas) + fresnel * _Borde + sol.color * destello;
                half alfa = saturate(_Opacidad * (0.55 + 0.6 * franjas) + fresnel * 0.5 + destello);
                col = MixFog(col, i.fog);
                return half4(min(col, 4), alfa);
            }
            ENDHLSL
        }
    }
}
