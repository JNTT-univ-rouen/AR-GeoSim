using UnityEngine;

public class HexGrid : MonoBehaviour
{
    public int width = 6;
    public int height = 6;

    public HexCell cellPrefab;

    // Masquer la grille ne desactive PAS son GameObject : les cellules
    // doivent exister et rester calees sur le Sandbox (SandboxHexBridge) pour
    // que les marqueurs ArUco puissent les modifier et que les nuages s'en
    // servent comme zones, meme quand rien n'est affiche.
    [Tooltip("Grille affichee au lancement. Decoche : la grille existe (cellules modifiables, zones des nuages) mais n'est pas dessinee tant qu'on ne l'affiche pas (Sandbox Settings, ou premiere tuile posee par un marqueur).")]
    [SerializeField] private bool visibleOnStart = false;

    private HexMesh hexMesh;
    private MeshRenderer hexRenderer;

    HexCell[] cells;

    public HexCell[] Cells => cells;

    // Vrai une fois le mesh triangule (fin de Start). Entre Awake et Start
    // (ex: objet active en cours de partie), les bounds du mesh sont vides :
    // ne pas s'en servir pour recaler la grille (cf. SandboxHexBridge).
    public bool IsReady { get; private set; }

    public void Retriangulate()
    {
        hexMesh.Triangulate(cells);
    }

    // Emprise brute (non mise a l'echelle) de la grille, dans son propre
    // espace local (X/Z = sol, Y = elevation). Sert de base au calcul du
    // facteur d'echelle pour recouvrir une zone cible (cf. SandboxHexBridge).
    public Bounds GetLocalBounds() => hexMesh.LocalBounds;

    /// <summary>
    /// Affichage de la grille (remplissage + contours). Ne touche qu'au rendu :
    /// cellules, collider et recalage sur le Sandbox continuent de vivre.
    /// </summary>
    public bool Visible
    {
        get => hexRenderer != null && hexRenderer.enabled;
        set
        {
            if (hexRenderer != null) hexRenderer.enabled = value;
        }
    }

    private void Awake()
    {
        hexMesh = GetComponentInChildren<HexMesh>();
        hexRenderer = hexMesh.GetComponent<MeshRenderer>();
        hexRenderer.enabled = visibleOnStart;
        GeneterateGrid();
    }

    private void Start()
    {
        hexMesh.Triangulate(cells);
        IsReady = true;
    }

    public HexCell GetCellAtPosition(Vector3 worldPosition)
    {
        if (cells == null) return null;

        Vector3 position = transform.InverseTransformPoint(worldPosition);
        HexCoordinates coordinates = HexCoordinates.FromPosition(position);

        // Retour en coordonnees "offset" (colonne, ligne) pour verifier chaque
        // axe separement : un simple test sur l'index lineaire laisserait un
        // point situe juste a gauche ou a droite de la grille retomber sur une
        // cellule du bord oppose, une ligne plus haut ou plus bas.
        int z = coordinates.Z;
        int x = coordinates.X + z / 2;
        if (z < 0 || z >= height || x < 0 || x >= width) return null;
        return cells[x + z * width];
    }

    public void ToggleCellTerrainType(Vector3 worldPosition)
    {
        HexCell cell = GetCellAtPosition(worldPosition);
        if (cell == null) return;

        cell.ToggleTerrainType();
        hexMesh.Triangulate(cells);
    }

    /// <summary>
    /// Donne un type de terrain a une cellule. Ne retriangule que si le type
    /// change : un marqueur pose reste detecte a chaque image, il ne faut pas
    /// reconstruire tout le mesh a chaque detection. Renvoie vrai si la
    /// cellule a change.
    /// </summary>
    public bool SetCellTerrainType(HexCell cell, HexTerrainType terrainType)
    {
        if (cell == null || cell.terrainType == terrainType) return false;

        cell.terrainType = terrainType;
        if (IsReady) hexMesh.Triangulate(cells);
        return true;
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
