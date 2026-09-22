// Remplissage texture des tuiles du HexGrid, technique "splat map" du
// tutoriel Catlike Coding "Hex Map" part 14 (https://catlikecoding.com/unity/tutorials/hex-map/part-14/) :
// un Texture2DArray contient toutes les textures de terrain (une par
// HexTerrainType), et chaque vertex porte :
//  - une couleur (COLOR) = poids de melange R/G/B pour jusqu'a 3 textures,
//  - un UV2 (TEXCOORD2) = index de texture associe a chaque canal R/G/B.
// Voir HexMesh.TriangulateConnection / AddTriangleTerrainTypes pour la
// construction de ces donnees par triangle.
//
// Queue Transparent + ZWrite Off + ZTest Always : le terrain du Sandbox
// est dessine en opaque (ZWrite On, ZTest LEqual par defaut) via un
// CommandBuffer sur SandboxCamera/SandboxContourCamera/SandboxDataCamera,
// APRES le rendu opaque normal de Unity (CameraEvent.AfterForwardOpaque).
// Comme le HexGrid a une hauteur Z fixe alors que le relief du sable varie,
// un test de profondeur classique ferait disparaitre le HexGrid partout ou
// le sable est plus haut (et inversement). En passant le HexGrid en
// Transparent + ZTest Always, il se dessine systematiquement apres le
// terrain, sans jamais se faire manger par le relief - _Opacity controle
// alors a quel point la vue du Sandbox reste visible en dessous.
Shader "Unlit/HexTerrain"
{
	Properties
	{
		_TerrainTextures ("Terrain Textures", 2DArray) = "" {}
		_TextureScale ("Texture tiling scale", Float) = 0.05
		_Opacity ("Opacity", Range(0,1)) = 0.5
		// Les textures sont volontairement peu saturees (cf. tutoriel), et
		// le blend alpha avec le sable dilue encore la couleur. >1 = plus
		// vif/sature (au dela de la texture d'origine), 1 = inchange,
		// 0 = niveaux de gris.
		_Saturation ("Saturation", Range(0, 3)) = 1.5
	}
	SubShader
	{
		Tags { "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" }
		LOD 10

		Pass
		{
			ZWrite Off
			ZTest Always
			Blend SrcAlpha OneMinusSrcAlpha

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 3.5
			#pragma require 2darray

			#include "UnityCG.cginc"

			UNITY_DECLARE_TEX2DARRAY(_TerrainTextures);
			float _TextureScale;
			float _Opacity;
			float _Saturation;

			struct appdata
			{
				float4 vertex : POSITION;
				fixed4 color : COLOR;
				float3 terrainType : TEXCOORD2;
			};

			struct v2f
			{
				float2 uv : TEXCOORD0;
				fixed4 weights : COLOR;
				float3 terrainType : TEXCOORD1;
				float4 vertex : SV_POSITION;
			};

			v2f vert (appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				// Position au sol comme UV pseudo-monde : la texture se
				// repete uniformement sur toute la grille (pas de couture
				// par hexagone).
				o.uv = v.vertex.xz * _TextureScale;
				o.weights = v.color;
				o.terrainType = v.terrainType;
				return o;
			}

			fixed4 frag (v2f i) : SV_Target
			{
				fixed4 c0 = UNITY_SAMPLE_TEX2DARRAY(_TerrainTextures, float3(i.uv, i.terrainType.x));
				fixed4 c1 = UNITY_SAMPLE_TEX2DARRAY(_TerrainTextures, float3(i.uv, i.terrainType.y));
				fixed4 c2 = UNITY_SAMPLE_TEX2DARRAY(_TerrainTextures, float3(i.uv, i.terrainType.z));
				fixed4 col = c0 * i.weights.r + c1 * i.weights.g + c2 * i.weights.b;

				// Boost de saturation : ecarte la couleur de son niveau de
				// gris equivalent (luminance), pour compenser des textures
				// sources peu saturees + la dilution par le blend alpha.
				float luminance = dot(col.rgb, float3(0.299, 0.587, 0.114));
				col.rgb = lerp(luminance.xxx, col.rgb, _Saturation);

				col.a *= _Opacity;
				return col;
			}
			ENDCG
		}
	}
	FallBack "Unlit/Texture"
}
