// Procedural terrain colouring for sandbox environment shaders (grassland, dried land).
// Works with the procedural mesh + _HeightTex pipeline (same as SandboxShaderHelper).
// Optional: assign a tiled _TerrainAlbedoTex (e.g. grass/soil photo from ambientCG.com) for extra detail.

#ifndef SANDBOX_TERRAIN_VISUAL_INCLUDED
#define SANDBOX_TERRAIN_VISUAL_INCLUDED

float _TerrainNoiseScale;
float _TerrainNoiseStrength;
float _SlopeBlendStrength;
float _TerrainAlbedoStrength;
float _TerrainAlbedoRepeat;

sampler2D _TerrainAlbedoTex;
float4 _TerrainAlbedoTex_ST;

float TerrainHash(float2 p)
{
	return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
}

float TerrainNoise(float2 p)
{
	float2 i = floor(p);
	float2 f = frac(p);
	f = f * f * (3.0 - 2.0 * f);
	float a = TerrainHash(i);
	float b = TerrainHash(i + float2(1.0, 0.0));
	float c = TerrainHash(i + float2(0.0, 1.0));
	float d = TerrainHash(i + float2(1.0, 1.0));
	return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

float TerrainFbm(float2 p)
{
	float v = 0.0;
	float a = 0.5;
	for (int k = 0; k < 4; k++)
	{
		v += a * TerrainNoise(p);
		p *= 2.02;
		a *= 0.5;
	}
	return v;
}

// Approximate surface slope from height map (0 = flat, 1 = steep).
float GetTerrainSlope(float2 uv)
{
	float2 d = float2(0.0015, 0.0015);
	float h = (float)tex2D(_HeightTex, uv).r;
	float hx = (float)tex2D(_HeightTex, uv + float2(d.x, 0)).r - h;
	float hy = (float)tex2D(_HeightTex, uv + float2(0, d.y)).r - h;
	return saturate(length(float2(hx, hy)) * 120.0);
}

float2 GetTerrainAlbedoUV(float2 meshUv)
{
	float repeat = max(_TerrainAlbedoRepeat, 0.001);
	float2 uv = meshUv * repeat;
	// Slight warp when tiling to soften obvious grid repeats (repeat > 1)
	if (repeat > 1.01)
		uv += (TerrainNoise(meshUv * 40.0) - 0.5) * 0.015 / repeat;
	return uv + _TerrainAlbedoTex_ST.zw;
}

float3 SampleTerrainAlbedo(float2 meshUv)
{
	return tex2D(_TerrainAlbedoTex, GetTerrainAlbedoUV(meshUv)).rgb;
}

float3 ApplyTerrainDetail(float3 baseColor, float2 worldUv, float height, float slope)
{
	float n = TerrainFbm(worldUv * _TerrainNoiseScale);
	float patch = (n - 0.5) * _TerrainNoiseStrength;
	float3 color = saturate(baseColor + patch * 0.12);

	// Steeper slopes → exposed soil / rock tint
	float3 slopeTint = lerp(color, color * float3(0.72, 0.68, 0.58), slope * _SlopeBlendStrength);
	color = slopeTint;

	if (_TerrainAlbedoStrength > 0.001)
	{
		float3 albedo = SampleTerrainAlbedo(worldUv);
		// Tint toward photo while keeping height/slope base colours
		color = lerp(color, albedo, _TerrainAlbedoStrength);
	}

	return saturate(color);
}

// Fertile grasslands / bocage — height-driven greens with meadow → pasture → woodland
float3 GetGrasslandTerrainColor(float2 worldUv, float height, float slope)
{
	float t = saturate(height);
	float3 low = float3(0.52, 0.78, 0.38);   // meadow / damp lowland
	float3 mid = float3(0.28, 0.62, 0.32);   // pasture
	float3 high = float3(0.14, 0.42, 0.22);  // hedgerow / woodland shadow
	float3 peak = float3(0.10, 0.34, 0.18);

	float3 baseCol;
	if (t < 0.35)
		baseCol = lerp(low, mid, t / 0.35);
	else if (t < 0.72)
		baseCol = lerp(mid, high, (t - 0.35) / 0.37);
	else
		baseCol = lerp(high, peak, (t - 0.72) / 0.28);

	return ApplyTerrainDetail(baseCol, worldUv, height, slope);
}

// Dried / Mediterranean — straw, ochre, burnt umber by elevation
float3 GetDriedTerrainColor(float2 worldUv, float height, float slope)
{
	float t = saturate(height);
	float3 low = float3(0.88, 0.82, 0.55);   // pale straw (valleys)
	float3 mid = float3(0.78, 0.62, 0.32);   // dry grass / ochre
	float3 high = float3(0.62, 0.42, 0.24);  // parched slope
	float3 peak = float3(0.48, 0.30, 0.18);  // burnt soil / rock

	float3 baseCol;
	if (t < 0.4)
		baseCol = lerp(low, mid, t / 0.4);
	else if (t < 0.75)
		baseCol = lerp(mid, high, (t - 0.4) / 0.35);
	else
		baseCol = lerp(high, peak, (t - 0.75) / 0.25);

	// Drier slopes lean redder
	float3 slopeDry = lerp(baseCol, baseCol * float3(1.08, 0.88, 0.72), slope * 0.5);
	return ApplyTerrainDetail(slopeDry, worldUv, height, slope);
}

// Route / parking — gris asphalte, quasi uniforme, legere usure en hauteur/pente
float3 GetTarmacTerrainColor(float2 worldUv, float height, float slope)
{
	float t = saturate(height);
	float3 low = float3(0.14, 0.14, 0.15);   // asphalte frais, sombre
	float3 mid = float3(0.22, 0.22, 0.23);   // asphalte courant
	float3 high = float3(0.32, 0.31, 0.30);  // use, poussiereux
	float3 peak = float3(0.42, 0.40, 0.37);  // tres use / gravillons apparents

	float3 baseCol;
	if (t < 0.4)
		baseCol = lerp(low, mid, t / 0.4);
	else if (t < 0.75)
		baseCol = lerp(mid, high, (t - 0.4) / 0.35);
	else
		baseCol = lerp(high, peak, (t - 0.75) / 0.25);

	// Pentes marquees -> plus clair (marquage au sol, usure)
	float3 slopeWear = lerp(baseCol, baseCol * float3(1.15, 1.15, 1.1), slope * 0.4);
	return ApplyTerrainDetail(slopeWear, worldUv, height, slope);
}

#endif
