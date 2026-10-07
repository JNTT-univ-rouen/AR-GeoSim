using ARSandbox.Aruco.HexTiles;
using UnityEngine;

namespace ARSandbox.Aruco.Actions
{
    /// <summary>
    /// Famille "Occupation du sol" : change le terrain de la cellule de
    /// l'HexGrid sur laquelle la carte est posee. Trois marqueurs partagent
    /// cette classe, seul le type de terrain change :
    ///   vegetalise (Grass, tuile verte), sec (Stone, tuile orange),
    ///   goudronne (Tarmac, tuile grise).
    ///
    /// L'action ne fait que transmettre l'intention : le delai de validation de
    /// la pose et la modification de la grille sont tenus par le
    /// HexTileManager, un composant de scene.
    ///
    /// Effet persistant : retirer la carte ne restaure rien.
    /// </summary>
    [CreateAssetMenu(fileName = "HexTerrainArucoAction", menuName = "Sandbox/ArUco/Tuile de terrain")]
    public class HexTerrainArucoAction : ArucoAction
    {
        [Header("Tuile")]
        [Tooltip("Terrain donne a la cellule sous la carte. Couleurs : Grass = vert, Stone = orange, Sand = jaune sable, Tarmac = gris goudron.")]
        [SerializeField] private HexTerrainType terrainType = HexTerrainType.Grass;

        public override void OnMarkerAppeared(ArucoMarkerReading reading, ArucoSceneContext context)
        {
            ReportSeen(reading, context, warnIfNoPosition: true);
        }

        /// <summary>
        /// Chaque detection compte : c'est en restant dans la meme cellule que
        /// la carte valide sa pose (cf. HexTileManager.placementValidationDelay).
        /// </summary>
        public override void OnMarkerHeld(ArucoMarkerReading reading, ArucoSceneContext context)
        {
            ReportSeen(reading, context, warnIfNoPosition: false);
        }

        // Rien a faire au retrait : la tuile reste en place.
        public override void OnMarkerDisappeared(ArucoSceneContext context) { }

        private void ReportSeen(ArucoMarkerReading reading, ArucoSceneContext context, bool warnIfNoPosition)
        {
            if (context == null) return;

            HexTileManager tiles = context.RequireHexTileManager(this);
            if (tiles == null) return;

            if (!reading.HasWorldPosition)
            {
                // Sans position monde, impossible de savoir quelle cellule est
                // visee : la carte est hors de la zone calibree.
                if (warnIfNoPosition) Debug.LogWarning($"{this} : carte sans position monde, tuile non posee.");
                return;
            }

            tiles.OnTileMarkerSeen(reading.WorldPosition, terrainType);
        }
    }
}
