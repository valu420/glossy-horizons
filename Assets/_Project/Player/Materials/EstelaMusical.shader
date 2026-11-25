// HU-06: cinta de la estela musical (emote Bailar).
// Muestra la tira de pentagrama con notas, la desliza a lo largo de la cinta
// (las notas "viajan" alrededor del personaje) y se ve por los dos lados.
// La transparencia de las puntas viene en el color de los vértices.
Shader "Glossy/EstelaMusical"
{
    Properties
    {
        _MainTex ("Tira musical", 2D) = "white" {}
        _Color ("Tinte", Color) = (1,1,1,1)
        _Velocidad ("Velocidad (tiras por segundo)", Float) = 0.1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            float _Velocidad;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex) + float2(-_Time.y * _Velocidad, 0);
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                return tex2D(_MainTex, i.uv) * i.color;
            }
            ENDCG
        }
    }
}
