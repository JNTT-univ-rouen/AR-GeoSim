//  SandboxShaderViolet.shader — camaieu violet par altitude
//
//  Lecture du relief par la couleur : violet clair sur les hauteurs, violet
//  profond dans les creux. Les bandes suivent les intervalles de courbes de
//  niveau (discreteNormalisedHeight), ce qui donne une carte lisible plutot
//  qu'un degrade continu : chaque palier correspond a une isohypse.
//
//  Construit sur la meme base que SandboxShaderBlackAndWhite : memes courbes de
//  niveau, memes labels, meme surcouche d'eau.

Shader "Unlit/SandboxShaderViolet"
{
	Properties
	{
		_HeightTex("Height Texture", 2D) = "white" {}
		_LabelMaskTex("Label Mask Texture", 2D) = "white" {}
		_MetaballTex("Metaball Texture", 2D) = "white" {}
		_WaterSurfaceTex("Water Surface Texture", 2D) = "white" {}
		_WaterColorTex("Water Color Texture", 2D) = "white" {}
		_VioletLow("Violet des creux", Color) = (0.17, 0.04, 0.29, 1)
		_VioletHigh("Violet des sommets", Color) = (0.91, 0.85, 0.97, 1)
		_ContourStride("Contour Stride (mm)", float) = 20
		_ContourWidth("Contour Width", float) = 1
		_MinorContours("Minor Contours", float) = 0
		_MinDepth("Min Depth (mm)", float) = 1000
		_MaxDepth("Max Depth (mm)", float) = 2000
	}
	SubShader
	{
		Tags { "RenderType"="Opaque" }
		LOD 100

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			#include "UnityCG.cginc"

			struct v2f
			{
				float2 uv_HeightTex : TEXCOORD0;
				float2 uv_LabelMaskTex : TEXCOORD1;
				float2 uv_MetaballTex: TEXCOORD2;
				float2 uv_WaterSurfaceTex: TEXCOORD3;
				float4 vertex : SV_POSITION;
			};

			#include "SandboxShaderHelper.cginc"

			sampler2D _MetaballTex;
			sampler2D _WaterSurfaceTex;
			sampler2D _WaterColorTex;

			float4 _MetaballTex_ST;
			float4 _WaterSurfaceTex_ST;
			float4 _WaterColorTex_ST;

			fixed4 _VioletLow;
			fixed4 _VioletHigh;

			v2f vert (uint id : SV_VertexID)
			{
				v2f o;
				uint vIndex = GetVertexID(id);

				o.vertex = mul(UNITY_MATRIX_VP, mul(Mat_Object2World, float4(VertexBuffer[vIndex], 1.0f)));
				o.uv_HeightTex = TRANSFORM_TEX(UVBuffer[vIndex], _HeightTex);
				o.uv_LabelMaskTex = TRANSFORM_TEX(UVBuffer[vIndex], _LabelMaskTex);
				o.uv_MetaballTex = TRANSFORM_TEX(UVBuffer[vIndex], _MetaballTex);
				o.uv_WaterSurfaceTex = TRANSFORM_TEX(UVBuffer[vIndex], _WaterSurfaceTex);

				return o;
			}

			fixed4 frag (v2f i) : SV_Target
			{
				ContourMapFrag contourMapFrag = GetContourMap(i);

				int onText = contourMapFrag.onText == 1 || contourMapFrag.onTextMask == 1;
				int drawMajorContourLine = contourMapFrag.onMajorContourLine == 1 && onText == 0;
				int drawMinorContourLine = contourMapFrag.onMinorContourLine == 1 && onText == 0;

				// normalisedHeight vaut 1 sur les points hauts du sable et 0 au
				// plus bas : le camaieu va donc du violet profond au violet clair.
				// Version "discrete" = un palier par intervalle de courbe.
				float height = saturate(contourMapFrag.discreteNormalisedHeight);
				fixed4 violet = lerp(_VioletLow, _VioletHigh, height);

				// Le texte reste lisible sur tout le camaieu : clair sur les
				// teintes foncees, fonce sur les teintes claires.
				fixed4 textBase = height > 0.5 ? BLACK_COLOUR : WHITE_COLOUR;
				fixed4 textColor = (1 - contourMapFrag.textIntensity) * violet +
					contourMapFrag.textIntensity * textBase;

				// Surcouche eau, identique aux autres shaders de terrain.
				float metaballValue = tex2D(_MetaballTex, i.uv_MetaballTex).r;
				float waterSurfaceHeightLarge = (float)tex2D(_WaterSurfaceTex, i.uv_WaterSurfaceTex);
				fixed4 waterColor = tex2D(_WaterColorTex, float2((waterSurfaceHeightLarge - 0.26) * 6.5f, 0));
				int inWater = metaballValue > 0.3;
				fixed4 bodyColor = inWater == 1 ? waterColor : violet;

				// Courbes de niveau : contrastees par rapport au fond local.
				fixed4 contrastMajor = height > 0.5 ? BLACK_COLOUR : WHITE_COLOUR;
				contrastMajor = inWater == 1 ? WHITE_COLOUR : contrastMajor;
				fixed4 contrastMinor = lerp(bodyColor, contrastMajor, 0.5);

				fixed4 finalColor = drawMajorContourLine == 1 ? contrastMajor : bodyColor;
				finalColor = drawMinorContourLine == 1 ? contrastMinor : finalColor;
				finalColor = contourMapFrag.onText == 1 ? textColor : finalColor;

				return finalColor;
			}
			ENDCG
		}
	}
}
