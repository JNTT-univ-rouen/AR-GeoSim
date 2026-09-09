using UnityEngine;

public enum HexTerrainType
{
    Grassland,
    Dried,
    Tarmac
}

public class HexCell : MonoBehaviour
{

    public HexCoordinates coordinates;
    public HexTerrainType terrainType = HexTerrainType.Grassland;

    // Profondeur Sandbox moyenne sous cet hexagone, normalisée (0 = MaxDepth,
    // 1 = MinDepth, cf. SandboxHexBridge.UpdateHexDepthData). Donnée pure :
    // n'affecte pas le mesh (le HexGrid reste plat, les caméras du Sandbox
    // étant toutes orthographiques vue du dessus, un relief 3D n'y serait
    // de toute façon pas visible). Sert de base à de futures propriétés
    // physiques par tuile (ex: pente = écart avec les voisins).
    public float DepthT;

    [SerializeField]
    HexCell[] neighbors;

    int elevation = int.MinValue; // valeur impossible pour forcer la 1ère mise à jour

    public Vector3 Position => transform.localPosition;

    public int Elevation
    {
        get => elevation;
        set
        {
            if (elevation == value) return;
            elevation = value;

            Vector3 position = transform.localPosition;
            position.y = value * HexMetrics.elevationStep;
            transform.localPosition = position;
        }
    }

    public void ToggleTerrainType()
    {
        int next = ((int)terrainType + 1) % System.Enum.GetValues(typeof(HexTerrainType)).Length;
        terrainType = (HexTerrainType)next;
    }

    public HexCell GetNeighbor(HexDirection direction)
    {
        return neighbors[(int) direction];
    }

    public void SetNeighbor(HexDirection direction, HexCell cell)
    {
        neighbors[(int)direction] = cell;
        cell.neighbors[(int)direction.Opposite()] = this;
    }
}
