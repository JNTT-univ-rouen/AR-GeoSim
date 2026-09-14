using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class HexMesh : MonoBehaviour {

	// Mesh affiché : remplissage texturé par type de terrain (submesh 0,
	// triangles) + contour blanc de chaque hexagone (submesh 1, triangles
	// aussi : un ruban fin le long de chaque arête). On n'utilise pas
	// MeshTopology.Lines pour le contour car sa largeur est fixée à 1px et
	// non réglable sur D3D11 (pas de glLineWidth équivalent).
	[Tooltip("Épaisseur du contour, en unités locales du HexGrid (mêmes unités que HexMetrics.outerRadius).")]
	public float outlineThickness = 0.4f;

	// Léger décalage vers le haut du contour pour éviter le z-fighting avec
	// le remplissage sous-jacent (quasi coplanaire par endroits).
	const float outlineYLift = 0.02f;

	Mesh hexMesh;
	List<Vector3> vertices;

	// Poids de melange "splat map" par vertex (comme le tutoriel Catlike
	// Coding "Hex Map" part 14) : R/G/B = poids des jusqu'a 3 textures de
	// terrain identifiees par terrainTypes (meme index de vertex). Une
	// cellule pleine utilise (1,0,0) ; le pont vers un voisin degrade
	// (1,0,0)->(0,1,0) ; le triangle de coin entre 3 cellules degrade vers
	// (0,0,1) pour la troisieme. Contrairement a interpoler un index de
	// categorie (ancienne version), interpoler des poids a un sens : le
	// shader reconstruit la couleur finale en sommant Texture[i] * poids[i].
	List<Color> weights;

	// Index de texture (dans le Texture2DArray _TerrainTextures) associe a
	// chaque canal de poids R/G/B, pour ce vertex. cf. HexTerrain.shader.
	List<Vector3> terrainTypes;

	List<int> triangles;

	List<Vector3> outlineVertices;
	List<int> outlineIndices;

	// Mesh de collision : mêmes hexagones pleins, utilisé uniquement par le
	// MeshCollider (picking/raycast), jamais affiché directement.
	Mesh collisionMesh;

	MeshCollider meshCollider;

	// Emprise du mesh affiché, dans l'espace local du HexGrid (X/Z = sol, Y = elevation).
	public Bounds LocalBounds => hexMesh.bounds;

	static readonly Color Weights1 = new Color(1f, 0f, 0f);
	static readonly Color Weights2 = new Color(0f, 1f, 0f);
	static readonly Color Weights3 = new Color(0f, 0f, 1f);

	void Awake () {
		GetComponent<MeshFilter>().mesh = hexMesh = new Mesh();
		hexMesh.name = "Hex Mesh";
		vertices = new List<Vector3>();
		weights = new List<Color>();
		terrainTypes = new List<Vector3>();
		triangles = new List<int>();
		outlineVertices = new List<Vector3>();
		outlineIndices = new List<int>();

		collisionMesh = new Mesh();
		collisionMesh.name = "Hex Mesh (Collision)";
		meshCollider = gameObject.AddComponent<MeshCollider>();
	}

	public void Triangulate (HexCell[] cells) {
		hexMesh.Clear();
		vertices.Clear();
		weights.Clear();
		terrainTypes.Clear();
		triangles.Clear();
		outlineVertices.Clear();
		outlineIndices.Clear();

		for (int i = 0; i < cells.Length; i++) {
			Triangulate(cells[i]);
			AddCellOutline(cells[i]);
		}

		// Les vertices de contour sont concaténés après ceux du remplissage,
		// dans le même buffer partagé par les deux submeshes.
		int fillVertexCount = vertices.Count;
		List<Vector3> allVertices = new List<Vector3>(vertices);
		allVertices.AddRange(outlineVertices);

		List<int> offsetOutlineIndices = new List<int>(outlineIndices.Count);
		for (int i = 0; i < outlineIndices.Count; i++) {
			offsetOutlineIndices.Add(outlineIndices[i] + fillVertexCount);
		}

		// Mesh.colors / UV2 doivent couvrir tous les vertices (remplissage +
		// contour), même si le shader du contour ne les lit pas.
		List<Color> allWeights = new List<Color>(weights);
		List<Vector3> allTerrainTypes = new List<Vector3>(terrainTypes);
		for (int i = 0; i < outlineVertices.Count; i++) {
			allWeights.Add(Color.white);
			allTerrainTypes.Add(Vector3.zero);
		}

		hexMesh.vertices = allVertices.ToArray();
		hexMesh.colors = allWeights.ToArray();
		hexMesh.SetUVs(2, allTerrainTypes);
		hexMesh.subMeshCount = 2;
		hexMesh.SetTriangles(triangles.ToArray(), 0);
		hexMesh.SetTriangles(offsetOutlineIndices.ToArray(), 1);
		hexMesh.RecalculateNormals();
		hexMesh.RecalculateBounds();

		collisionMesh.vertices = vertices.ToArray();
		collisionMesh.triangles = triangles.ToArray();
		collisionMesh.RecalculateNormals();
		collisionMesh.RecalculateBounds();
		meshCollider.sharedMesh = collisionMesh;
	}

	// Ruban fin le long des 6 arêtes extérieures de l'hexagone (coins
	// pleins, non rétrécis) : les hexagones voisins partagent ainsi la
	// même arête (et dessinent chacun le même ruban dessus, sans souci
	// puisque les deux calculs donnent une géométrie identique).
	void AddCellOutline (HexCell cell) {
		Vector3 center = cell.transform.localPosition;

		for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++) {
			Vector3 p1 = center + HexMetrics.GetFirstCorner(d);
			Vector3 p2 = center + HexMetrics.GetSecondCorner(d);
			AddOutlineSegment(p1, p2);
		}
	}

	// Ajoute un petit quad (2 triangles) formant un ruban de largeur
	// outlineThickness, centré sur le segment p1->p2, dans le plan sol X/Z.
	void AddOutlineSegment (Vector3 p1, Vector3 p2) {
		Vector3 dir = (p2 - p1).normalized;
		Vector3 perp = new Vector3(-dir.z, 0f, dir.x) * (outlineThickness * 0.5f);
		Vector3 lift = new Vector3(0f, outlineYLift, 0f);

		Vector3 a = p1 + perp + lift;
		Vector3 b = p2 + perp + lift;
		Vector3 c = p2 - perp + lift;
		Vector3 d = p1 - perp + lift;

		int vertexIndex = outlineVertices.Count;
		outlineVertices.Add(a);
		outlineVertices.Add(b);
		outlineVertices.Add(c);
		outlineVertices.Add(d);

		outlineIndices.Add(vertexIndex);
		outlineIndices.Add(vertexIndex + 1);
		outlineIndices.Add(vertexIndex + 2);
		outlineIndices.Add(vertexIndex);
		outlineIndices.Add(vertexIndex + 2);
		outlineIndices.Add(vertexIndex + 3);
	}

	void Triangulate (HexCell cell) {
		for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++) {
			Triangulate(d, cell);
		}
	}

	void Triangulate (HexDirection direction, HexCell cell) {
		Vector3 center = cell.transform.localPosition;
		Vector3 v1 = center + HexMetrics.GetFirstSolidCorner(direction);
		Vector3 v2 = center + HexMetrics.GetSecondSolidCorner(direction);

		AddTriangle(center, v1, v2);
		AddTriangleColor(Weights1);
		AddTriangleTerrainTypes(cell.terrainType);

		if (direction <= HexDirection.SE) {
			TriangulateConnection(direction, cell, v1, v2);
		}
	}

	void TriangulateConnection (
		HexDirection direction, HexCell cell, Vector3 v1, Vector3 v2
	) {
		HexCell neighbor = cell.GetNeighbor(direction);
		if (neighbor == null) {
			return;
		}
		Vector3 bridge = HexMetrics.GetBridge(direction);
		Vector3 v3 = v1 + bridge;
		Vector3 v4 = v2 + bridge;
		v3.y = neighbor.Position.y;
		v4.y = neighbor.Position.y;

		AddQuad(v1, v2, v3, v4);
		AddQuadColor(Weights1, Weights2);
		AddQuadTerrainTypes(cell.terrainType, neighbor.terrainType);

		HexCell nextNeighbor = cell.GetNeighbor(direction.Next());
		if (direction <= HexDirection.E && nextNeighbor != null) {
			Vector3 v5 = v2 + HexMetrics.GetBridge(direction.Next());
			v5.y = nextNeighbor.Position.y;
			AddTriangle(v2, v4, v5);
			AddTriangleColor(Weights1, Weights2, Weights3);
			AddTriangleTerrainTypes(cell.terrainType, neighbor.terrainType, nextNeighbor.terrainType);
		}

	}

	void AddTriangle (Vector3 v1, Vector3 v2, Vector3 v3) {
		int vertexIndex = vertices.Count;
		vertices.Add(v1);
		vertices.Add(v2);
		vertices.Add(v3);
		triangles.Add(vertexIndex);
		triangles.Add(vertexIndex + 1);
		triangles.Add(vertexIndex + 2);
	}

	void AddTriangleColor (Color color) {
		weights.Add(color);
		weights.Add(color);
		weights.Add(color);
	}

	void AddTriangleColor (Color c1, Color c2, Color c3) {
		weights.Add(c1);
		weights.Add(c2);
		weights.Add(c3);
	}

	// Une seule texture pour tout le triangle (fan plein d'une cellule) :
	// le canal R (poids 1) pointe sur cette texture, G/B sont inutilises.
	void AddTriangleTerrainTypes (HexTerrainType type) {
		AddTriangleTerrainTypes(type, type, type);
	}

	void AddTriangleTerrainTypes (HexTerrainType type1, HexTerrainType type2, HexTerrainType type3) {
		Vector3 types = new Vector3((int)type1, (int)type2, (int)type3);
		terrainTypes.Add(types);
		terrainTypes.Add(types);
		terrainTypes.Add(types);
	}

	void AddQuad (Vector3 v1, Vector3 v2, Vector3 v3, Vector3 v4) {
		int vertexIndex = vertices.Count;
		vertices.Add(v1);
		vertices.Add(v2);
		vertices.Add(v3);
		vertices.Add(v4);
		triangles.Add(vertexIndex);
		triangles.Add(vertexIndex + 2);
		triangles.Add(vertexIndex + 1);
		triangles.Add(vertexIndex + 1);
		triangles.Add(vertexIndex + 2);
		triangles.Add(vertexIndex + 3);
	}

	void AddQuadColor (Color c1, Color c2) {
		weights.Add(c1);
		weights.Add(c1);
		weights.Add(c2);
		weights.Add(c2);
	}

	// Pont entre 2 cellules : canal B inutilise (poids 0) sur tout le quad,
	// on y remet type1 par simplicite (valeur d'index toujours valide).
	void AddQuadTerrainTypes (HexTerrainType type1, HexTerrainType type2) {
		Vector3 types = new Vector3((int)type1, (int)type2, (int)type1);
		terrainTypes.Add(types);
		terrainTypes.Add(types);
		terrainTypes.Add(types);
		terrainTypes.Add(types);
	}
}
