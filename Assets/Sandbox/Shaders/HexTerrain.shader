// Remplissage texture des tuiles du HexGrid, technique "splat map" du
// tutoriel Catlike Coding "Hex Map" part 14 (https://catlikecoding.com/unity/tutorials/hex-map/part-14/) :
// un Texture2DArray contient toutes les textures de terrain (une par
// HexTerrainType), et chaque vertex porte :
//  - une couleur (COLOR) = poids de melange R/G/B pour jusqu'a 3 textures,
//  - un UV2 (TEXCOORD2) = index de texture associe a chaque canal R/G/B.
// Voir HexMesh.TriangulateConnection / AddTriangleTerrainTypes pour la
// construction de ces donnees par triangle.
Shader "Unlit/HexTerrain"
{
	Properties
	{
		_TerrainTextures ("Terrain Textures", 2DArray) = "" {}
		_TextureScale ("Texture tiling scale", Float) = 0.05
	}
	SubShader
	{
		Tags { "RenderType"="Opaque" }
		LOD 10

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 3.5
			#pragma require 2darray

			#include "UnityCG.cginc"

			UNITY_DECLARE_TEX2DARRAY(_TerrainTextures);
			float _TextureScale;

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
				return c0 * i.weights.r + c1 * i.weights.g + c2 * i.weights.b;
			}
			ENDCG
		}
	}
	FallBack "Unlit/Texture"
}
