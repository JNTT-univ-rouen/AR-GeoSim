using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class HexMesh : MonoBehaviour {

	// Mesh affiché : remplissage coloré par type de terrain (submesh 0,
	// triangles) + contour blanc de chaque hexagone (submesh 1, triangles
	// aussi : un ruban fin le long de chaque arête). On n'utilise pas
	// MeshTopology.Lines pour le contour car sa largeur est fixée à 1px et
	// non réglable sur D3D11 (pas de glLineWidth équivalent).
	[Tooltip("Épaisseur du contour, en unités locales du HexGrid (mêmes unités que HexMetrics.outerRadius).")]
	public float outlineThickness = 0.4f;

	// Couleur finale (pas un index !) par type de terrain, comme dans le
	// tutoriel Catlike Coding "Hex Map" (part 2) : cell.color y est deja la
	// couleur RGB a afficher, ecrite telle quelle en couleur de vertex. Le
	// blend aux frontieres vient alors juste de l'interpolation GPU normale
	// entre ces couleurs reelles - contrairement a interpoler un index de
	// categorie puis le redecoder dans le shader (ce qui produisait des
	// couleurs de transition fausses, ex: Grassland/Tarmac qui traversait
	// la valeur de Dried au milieu de la frontiere).
	[Header("Couleurs par type de terrain")]
	public Color grasslandColor = new Color(0.28f, 0.62f, 0.32f);
	public Color driedColor = new Color(0.78f, 0.62f, 0.32f);
	public Color tarmacColor = new Color(0.22f, 0.22f, 0.23f);

	// Léger décalage vers le haut du contour pour éviter le z-fighting avec
	// le remplissage sous-jacent (quasi coplanaire par endroits).
	const float outlineYLift = 0.02f;

	Mesh hexMesh;
	List<Vector3> vertices;
	List<Color> colors;
	List<int> triangles;

	List<Vector3> outlineVertices;
	List<int> outlineIndices;

	// Mesh de collision : mêmes hexagones pleins, utilisé uniquement par le
	// MeshCollider (picking/raycast), jamais affiché directement.
	Mesh collisionMesh;

	MeshCollider meshCollider;

	// Emprise du mesh affiché, dans l'espace local du HexGrid (X/Z = sol, Y = elevation).
	public Bounds LocalBounds => hexMesh.bounds;

	void Awake () {
		GetComponent<MeshFilter>().mesh = hexMesh = new Mesh();
		hexMesh.name = "Hex Mesh";
		vertices = new List<Vector3>();
		colors = new List<Color>();
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
		colors.Clear();
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

		// Mesh.colors doit couvrir tous les vertices (remplissage + contour),
		// même si le shader du contour ne lit pas cette couleur.
		List<Color> allColors = new List<Color>(colors);
		for (int i = 0; i < outlineVertices.Count; i++) {
			allColors.Add(Color.white);
		}

		hexMesh.vertices = allVertices.ToArray();
		hexMesh.colors = allColors.ToArray();
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
		AddTriangleColor(TerrainVertexColor(cell));

		if (direction <= HexDirection.SE) {
			TriangulateConnection(direction, cell, v1, v2);
		}
	}

	Color TerrainVertexColor (HexCell cell) {
		switch (cell.terrainType) {
			case HexTerrainType.Dried: return driedColor;
			case HexTerrainType.Tarmac: return tarmacColor;
			default: return grasslandColor;
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
		AddQuadColor(TerrainVertexColor(cell), TerrainVertexColor(neighbor));

		HexCell nextNeighbor = cell.GetNeighbor(direction.Next());
		if (direction <= HexDirection.E && nextNeighbor != null) {
			Vector3 v5 = v2 + HexMetrics.GetBridge(direction.Next());
			v5.y = nextNeighbor.Position.y;
			AddTriangle(v2, v4, v5);
			AddTriangleColor(TerrainVertexColor(cell), TerrainVertexColor(neighbor), TerrainVertexColor(nextNeighbor));
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
		colors.Add(color);
		colors.Add(color);
		colors.Add(color);
	}

	void AddTriangleColor (Color c1, Color c2, Color c3) {
		colors.Add(c1);
		colors.Add(c2);
		colors.Add(c3);
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
		colors.Add(c1);
		colors.Add(c1);
		colors.Add(c2);
		colors.Add(c2);
	}

	void AddQuadColor (Color c1, Color c2, Color c3, Color c4) {
		colors.Add(c1);
		colors.Add(c2);
		colors.Add(c3);
		colors.Add(c4);
	}
}
