// Contour des hexagones du HexGrid : ligne pleine unie (pas de couleur par
// vertex, contrairement a HexTerrain.shader), dessinee sur le submesh
// MeshTopology.Lines construit par HexMesh (cf. AddCellOutline).
//
// Meme traitement Transparent/ZTest Always que HexTerrain.shader (voir son
// commentaire d'en-tete) : le contour doit lui aussi toujours passer devant
// le terrain du Sandbox, quel que soit le relief local du sable.
Shader "Unlit/HexOutline"
{
	Properties
	{
		_Color ("Color", Color) = (1,1,1,1)
		_Opacity ("Opacity", Range(0,1)) = 1
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

			#include "UnityCG.cginc"

			fixed4 _Color;
			float _Opacity;

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
				fixed4 col = _Color;
				col.a *= _Opacity;
				return col;
			}
			ENDCG
		}
	}
	FallBack "Unlit/Texture"
}
