using UnityEngine;

namespace ARSandbox.Aruco.Actions
{
    /// <summary>
    /// Famille "Lecture relief" : regle comment le relief est montre, sans
    /// toucher a la simulation.
    ///
    /// Deux reglages independants, chacun pouvant rester sur "ne change rien" :
    /// la vue du terrain (shader) et les labels d'altitude. Un asset n'active
    /// donc que ce qui le concerne - les cinq marqueurs de la famille partagent
    /// cette seule classe.
    ///
    /// Comportement voulu pour cette famille : le changement est PERSISTANT.
    /// Poser la carte change la vue, la retirer ne la defait pas - comme si on
    /// avait clique dans le menu. Rien n'est donc memorise ni restaure, a la
    /// difference de la qualite du sol.
    ///
    /// Et le changement n'est applique QUE s'il modifie quelque chose : reposer
    /// la meme carte, ou poser celle de la vue deja active, ne declenche aucun
    /// rechargement de shader.
    /// </summary>
    [CreateAssetMenu(fileName = "ReliefViewArucoAction", menuName = "Sandbox/ArUco/Lecture relief")]
    public class ReliefViewArucoAction : ArucoAction
    {
        /// <summary>
        /// Vues disponibles. Les valeurs 0 a 3 correspondent aux saisons du menu
        /// deroulant : normal, sec/orange, vert, noir et blanc.
        /// </summary>
        public enum TerrainView
        {
            NeChangeRien = -1,
            Normal = 0,
            SecOrange = 1,
            Vert = 2,
            NoirEtBlanc = 3,

            /// <summary>
            /// Shader fourni par l'asset lui-meme (champ Custom Terrain Shader),
            /// pour les vues qui n'existent pas dans le menu des saisons.
            /// </summary>
            Personnalise = 100
        }

        public enum LabelSetting
        {
            NeChangeRien,
            Activer,
            Desactiver
        }

        [Header("Vue du relief")]
        [SerializeField] private TerrainView terrainView = TerrainView.NeChangeRien;

        [Tooltip("Utilise seulement si Terrain View = Personnalise. Un asset peut referencer un shader, c'est un asset lui aussi.")]
        [SerializeField] private Shader customTerrainShader;

        [Tooltip("Labels d'altitude : les nombres portes par les courbes de niveau.")]
        [SerializeField] private LabelSetting labels = LabelSetting.NeChangeRien;

        public override void OnMarkerAppeared(ArucoMarkerReading reading, ArucoSceneContext context)
        {
            if (context == null) return;

            bool changedView = ApplyTerrainView(context);
            bool changedLabels = ApplyLabels(context);

            if (changedView || changedLabels)
            {
                Debug.Log($"{this} : vue {terrainView}, labels {labels}");
            }
        }

        // Rien a faire quand la carte est retiree : la vue choisie reste en place.
        public override void OnMarkerDisappeared(ArucoSceneContext context) { }

        /// <summary>
        /// Applique la vue si elle n'est pas deja active. Renvoie vrai si quelque
        /// chose a change.
        ///
        /// La comparaison se fait sur le shader reellement en place
        /// (Sandbox.CurrentShader) : pas d'etat tenu en parallele, donc pas de
        /// risque qu'il mente apres un changement fait depuis le menu.
        /// </summary>
        private bool ApplyTerrainView(ArucoSceneContext context)
        {
            if (terrainView == TerrainView.NeChangeRien) return false;

            if (context.Sandbox == null)
            {
                Debug.LogWarning($"{this} : aucun Sandbox dans le contexte, vue non appliquee.", this);
                return false;
            }

            if (terrainView == TerrainView.Personnalise)
            {
                if (customTerrainShader == null)
                {
                    Debug.LogWarning($"{this} : Terrain View = Personnalise mais aucun shader assigne.", this);
                    return false;
                }

                if (context.Sandbox.CurrentShader == customTerrainShader) return false;

                // La texture de sol est retiree, comme le fait le menu pour le
                // noir et blanc : elle n'a pas de sens sur un camaieu d'altitude.
                context.Sandbox.SetSandboxShader(customTerrainShader);
                context.Sandbox.ClearTerrainAlbedo();
                return true;
            }

            UI_DropdownSeasonMenu seasonMenu = context.RequireSeasonMenu(this);
            if (seasonMenu == null) return false;

            // Deja ce shader en place : rien a faire. On passe quand meme par le
            // menu quand il faut changer, car lui seul applique aussi la texture
            // de sol associee a la saison.
            if (context.Sandbox.CurrentShader == seasonMenu.GetSeasonShader((int)terrainView)) return false;

            seasonMenu.ChangeSeasonDropdown((int)terrainView);
            seasonMenu.ChangeSeasonImage((int)terrainView);
            return true;
        }

        /// <summary>
        /// Active ou coupe les labels si ce n'est pas deja l'etat en cours.
        /// Renvoie vrai si quelque chose a change.
        /// </summary>
        private bool ApplyLabels(ArucoSceneContext context)
        {
            if (labels == LabelSetting.NeChangeRien) return false;

            TopographyLabelManager labelManager = context.RequireTopographyLabelManager(this);
            if (labelManager == null) return false;

            bool wanted = labels == LabelSetting.Activer;
            if (labelManager.ContourLabelsEnabled == wanted) return false;

            labelManager.UI_ToggleContourLabels(wanted);
            return true;
        }
    }
}
