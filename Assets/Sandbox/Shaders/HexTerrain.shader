// Remplissage des tuiles du HexGrid. Comme dans le tutoriel Catlike Coding
// "Hex Map" (part 2) : chaque cellule porte une vraie couleur RGB finale
// (HexMesh.TerrainVertexColor, a partir de HexCell.terrainType), ecrite
// directement en couleur de vertex. Le shader se contente de l'afficher ;
// le blend aux frontieres entre deux types de terrain vient uniquement de
// l'interpolation GPU standard entre ces couleurs reelles.
Shader "Unlit/HexTerrain"
{
	Properties
	{
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

			#include "UnityCG.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
				fixed4 color : COLOR;
			};

			struct v2f
			{
				fixed4 color : COLOR;
				float4 vertex : SV_POSITION;
			};

			v2f vert (appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.color = v.color;
				return o;
			}

			fixed4 frag (v2f i) : SV_Target
			{
				return i.color;
			}
			ENDCG
		}
	}
	FallBack "Unlit/Texture"
}
