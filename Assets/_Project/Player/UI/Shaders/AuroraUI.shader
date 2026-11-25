// HU-02: fondo animado de las interfaces (identidad Frutiger Aqua de Glossy Horizons).
// Aurora verde azulada con cintas de luz que ondulan y burbujas que suben.
// Es un shader de uGUI: se pone en el material de un Image. La forma la da el sprite
// (su transparencia) y el color lo calcula el shader, sobre el rectángulo de la imagen
// (con el componente FondoVivo) o sobre toda la pantalla (como un escritorio de Vista).
Shader "Glossy/UI/Aurora"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite (da la forma)", 2D) = "white" {}
        _Color ("Tinte", Color) = (1,1,1,1)
        _ColorFondo ("Fondo", Color) = (0.02, 0.32, 0.40, 1)
        _ColorHorizonte ("Luz del horizonte", Color) = (0.10, 0.30, 0.28, 1)
        _ColorLima ("Cintas: verde lima", Color) = (0.70, 1.00, 0.35, 1)
        _ColorCian ("Cintas: cian", Color) = (0.40, 0.95, 1.00, 1)
        _Velocidad ("Velocidad", Range(0, 3)) = 1
        _Burbujas ("Cantidad de burbujas", Range(0, 1)) = 1
        [Toggle] _Local ("Usar el rectángulo de la imagen (si no, la pantalla)", Float) = 1
        _Aspecto ("Ancho / alto del rectángulo (lo pone FondoVivo)", Float) = 2

        // --- lo que necesita uGUI (máscaras y recortes) ---
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 mundo : TEXCOORD1;
                float4 pantalla : TEXCOORD2;
            };

            sampler2D _MainTex;
            fixed4 _Color, _ColorFondo, _ColorHorizonte, _ColorLima, _ColorCian;
            float _Velocidad, _Burbujas, _Local, _Aspecto;
            float4 _ClipRect;

            v2f vert (appdata v)
            {
                v2f o;
                o.mundo = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.pantalla = ComputeScreenPos(o.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                return o;
            }

            // pow() con base negativa da NaN en los shaders: para elevar al cuadrado se usa x*x
            float Cuad (float x) { return x * x; }

            float hash (float n) { return frac(sin(n * 127.1) * 43758.5453); }

            float3 Aurora (float2 p, float t)
            {
                float u = p.x, v = p.y;
                // fondo verde azulado con un horizonte de luz
                float3 col = _ColorFondo.rgb + float3(0.03, 0.18, 0.16) * v;
                col += _ColorHorizonte.rgb * exp(-Cuad((v - 0.42) / 0.10));

                // cintas de luz que suben hacia la derecha y ondulan
                [unroll] for (int i = 0; i < 7; i++)
                {
                    float k = i / 6.0;
                    float baseY = 0.18 + 0.10 * k + 0.55 * pow(max(u, 0.0001), 1.6 + 0.3 * k);
                    float onda = 0.025 * sin(u * 4.0 + t * 0.5 + i * 1.7) + 0.012 * sin(u * 9.0 - t * 0.8 + i);
                    float d = v - (baseY + onda);
                    float ancho = 0.0035 + 0.0025 * k;
                    float linea = exp(-Cuad(d / ancho));
                    float halo = exp(-abs(d) / (0.04 + 0.02 * k)) * 0.22;
                    float destello = 0.55 + 0.45 * sin(u * 5.0 - t * 1.1 + i * 2.3);
                    float tono = saturate(u * 1.2 - 0.1 + 0.15 * sin(t * 0.3 + i));
                    col += lerp(_ColorLima.rgb, _ColorCian.rgb, tono) * (linea * 0.9 + halo) * destello;
                }
                // haz de hilos finos
                float hilos = 0;
                [unroll] for (int j = 0; j < 10; j++)
                {
                    float b2 = 0.40 + 0.012 * j + 0.30 * Cuad(u - 0.2);
                    float d2 = v - b2 - 0.02 * sin(u * 3.0 + t * 0.4);
                    hilos += exp(-Cuad(d2 / 0.0018));
                }
                col += float3(0.6, 1.0, 0.95) * hilos * 0.25 * smoothstep(0.0, 0.5, u) * smoothstep(1.0, 0.6, u);
                return col;
            }

            float3 Burbujas (float2 p, float aspecto, float t)
            {
                float3 col = 0;
                [unroll] for (int i = 0; i < 9; i++)
                {
                    float r = 0.012 + 0.03 * hash(i + 3.1);                       // tamaño
                    float vel = 0.025 + 0.04 * hash(i + 7.7);                     // suben despacio
                    float x = hash(i + 1.3) + 0.03 * sin(t * 0.7 + i * 2.1);      // se mecen
                    float y = frac(hash(i + 5.9) + t * vel) * 1.3 - 0.15;
                    float2 d = float2((p.x - x) * aspecto, p.y - y);
                    float dist = length(d) / r;
                    float aro = smoothstep(0.80, 0.97, dist) * (1 - smoothstep(0.97, 1.03, dist));
                    float cuerpo = (1 - smoothstep(0.0, 1.0, dist)) * 0.08;
                    float brillo = exp(-length(d / r - float2(-0.35, 0.35)) * 9.0) * 0.9;
                    col += (aro * 0.55 + cuerpo + brillo) * step(dist, 1.1);
                }
                return col * float3(0.85, 1.0, 1.0);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 forma = tex2D(_MainTex, i.uv);
                // Local: la aurora completa ocupa el rectángulo de la imagen (Image sin sprite: uv 0..1)
                // Pantalla: todas las ventanas comparten el mismo fondo, como un escritorio
                bool local = _Local > 0.5;
                float2 p = local ? i.uv : i.pantalla.xy / max(i.pantalla.w, 1e-5);
                float aspecto = local ? max(_Aspecto, 0.01) : _ScreenParams.x / _ScreenParams.y;
                float t = _Time.y * _Velocidad;

                float3 col = Aurora(p, t) + Burbujas(p, aspecto, t) * _Burbujas;
                fixed4 c = fixed4(saturate(col), forma.a * i.color.a);
                c.rgb *= i.color.rgb;

                #ifdef UNITY_UI_CLIP_RECT
                c.a *= UnityGet2DClipping(i.mundo.xy, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(c.a - 0.001);
                #endif
                return c;
            }
            ENDCG
        }
    }
}
