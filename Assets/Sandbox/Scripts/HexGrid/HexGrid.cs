using UnityEngine;

public class HexGrid : MonoBehaviour
{
    public int width = 6;
    public int height = 6;

    public HexCell cellPrefab;

    private HexMesh hexMesh;

    HexCell[] cells;

    public HexCell[] Cells => cells;

    public void Retriangulate()
    {
        hexMesh.Triangulate(cells);
    }

    // Emprise brute (non mise a l'echelle) de la grille, dans son propre
    // espace local (X/Z = sol, Y = elevation). Sert de base au calcul du
    // facteur d'echelle pour recouvrir une zone cible (cf. SandboxHexBridge).
    public Bounds GetLocalBounds() => hexMesh.LocalBounds;

    private void Awake()
    {
        hexMesh = GetComponentInChildren<HexMesh>();
        GeneterateGrid();
    }

    private void Start()
    {
        hexMesh.Triangulate(cells);
    }

    public HexCell GetCellAtPosition(Vector3 worldPosition)
    {
        Vector3 position = transform.InverseTransformPoint(worldPosition);
        HexCoordinates coordinates = HexCoordinates.FromPosition(position);
        int index = coordinates.X + coordinates.Z * width + coordinates.Z / 2;
        if (index < 0 || index >= cells.Length) return null;
        return cells[index];
    }

    public void ToggleCellTerrainType(Vector3 worldPosition)
    {
        HexCell cell = GetCellAtPosition(worldPosition);
        if (cell == null) return;

        cell.ToggleTerrainType();
        hexMesh.Triangulate(cells);
    }

    void CreateCell(int x, int z, int i)
    {
        Vector3 position;
        position.x = (x + z * 0.5f - z/2) * (HexMetrics.innerRadius *2f);
        position.y = 0f;
        position.z = z * (HexMetrics.outerRadius*1.5f);

        HexCell cell = cells[i] = Instantiate<HexCell>(cellPrefab);
        cell.transform.SetParent(transform, false);
        cell.transform.localPosition = position;
        cell.coordinates = HexCoordinates.FromOffsetCoordiantes(x, z);
        cell.Elevation = 0;

        if (x > 0)
        {
            cell.SetNeighbor(HexDirection.W,cells[i-1]);
        }

        if (z > 0)
        {
            if ((z & 1) == 0)
            {
                cell.SetNeighbor(HexDirection.SE,cells[i-width]);
                if (x > 0)
                {
                    cell.SetNeighbor(HexDirection.SW, cells[i - width - 1]);
                }
            }
            else
            {
                cell.SetNeighbor(HexDirection.SW, cells[i-width]);
                if (x < width - 1)
                {
                    cell.SetNeighbor(HexDirection.SE, cells[i - width + 1]);
                }
            }
        }
    }

    void GeneterateGrid()
    {
        cells = new HexCell[width * height];

        for (int z = 0, i = 0; z < height; z++)
        {
            for (int x = 0; x < width; x++)
            {
                CreateCell(x,z, i++);
            }
        }
    }
}
