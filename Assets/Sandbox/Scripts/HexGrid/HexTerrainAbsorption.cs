using System;
using UnityEngine;

/// <summary>
/// Proprietes physiques du terrain, cellule par cellule. Pour l'instant une
/// seule : combien de gouttes d'eau une cellule peut avaler par seconde.
///
/// Le modele est volontairement un DEBIT FIXE, et non une capacite qui se
/// remplit : un debit suffit a produire le phenomene qu'on veut montrer. Le
/// goudron a zero, l'eau reste en surface et ruisselle ; le vegetalise avale
/// vite, elle disparait. La saturation (une pelouse qui finit gorgee, un sol
/// asseche d'abord hydrophobe) viendra comme second champ de ce tableau, sans
/// rien reprendre de ce qui est ici.
///
/// Ce composant ne tient que la donnee et sa lecture. L'absorption elle-meme
/// est appliquee par WaterSimulation, qui est proprietaire des gouttes : c'est
/// la qu'on sait lesquelles existent et ou elles sont.
/// </summary>
public class HexTerrainAbsorption : MonoBehaviour
{
    [Serializable]
    public struct TerrainAbsorption
    {
        [Tooltip("Terrain concerne. Seuls Grass, Stone et Tarmac sont en service (cf. HexCell.TypesInUse).")]
        public HexTerrainType terrain;

        [Tooltip("Gouttes avalees par seconde et par cellule, quand le curseur d'absorption est au maximum. 0 = impermeable.")]
        [Range(0f, 30f)]
        public float dropsPerSecond;
    }

    [Tooltip("Debit d'infiltration de chaque terrain. Un terrain absent de la liste prend la valeur par defaut ci-dessous.")]
    [SerializeField]
    private TerrainAbsorption[] terrains =
    {
        // Vegetalise : le sol vivant boit vite.
        new TerrainAbsorption { terrain = HexTerrainType.Grass,  dropsPerSecond = 10f },
        // Asseche : croute dure, l'eau penetre mal.
        new TerrainAbsorption { terrain = HexTerrainType.Stone,  dropsPerSecond = 1f },
        // Artificialise : rien ne passe.
        new TerrainAbsorption { terrain = HexTerrainType.Tarmac, dropsPerSecond = 0f },
    };

    [Tooltip("Debit applique a un terrain absent de la liste, et a l'eau qui tombe hors de la grille.")]
    [Range(0f, 30f)]
    [SerializeField] private float defaultDropsPerSecond = 10f;

    // Debit impose a TOUTES les cellules, quel que soit leur terrain.
    //
    // Les marqueurs de qualite du sol (11 Sec, 12 Artificialise, 13 Poreux)
    // decrivent le bac entier, pas une cellule : ils servent justement quand on
    // n'utilise pas la grille. Tant qu'une de ces cartes impose son reglage, le
    // tableau par terrain est mis de cote - sans quoi les deux systemes se
    // contrediraient, une cellule de goudron restant impermeable au milieu d'un
    // bac declare poreux.
    //
    // Ce debit impose ne dure qu'un episode d'absorption : des que l'eau
    // presente sous ce reglage a ete entierement bue, chaque terrain retrouve
    // son debit du tableau (cf. EndOverrideIfDry). Poser une carte tuile le
    // leve aussi.
    //
    // Etat d'execution, non serialise : rien a enregistrer dans la scene.
    private bool hasGlobalOverride;
    private float globalOverrideDropsPerSecond;

    // Vrai des que de l'eau a ete vue sur le bac depuis que le debit est
    // impose. Sans ca, une carte posee sur un bac sec - le cas courant, on pose
    // la carte PUIS on fait pleuvoir - serait levee avant meme d'avoir servi.
    private bool overrideHasSeenWater;

    /// <summary>Vrai quand une carte de qualite du sol impose son debit.</summary>
    public bool HasGlobalOverride => hasGlobalOverride;

    /// <summary>
    /// Impose un debit a tout le bac. Appele par les marqueurs de qualite du
    /// sol, qui raisonnent globalement.
    /// </summary>
    public void SetGlobalOverride(float dropsPerSecond)
    {
        hasGlobalOverride = true;
        globalOverrideDropsPerSecond = Mathf.Max(0f, dropsPerSecond);
        overrideHasSeenWater = false;
    }

    /// <summary>Rend la main au tableau par terrain.</summary>
    public void ClearGlobalOverride()
    {
        hasGlobalOverride = false;
        overrideHasSeenWater = false;
    }

    /// <summary>
    /// A appeler a chaque pas de la simulation d'eau, avec le nombre de gouttes
    /// sur le bac. Leve le debit impose une fois que l'eau tombee sous ce
    /// reglage a disparu : chaque terrain retrouve alors son debit du tableau.
    /// Renvoie vrai au pas ou le debit est leve.
    ///
    /// Suivi a part de la coroutine d'absorption : celle-ci peut ne jamais
    /// tourner (case decochee), s'arreter avant la fin (changement de mode),
    /// ou etre lancee plusieurs fois. Seul le niveau d'eau fait foi.
    /// </summary>
    public bool EndOverrideIfDry(int dropletCount)
    {
        if (!hasGlobalOverride) return false;

        if (dropletCount > 0)
        {
            overrideHasSeenWater = true;
            return false;
        }

        if (!overrideHasSeenWater) return false;

        ClearGlobalOverride();
        return true;
    }

    /// <summary>
    /// Debit d'infiltration de cette cellule. Une cellule nulle (eau hors
    /// grille) ou jamais peinte prend le debit par defaut : au lancement tout
    /// le bac est vegetalise, c'est donc la valeur qui ne surprend pas.
    /// </summary>
    public float DropsPerSecondFor(HexCell cell)
    {
        if (hasGlobalOverride) return globalOverrideDropsPerSecond;
        if (cell == null || !cell.Painted) return defaultDropsPerSecond;
        return DropsPerSecondFor(cell.terrainType);
    }

    public float DropsPerSecondFor(HexTerrainType terrain)
    {
        if (hasGlobalOverride) return globalOverrideDropsPerSecond;

        if (terrains != null)
        {
            for (int i = 0; i < terrains.Length; i++)
            {
                if (terrains[i].terrain == terrain) return Mathf.Max(0f, terrains[i].dropsPerSecond);
            }
        }
        return Mathf.Max(0f, defaultDropsPerSecond);
    }
}
