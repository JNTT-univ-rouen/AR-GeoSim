using UnityEngine;

namespace ARSandbox.Aruco.HexTiles
{
    /// <summary>
    /// Peint les tuiles de l'HexGrid demandees par les cartes d'occupation du
    /// sol : poser une carte donne un terrain a la cellule qui la porte.
    ///
    /// Comme pour les nuages, la pose doit etre stable avant d'etre prise en
    /// compte, plusieurs cartes peuvent agir en meme temps, et l'effet est
    /// PERSISTANT : retirer la carte laisse la tuile en place, seule une autre
    /// carte sur la meme cellule la change.
    ///
    /// Le suivi des cartes est delegue a ArucoPlacementTracker, partage avec les
    /// autres effets poses sur le bac.
    /// </summary>
    public class HexTileManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private HexGrid hexGrid;

        [Header("Comportement")]
        [Tooltip("Duree pendant laquelle la carte doit rester sur la meme cellule avant de la peindre. Evite de repeindre les cellules traversees pendant la pose.")]
        [SerializeField] private float placementValidationDelay = 1f;

        [Tooltip("Duree sans revoir une carte avant de la considerer retiree.")]
        [SerializeField] private float markerLostDelay = 1f;

        [Tooltip("Reaffiche la grille des la premiere tuile posee. Sinon, une tuile peinte alors que la grille est cachee resterait invisible jusqu'a ce qu'on la rouvre depuis Sandbox Settings.")]
        [SerializeField] private bool showGridOnPaint = true;

        [Tooltip("Journalise les tuiles peintes dans la console.")]
        [SerializeField] private bool logEvents = true;

        private ArucoPlacementTracker tracker;

        void Awake()
        {
            tracker = new ArucoPlacementTracker(
                placementValidationDelay,
                markerLostDelay,
                HexMetrics.innerRadius * 2f);
        }

        /// <summary>
        /// A appeler a chaque detection d'une carte d'occupation du sol. La
        /// cellule n'est peinte qu'une fois la pose stable.
        /// </summary>
        public void OnTileMarkerSeen(int markerId, Vector3 worldPosition, HexTerrainType terrainType)
        {
            if (hexGrid == null || !hexGrid.IsReady) return;

            HexCell cell = hexGrid.GetCellAtPosition(worldPosition);
            tracker.Report(markerId, cell, worldPosition, out bool justValidated);

            if (!justValidated) return;

            if (cell == null)
            {
                if (logEvents) Debug.Log("HexTileManager : carte posee hors de la grille, aucune tuile peinte.", this);
                return;
            }

            if (!hexGrid.SetCellTerrainType(worldPosition, terrainType)) return;

            if (showGridOnPaint) hexGrid.Visible = true;

            if (logEvents) Debug.Log($"HexTileManager : tuile {terrainType} posee.", this);
        }

        /// <summary>Nombre de cartes de cet identifiant actuellement sur le bac.</summary>
        public int CountMarkersInSandbox(int markerId)
        {
            return tracker != null ? tracker.Count(markerId) : 0;
        }

        void Update()
        {
            // Les cartes retirees n'ont aucun effet ici, la tuile restant en
            // place. On vide quand meme le suivi, sans quoi reposer une carte
            // au meme endroit ne declencherait plus rien.
            tracker.CollectLost();
        }
    }
}
