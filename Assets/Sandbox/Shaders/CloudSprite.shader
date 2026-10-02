// Sprite de nuage projete sur le sable.
//
// Meme traitement que HexTerrain/HexOutline : Queue Transparent + ZWrite Off +
// ZTest Always. Le terrain du Sandbox est dessine en opaque via un CommandBuffer
// et son relief varie, alors que le nuage est a une hauteur fixe : avec un test
// de profondeur classique, il disparaitrait derriere les bosses de sable.
//
// _Color teinte le sprite et porte l'opacite d'ensemble (fondu d'apparition,
// disparition en fin de reserve d'eau).

Shader "Unlit/CloudSprite"
{
	Properties
	{
		_MainTex ("Sprite", 2D) = "white" {}
		_Color ("Teinte", Color) = (1,1,1,1)
	}
	SubShader
	{
		Tags { "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" }
		LOD 100

		Pass
		{
			ZWrite Off
			ZTest Always
			Cull Off
			Blend SrcAlpha OneMinusSrcAlpha

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			#include "UnityCG.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
				fixed4 color : COLOR;
			};

			struct v2f
			{
				float2 uv : TEXCOORD0;
				fixed4 color : COLOR;
				float4 vertex : SV_POSITION;
			};

			sampler2D _MainTex;
			float4 _MainTex_ST;
			fixed4 _Color;

			v2f vert (appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = TRANSFORM_TEX(v.uv, _MainTex);
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
	FallBack "Sprites/Default"
}
