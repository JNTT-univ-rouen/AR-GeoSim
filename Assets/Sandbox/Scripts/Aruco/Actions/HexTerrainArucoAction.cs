using ARSandbox.Aruco.HexTiles;
using UnityEngine;

namespace ARSandbox.Aruco.Actions
{
    /// <summary>
    /// Famille "Occupation du sol" : poser la carte donne un terrain a la
    /// cellule de l'HexGrid qui la porte.
    ///
    /// Action locale : elle a besoin de la position monde de la carte, donc
    /// d'une detection dans la zone calibree. L'effet est persistant, la tuile
    /// restant en place une fois la carte retiree.
    ///
    /// Comme pour les nuages, tout ce qui demande du temps (pose stable, suivi
    /// de plusieurs cartes) est tenu par un composant de scene, ici
    /// HexTileManager : un asset ne peut ni attendre ni garder d'etat.
    /// </summary>
    [CreateAssetMenu(fileName = "HexTerrainArucoAction", menuName = "Sandbox/ArUco/Occupation du sol (tuile HexGrid)")]
    public class HexTerrainArucoAction : ArucoAction
    {
        [Header("Tuile posee")]
        [Tooltip("Terrain applique a la cellule. L'ordre de l'enumeration est l'index de texture dans le Texture2DArray (cf. HexTerrainTextureArrayBuilder).")]
        [SerializeField] private HexTerrainType terrainType = HexTerrainType.Grass;

        public override void OnMarkerAppeared(ArucoMarkerReading reading, ArucoSceneContext context)
        {
            ReportSeen(reading, context, warnIfNoPosition: true);
        }

        /// <summary>
        /// Chaque detection compte : c'est en restant sur la meme cellule que la
        /// carte valide sa pose.
        /// </summary>
        public override void OnMarkerHeld(ArucoMarkerReading reading, ArucoSceneContext context)
        {
            ReportSeen(reading, context, warnIfNoPosition: false);
        }

        // Rien au retrait : la tuile reste.
        public override void OnMarkerDisappeared(ArucoSceneContext context) { }

        private void ReportSeen(ArucoMarkerReading reading, ArucoSceneContext context, bool warnIfNoPosition)
        {
            if (context == null) return;

            HexTileManager tiles = context.RequireHexTileManager(this);
            if (tiles == null) return;

            if (!reading.HasWorldPosition)
            {
                if (warnIfNoPosition) Debug.LogWarning($"{this} : carte sans position monde, aucune tuile peinte.");
                return;
            }

            tiles.OnTileMarkerSeen(reading.Id, reading.WorldPosition, terrainType);
        }
    }
}
