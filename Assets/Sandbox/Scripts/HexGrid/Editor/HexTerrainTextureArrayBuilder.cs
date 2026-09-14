using UnityEditor;
using UnityEngine;

/// Construit le Texture2DArray utilisé par HexTerrain.shader (technique
/// splat map, cf. tutoriel Catlike Coding "Hex Map" part 14) à partir de
/// textures individuelles, et l'assigne à HexTerrainMat. Un Texture2DArray
/// ne peut pas être importé directement dans Unity : il doit être
/// construit par script, d'où ce petit outil à lancer une fois (et à
/// relancer si on change les textures sources).
///
/// L'ordre des textures ci-dessous DOIT correspondre à l'ordre des valeurs
/// de l'enum HexTerrainType (Sand=0, Grass=1, Mud=2, Stone=3, Snow=4,
/// Tarmac=5), puisque HexMesh écrit directement (int)cell.terrainType
/// comme index de tranche. sand/grass/mud/stone/snow : fournies par
/// l'utilisateur, style non photoréaliste dans l'esprit du tutoriel
/// Catlike Coding "Hex Map" part 14. tarmac : générée procéduralement
/// (aucun équivalent "goudron" dans le tutoriel), même esthétique.
public static class HexTerrainTextureArrayBuilder
{
    static readonly string[] SourceTexturePaths =
    {
        "Assets/Sandbox/Textures/textures_clc/sand.png",   // 0 = Sand
        "Assets/Sandbox/Textures/textures_clc/grass.png",  // 1 = Grass
        "Assets/Sandbox/Textures/textures_clc/mud.png",    // 2 = Mud
        "Assets/Sandbox/Textures/textures_clc/stone.png",  // 3 = Stone
        "Assets/Sandbox/Textures/textures_clc/snow.png",   // 4 = Snow
        "Assets/Sandbox/Textures/textures_clc/tarmac.png", // 5 = Tarmac
    };

    const string OutputArrayPath = "Assets/Sandbox/Textures/HexTerrainArray.asset";
    const string MaterialPath = "Assets/Sandbox/Shaders/HexTerrainMat.mat";

    [MenuItem("Tools/Sandbox/Build Hex Terrain Texture Array")]
    public static void Build()
    {
        var sources = new Texture2D[SourceTexturePaths.Length];
        var reimported = false;

        for (int i = 0; i < SourceTexturePaths.Length; i++)
        {
            string path = SourceTexturePaths[i];
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError($"HexTerrainTextureArrayBuilder: texture introuvable a '{path}'.");
                return;
            }

            if (!importer.isReadable)
            {
                importer.isReadable = true;
                importer.SaveAndReimport();
                reimported = true;
            }

            sources[i] = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        if (reimported)
        {
            Debug.Log("HexTerrainTextureArrayBuilder: certaines textures ont ete rendues lisibles (Read/Write Enabled) pour construire le tableau.");
        }

        int width = sources[0].width;
        int height = sources[0].height;
        for (int i = 1; i < sources.Length; i++)
        {
            if (sources[i].width != width || sources[i].height != height)
            {
                Debug.LogError(
                    $"HexTerrainTextureArrayBuilder: '{SourceTexturePaths[i]}' ({sources[i].width}x{sources[i].height}) " +
                    $"n'a pas la meme taille que '{SourceTexturePaths[0]}' ({width}x{height}). " +
                    "Toutes les tranches d'un Texture2DArray doivent avoir la meme taille.");
                return;
            }
        }

        var array = new Texture2DArray(width, height, sources.Length, TextureFormat.RGBA32, true, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Repeat,
        };

        for (int i = 0; i < sources.Length; i++)
        {
            array.SetPixels32(sources[i].GetPixels32(), i);
        }
        array.Apply(true, false);

        var existingArray = AssetDatabase.LoadAssetAtPath<Texture2DArray>(OutputArrayPath);
        if (existingArray != null)
        {
            EditorUtility.CopySerialized(array, existingArray);
            AssetDatabase.SaveAssets();
        }
        else
        {
            AssetDatabase.CreateAsset(array, OutputArrayPath);
            AssetDatabase.SaveAssets();
            existingArray = AssetDatabase.LoadAssetAtPath<Texture2DArray>(OutputArrayPath);
        }

        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            Debug.LogError($"HexTerrainTextureArrayBuilder: materiau introuvable a '{MaterialPath}'. Tableau cree/mis a jour, mais non assigne.");
            return;
        }

        material.SetTexture("_TerrainTextures", existingArray);
        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets();

        Debug.Log($"HexTerrainTextureArrayBuilder: Texture2DArray genere a '{OutputArrayPath}' ({sources.Length} tranches, {width}x{height}) et assigne a '{MaterialPath}'.");
    }
}
