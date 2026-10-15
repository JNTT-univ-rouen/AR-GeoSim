using UnityEngine;

public class HexGrid : MonoBehaviour
{
    public int width = 6;
    public int height = 6;

    public HexCell cellPrefab;

    private HexMesh hexMesh;

    HexCell[] cells;

    public HexCell[] Cells => cells;

    // Vrai une fois le mesh triangule (fin de Start). Entre Awake et Start
    // (ex: objet active en cours de partie), les bounds du mesh sont vides :
    // ne pas s'en servir pour recaler la grille (cf. SandboxHexBridge).
    public bool IsReady { get; private set; }

    [Tooltip("Pose une carte de depart au lancement : centre-ville en goudron, avenues, tissu urbain sec, campagne verte (cf. HexDefaultMap). Decoche : la grille demarre vierge, aucune cellule n'est dessinee tant qu'une carte ArUco ou un clic n'a rien pose.")]
    [SerializeField] private bool paintDefaultMap = true;

    [Tooltip("Desactive l'objet une fois la grille construite. Les cellules existent alors deja : les cartes ArUco peuvent les peindre et les nuages y trouvent leur decoupage en zones, meme grille cachee. Sans ca, un objet desactive des le depart n'executerait jamais son Awake et n'aurait aucune cellule.")]
    [SerializeField] private bool hiddenAtStartup = true;

    /// <summary>
    /// Affichage de la grille. Desactive l'objet entier, comme avant : le
    /// Sandbox continue de tourner sans elle, et plus rien ne la met a jour.
    /// La construction, elle, a deja eu lieu au lancement (cf. hiddenAtStartup).
    /// </summary>
    public bool Visible
    {
        get => gameObject.activeSelf;
        set => gameObject.SetActive(value);
    }

    public void Retriangulate()
    {
        hexMesh.Triangulate(cells);
    }

    // Emprise brute (non mise a l'echelle) de la grille, dans son propre
    // espace local (X/Z = sol, Y = elevation). Sert de base au calcul du
    // facteur d'echelle pour recouvrir une zone cible (cf. SandboxHexBridge).
    //
    // Calculee a partir des CELLULES, pas du mesh : depuis que seules les
    // cellules peintes sont dessinees, les bounds du mesh ne couvrent qu'une
    // partie de la grille, voire rien du tout au lancement. Les utiliser
    // donnait une echelle delirante, et un unique hexagone etire sur tout le
    // bac - le "bloc homogene" observe.
    public Bounds GetLocalBounds()
    {
        if (cells == null || cells.Length == 0) return new Bounds();

        Bounds bounds = new Bounds(cells[0].Position, Vector3.zero);
        for (int i = 1; i < cells.Length; i++)
        {
            bounds.Encapsulate(cells[i].Position);
        }

        // Les positions sont les centres : on ajoute le rayon d'un hexagone de
        // chaque cote pour obtenir l'emprise reelle.
        bounds.Expand(new Vector3(HexMetrics.innerRadius * 2f, 0f, HexMetrics.outerRadius * 2f));
        return bounds;
    }

    private void Awake()
    {
        hexMesh = GetComponentInChildren<HexMesh>();
        GeneterateGrid();
    }

    private void Start()
    {
        if (paintDefaultMap) HexDefaultMap.Apply(cells, width, height);

        hexMesh.Triangulate(cells);
        IsReady = true;

        // Construite puis rangee : les cellules restent accessibles alors que
        // rien n'est dessine ni mis a jour.
        if (hiddenAtStartup) gameObject.SetActive(false);
    }

    public HexCell GetCellAtPosition(Vector3 worldPosition)
    {
        Vector3 position = transform.InverseTransformPoint(worldPosition);
        HexCoordinates coordinates = HexCoordinates.FromPosition(position);

        // Colonne et ligne en coordonnees "offset", celles de la grille.
        int column = coordinates.X + coordinates.Z / 2;
        int row = coordinates.Z;

        // Tester seulement l'index global ne suffit pas : un point situe juste
        // a cote de la grille donne une colonne hors limites, dont l'index
        // retombe sur une cellule du bord oppose. Une carte posee au bord
        // peignait alors la mauvaise tuile.
        if (column < 0 || column >= width) return null;
        if (row < 0 || row >= height) return null;

        return cells[column + row * width];
    }

    public void ToggleCellTerrainType(Vector3 worldPosition)
    {
        HexCell cell = GetCellAtPosition(worldPosition);
        if (cell == null) return;

        cell.ToggleTerrainType();
        hexMesh.Triangulate(cells);
    }

    /// <summary>
    /// Pose un terrain sur la cellule qui contient ce point. Renvoie faux si le
    /// point tombe hors de la grille, ou si la cellule portait deja ce terrain
    /// (inutile de reconstruire le mesh dans ce cas).
    /// </summary>
    public bool SetCellTerrainType(Vector3 worldPosition, HexTerrainType type)
    {
        HexCell cell = GetCellAtPosition(worldPosition);
        if (cell == null) return false;
        if (cell.Painted && cell.terrainType == type) return false;

        cell.SetTerrainType(type);
        hexMesh.Triangulate(cells);
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
