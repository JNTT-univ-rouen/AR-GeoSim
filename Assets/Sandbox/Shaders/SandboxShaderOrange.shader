//  SandboxShaderOrange.shader — Dried / arid terrain (normal season in water sim UI)
//
//  Natural look: height gradient + procedural variation + slope drying.
//  Optional _TerrainAlbedoTex: tileable dry grass or soil photo.

Shader "Unlit/SandboxShaderOrange"
{
	Properties
	{
		_HeightTex("Height Texture", 2D) = "white" {}
		_LabelMaskTex("Label Mask Texture", 2D) = "white" {}
		_MetaballTex("Metaball Texture", 2D) = "white" {}
		_WaterSurfaceTex("Water Surface Texture", 2D) = "white" {}
		_WaterColorTex("Water Color Texture", 2D) = "white" {}
		_TerrainAlbedoTex("Terrain Albedo (optional)", 2D) = "gray" {}
		_TerrainNoiseScale("Noise scale", Float) = 14
		_TerrainNoiseStrength("Noise strength", Range(0, 1)) = 0.4
		_SlopeBlendStrength("Slope soil blend", Range(0, 1)) = 0.5
		_TerrainAlbedoStrength("Photo texture blend", Range(0, 1)) = 0
		_TerrainAlbedoRepeat("Texture repeat (1 = once over sandbox)", Float) = 1
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
				float2 uv_MetaballTex : TEXCOORD2;
				float2 uv_WaterSurfaceTex : TEXCOORD3;
				float4 vertex : SV_POSITION;
			};

			#include "SandboxShaderHelper.cginc"
			#include "SandboxTerrainVisual.cginc"

			sampler2D _MetaballTex;
			sampler2D _WaterSurfaceTex;
			sampler2D _WaterColorTex;

			float4 _MetaballTex_ST;
			float4 _WaterSurfaceTex_ST;
			float4 _WaterColorTex_ST;

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
				
				fixed4 textColor = (1 - contourMapFrag.textIntensity) * fixed4(1, 1, 1, 1) +
					contourMapFrag.textIntensity * fixed4(0, 0, 0, 1);

				float slope = GetTerrainSlope(i.uv_HeightTex);
				float3 landRgb = GetDriedTerrainColor(i.uv_HeightTex, contourMapFrag.normalisedHeight, slope);
				fixed4 baseColor = fixed4(landRgb, 1);

				float metaballValue = tex2D(_MetaballTex, i.uv_MetaballTex).r;
				float waterSurfaceHeightLarge = (float)tex2D(_WaterSurfaceTex, i.uv_WaterSurfaceTex);
				fixed4 waterColor = tex2D(_WaterColorTex, float2((waterSurfaceHeightLarge - 0.26) * 6.5f, 0));
				int inWater = metaballValue > 0.3;
				fixed4 bodyColor = inWater == 1 ? waterColor : baseColor;

				fixed4 contrastMajor = inWater == 1 ? WHITE_COLOUR : BLACK_COLOUR;
				fixed4 contrastMinor = inWater == 1 ? WHITE_COLOUR - MINOR_CONTOUR_COLOUR : MINOR_CONTOUR_COLOUR;

				fixed4 finalColor = drawMajorContourLine == 1 ? contrastMajor : bodyColor;
				finalColor = drawMinorContourLine == 1 ? contrastMinor : finalColor;
				finalColor = contourMapFrag.onText == 1 ? textColor : finalColor;

				return finalColor;
			}
			ENDCG
		}
	}
}
