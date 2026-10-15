using UnityEngine;

// Ordre = index de tranche dans le Texture2DArray _TerrainTextures
// (cf. HexTerrainTextureArrayBuilder.cs). Sand/Grass/Mud/Stone/Snow :
// memes noms que les textures du tutoriel Catlike Coding "Hex Map".
// Tarmac (goudron) : aucune texture "goudron" fournie par le tutoriel,
// donc generee proceduralement (bruit fractal, gris fonce, meme faible
// contraste que les 5 autres) dans le meme dossier, textures_clc/tarmac.png.
//
// Seuls Grass, Stone et Tarmac sont en service (cf. HexCell.TypesInUse) :
// l'enum garde les trois autres pour ne pas decaler les index de tranche.
public enum HexTerrainType
{
    Sand,
    Grass,
    Mud,
    Stone,
    Snow,
    Tarmac
}

public class HexCell : MonoBehaviour
{

    public HexCoordinates coordinates;
    public HexTerrainType terrainType = HexTerrainType.Grass;

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

    // Vrai une fois que la cellule a recu un terrain, par une carte ArUco ou
    // un clic. Les cellules jamais peintes ne sont pas dessinees du tout :
    // sinon, poser une seule tuile ferait apparaitre tout le bac dans le
    // terrain par defaut, et l'effet local serait illisible.
    public bool Painted { get; private set; }

    /// <summary>Applique un terrain et marque la cellule comme peinte.</summary>
    public void SetTerrainType(HexTerrainType type)
    {
        terrainType = type;
        Painted = true;
    }

    /// <summary>Rend la cellule a son etat non peint : elle cesse d'etre dessinee.</summary>
    public void ClearTerrain()
    {
        Painted = false;
    }

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

    // Terrains reellement utilises : ceux que les trois cartes ArUco savent
    // poser (7 Artificialisation -> Tarmac, 8 Vegetalisation -> Grass,
    // 9 Assechement -> Stone), et ceux de la carte de depart.
    //
    // Sand, Mud et Snow restent dans l'enum sans etre proposes : l'ordre de
    // celui-ci est l'index de tranche dans le Texture2DArray, donc les
    // retirer decalerait les textures des trois autres. Pour en remettre un
    // en service, il suffit de l'ajouter ici.
    static readonly HexTerrainType[] TypesInUse =
    {
        HexTerrainType.Tarmac,
        HexTerrainType.Grass,
        HexTerrainType.Stone,
    };

    public void ToggleTerrainType()
    {
        // Une cellule posee sur un terrain hors service repart sur le premier
        // de la liste : IndexOf renvoie -1, donc l'index suivant est 0.
        int current = System.Array.IndexOf(TypesInUse, terrainType);
        int next = (current + 1) % TypesInUse.Length;
        SetTerrainType(TypesInUse[next]);
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
