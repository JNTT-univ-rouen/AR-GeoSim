using UnityEngine;

/// <summary>
/// Carte de depart posee sur la grille au lancement : tout le bac en terrain
/// vegetalise.
///
/// Les 64 cellules sont bel et bien PEINTES, et non simplement laissees a leur
/// type par defaut : une cellule peinte est dessinee (cf.
/// HexMesh.drawOnlyPaintedCells) et porte les proprietes physiques de son
/// terrain (cf. HexTerrainAbsorption). L'etat de depart est donc visible et
/// agit sur l'eau, sans qu'aucune carte ArUco n'ait ete posee.
///
/// Les cartes ArUco repeignent ensuite les cellules par-dessus : goudron pour
/// l'artificialisation, pierre pour l'assechement, vert pour revenir.
///
/// La version precedente dessinait un centre-ville (coeur bati, avenues,
/// tissu urbain, campagne). Elle reste dans l'historique du depot si le besoin
/// revient : le point de depart utile est desormais un terrain uniforme, dont
/// on observe la degradation.
/// </summary>
public static class HexDefaultMap
{
    /// <summary>Terrain pose partout au lancement.</summary>
    public const HexTerrainType InitialTerrain = HexTerrainType.Grass;

    /// <summary>
    /// Applique la carte de depart. `width` et `height` sont les dimensions de
    /// la grille, les cellules etant rangees ligne par ligne - sans importance
    /// ici, le terrain etant uniforme, mais la signature reste celle attendue
    /// par HexGrid.
    /// </summary>
    public static void Apply(HexCell[] cells, int width, int height)
    {
        if (cells == null) return;

        foreach (HexCell cell in cells)
        {
            if (cell != null) cell.SetTerrainType(InitialTerrain);
        }
    }
}
