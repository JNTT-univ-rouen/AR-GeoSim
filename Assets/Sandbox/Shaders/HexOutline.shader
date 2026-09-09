// Contour des hexagones du HexGrid : ligne pleine unie (pas de couleur par
// vertex, contrairement a HexTerrain.shader), dessinee sur le submesh
// MeshTopology.Lines construit par HexMesh (cf. AddCellOutline).
Shader "Unlit/HexOutline"
{
	Properties
	{
		_Color ("Color", Color) = (1,1,1,1)
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

			fixed4 _Color;

			struct appdata
			{
				float4 vertex : POSITION;
			};

			struct v2f
			{
				float4 vertex : SV_POSITION;
			};

			v2f vert (appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				return o;
			}

			fixed4 frag (v2f i) : SV_Target
			{
				return _Color;
			}
			ENDCG
		}
	}
	FallBack "Unlit/Texture"
}
