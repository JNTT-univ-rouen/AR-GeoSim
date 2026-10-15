using UnityEngine;

/// <summary>
/// Carte de depart posee sur la grille au lancement : un centre-ville et sa
/// campagne, avec trois terrains seulement.
///
///   Tarmac : le coeur bati et deux avenues qui en partent ;
///   Stone  : le tissu urbain autour, mineral et sec ;
///   Grass  : la campagne, plus un parc garde dans la ville.
///
/// Les rayons sont proportionnels a la grille, pas ecrits en dur : la carte
/// tient aussi bien sur une grille 6x6 que 12x10. Sur une grille 8x8, celle de
/// la scene, la repartition tourne autour d'un quart de goudron, un tiers de
/// sol sec et 40% de campagne.
///
/// Les cartes ArUco repeignent ensuite les cellules par-dessus.
/// </summary>
public static class HexDefaultMap
{
    // Fractions du rayon de la grille. Reglees pour qu'il reste de la campagne
    // visible sur une petite grille : au-dela, la ville mange tout le bac.
    const float CoreRadiusFactor = 0.3f;
    const float CityRadiusFactor = 0.7f;

    /// <summary>
    /// Applique la carte de depart. `width` et `height` sont les dimensions de
    /// la grille, les cellules etant rangees ligne par ligne.
    /// </summary>
    public static void Apply(HexCell[] cells, int width, int height)
    {
        if (cells == null || cells.Length == 0) return;

        int centreColumn = width / 2;
        int centreRow = height / 2;
        HexCoordinates centre = HexCoordinates.FromOffsetCoordiantes(centreColumn, centreRow);

        float gridRadius = Mathf.Min(width, height) * 0.5f;
        int coreRadius = Mathf.Max(1, Mathf.RoundToInt(gridRadius * CoreRadiusFactor));
        int cityRadius = Mathf.Max(coreRadius + 1, Mathf.RoundToInt(gridRadius * CityRadiusFactor));

        // Un ilot vert dans le tissu urbain, decale du centre pour ne pas
        // couper les avenues.
        HexCoordinates park = HexCoordinates.FromOffsetCoordiantes(
            centreColumn + coreRadius + 1,
            centreRow + coreRadius);

        for (int row = 0, i = 0; row < height; row++)
        {
            for (int column = 0; column < width && i < cells.Length; column++, i++)
            {
                HexCoordinates coordinates = HexCoordinates.FromOffsetCoordiantes(column, row);
                cells[i].SetTerrainType(
                    TerrainFor(coordinates, centre, park, coreRadius, cityRadius));
            }
        }
    }

    private static HexTerrainType TerrainFor(HexCoordinates cell, HexCoordinates centre,
                                             HexCoordinates park, int coreRadius, int cityRadius)
    {
        // Le parc d'abord : il doit rester vert malgre la ville autour.
        if (Distance(cell, park) == 0) return HexTerrainType.Grass;

        int distance = Distance(cell, centre);

        // Coeur bati.
        if (distance <= coreRadius) return HexTerrainType.Tarmac;

        // Avenues : deux axes de la grille hexagonale passant par le centre.
        // Elles s'arretent juste apres la ville, faute de quoi des hexagones de
        // goudron isoles apparaissent jusque dans les coins du bac.
        if (IsOnAvenue(cell, centre) && distance <= cityRadius + 1) return HexTerrainType.Tarmac;

        // Tissu urbain mineral.
        if (distance <= cityRadius) return HexTerrainType.Stone;

        // Campagne.
        return HexTerrainType.Grass;
    }

    /// <summary>
    /// Deux avenues seulement, et non les trois axes de la grille : avec trois,
    /// le centre devient une etoile illisible sur une petite grille.
    /// </summary>
    private static bool IsOnAvenue(HexCoordinates cell, HexCoordinates centre)
    {
        return cell.Z == centre.Z || cell.X == centre.X;
    }

    /// <summary>
    /// Distance en nombre de cellules, en coordonnees cube : la moitie de la
    /// somme des ecarts sur les trois axes.
    /// </summary>
    private static int Distance(HexCoordinates a, HexCoordinates b)
    {
        return (Mathf.Abs(a.X - b.X) + Mathf.Abs(a.Y - b.Y) + Mathf.Abs(a.Z - b.Z)) / 2;
    }
}
