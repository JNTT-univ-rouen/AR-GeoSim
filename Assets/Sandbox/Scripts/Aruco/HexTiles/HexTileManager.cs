using System.Collections.Generic;
using UnityEngine;

namespace ARSandbox.Aruco.HexTiles
{
    /// <summary>
    /// Change le type de terrain des cellules de l'HexGrid a la demande des
    /// marqueurs "tuile" (vegetalise, sec, goudronne).
    ///
    /// Regles tenues ici :
    ///  - une carte doit rester STABLE dans la meme cellule pendant
    ///    `placementValidationDelay` avant de la modifier : une carte glissee
    ///    sur le sable ne repeint pas toutes les cellules traversees ;
    ///  - le changement est persistant : retirer la carte laisse la tuile en
    ///    place, seule une autre carte la change ;
    ///  - plusieurs cartes, meme de meme identifiant, peuvent etre posees en
    ///    meme temps : le suivi se fait par cellule, pas par identifiant ;
    ///  - la grille est masquee au lancement : la premiere tuile posee
    ///    l'affiche (cf. `showGridOnPaint`), sinon l'effet serait invisible.
    ///
    /// Une action ArUco est un asset : elle ne peut ni attendre ni tenir d'etat
    /// de scene. Le delai de validation vit donc ici, comme pour les nuages.
    /// </summary>
    public class HexTileManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private HexGrid hexGrid;

        [Header("Comportement")]
        [Tooltip("Duree pendant laquelle la carte doit rester dans la meme cellule avant que celle-ci change de terrain. Evite de repeindre les cellules traversees pendant la pose.")]
        [SerializeField] private float placementValidationDelay = 1f;

        [Tooltip("Duree sans revoir une carte avant d'oublier une pose non encore validee.")]
        [SerializeField] private float markerLostDelay = 1f;

        [Tooltip("Affiche la grille des qu'une tuile est posee par un marqueur. Decoche : la tuile change mais la grille reste masquee jusqu'a son affichage depuis Sandbox Settings.")]
        [SerializeField] private bool showGridOnPaint = true;

        [Tooltip("Journalise les changements de tuile dans la console.")]
        [SerializeField] private bool logEvents = true;

        /// <summary>Une cellule visee par une carte, en attente de validation.</summary>
        private class PendingTile
        {
            public HexTerrainType TerrainType;
            public float StableSince;
            public float LastSeenTime;
            public bool Applied;
        }

        private readonly Dictionary<HexCell, PendingTile> pendingByCell = new Dictionary<HexCell, PendingTile>();

        // Tampon reutilise a chaque passage, pour ne pas allouer par frame.
        private readonly List<HexCell> expiredBuffer = new List<HexCell>();

        /// <summary>
        /// Appele a chaque detection d'une carte tuile, apparition comme
        /// maintien. La cellule ne change qu'une fois la carte restee assez
        /// longtemps dessus.
        /// </summary>
        public void OnTileMarkerSeen(Vector3 worldPosition, HexTerrainType terrainType)
        {
            if (hexGrid == null || !hexGrid.IsReady) return;

            HexCell cell = hexGrid.GetCellAtPosition(worldPosition);
            if (cell == null) return;

            if (!pendingByCell.TryGetValue(cell, out PendingTile pending) || pending.TerrainType != terrainType)
            {
                // Nouvelle carte sur cette cellule, ou carte d'un autre type
                // posee a la place : le decompte repart.
                pendingByCell[cell] = new PendingTile
                {
                    TerrainType = terrainType,
                    StableSince = Time.time,
                    LastSeenTime = Time.time
                };
                return;
            }

            pending.LastSeenTime = Time.time;

            if (pending.Applied) return;
            if (Time.time - pending.StableSince < placementValidationDelay) return;

            pending.Applied = true;
            Apply(cell, terrainType);
        }

        private void Apply(HexCell cell, HexTerrainType terrainType)
        {
            bool changed = hexGrid.SetCellTerrainType(cell, terrainType);

            if (showGridOnPaint && !hexGrid.Visible)
            {
                hexGrid.Visible = true;
                if (logEvents) Debug.Log("HexTileManager : grille affichee par la premiere tuile posee.", this);
            }

            if (changed && logEvents)
            {
                Debug.Log($"HexTileManager : cellule {cell.coordinates} -> {terrainType}.", this);
            }
        }

        void Update()
        {
            expiredBuffer.Clear();

            foreach (KeyValuePair<HexCell, PendingTile> entry in pendingByCell)
            {
                if (Time.time - entry.Value.LastSeenTime > markerLostDelay) expiredBuffer.Add(entry.Key);
            }

            // La tuile deja posee reste en place : on oublie seulement le suivi.
            foreach (HexCell cell in expiredBuffer)
            {
                pendingByCell.Remove(cell);
            }
        }
    }
}
