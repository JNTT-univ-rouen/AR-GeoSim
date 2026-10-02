using UnityEngine;

namespace ARSandbox.Aruco
{
    /// <summary>
    /// Les objets de scene que les actions ont le droit de piloter.
    ///
    /// Une action est un asset (ScriptableObject) : elle ne peut pas contenir
    /// de reference vers la scene, celle-ci changeant a chaque chargement. Le
    /// routeur transmet donc ce contexte a chaque appel, et l'action y pioche
    /// ce dont elle a besoin.
    ///
    /// Pour donner acces a un nouveau systeme (HexGrid, labels, simulation de
    /// vent...), ajouter un champ ici et le renseigner dans l'inspecteur : les
    /// actions existantes ne sont pas touchees.
    ///
    /// Les champs peuvent etre vides : une action verifie toujours ce qu'elle
    /// utilise (cf. RequireWaterSimulation).
    /// </summary>
    public class ArucoSceneContext : MonoBehaviour
    {
        [SerializeField] private Sandbox sandbox;
        [SerializeField] private WaterSimulation.WaterSimulation waterSimulation;

        [Tooltip("Gestionnaire des labels d'altitude (etiquettes chiffrees sur les courbes de niveau).")]
        [SerializeField] private TopographyLabelManager topographyLabelManager;

        [Tooltip("Menu des saisons : porte les 4 shaders de terrain (normal, sec, vert, noir et blanc) et sait les appliquer avec la texture de sol qui va avec.")]
        [SerializeField] private UI_DropdownSeasonMenu seasonMenu;

        [Tooltip("Gestionnaire des nuages : cree et fait vivre les nuages demandes par les marqueurs de precipitations.")]
        [SerializeField] private Clouds.CloudManager cloudManager;

        public Sandbox Sandbox => sandbox;
        public Clouds.CloudManager CloudManager => cloudManager;
        public WaterSimulation.WaterSimulation WaterSimulation => waterSimulation;
        public TopographyLabelManager TopographyLabelManager => topographyLabelManager;
        public UI_DropdownSeasonMenu SeasonMenu => seasonMenu;

        /// <summary>
        /// Renvoie la simulation d'eau, ou null en signalant ce qui manque : une
        /// reference oubliee dans l'inspecteur est un defaut de cablage, pas un
        /// cas normal, autant le dire clairement dans la console.
        /// </summary>
        public WaterSimulation.WaterSimulation RequireWaterSimulation(Object asker)
        {
            if (waterSimulation == null)
            {
                Debug.LogWarning($"ArucoSceneContext : aucune WaterSimulation assignee, '{asker}' ne peut rien faire.", this);
            }
            return waterSimulation;
        }

        /// <summary>Meme principe, pour les nuages.</summary>
        public Clouds.CloudManager RequireCloudManager(Object asker)
        {
            if (cloudManager == null)
            {
                Debug.LogWarning($"ArucoSceneContext : aucun CloudManager assigne, '{asker}' ne peut pas creer de nuage.", this);
            }
            return cloudManager;
        }

        /// <summary>Meme principe que RequireWaterSimulation, pour les labels d'altitude.</summary>
        public TopographyLabelManager RequireTopographyLabelManager(Object asker)
        {
            if (topographyLabelManager == null)
            {
                Debug.LogWarning($"ArucoSceneContext : aucun TopographyLabelManager assigne, '{asker}' ne peut pas toucher aux labels.", this);
            }
            return topographyLabelManager;
        }

        /// <summary>Meme principe, pour la vue du terrain (shaders de saison).</summary>
        public UI_DropdownSeasonMenu RequireSeasonMenu(Object asker)
        {
            if (seasonMenu == null)
            {
                Debug.LogWarning($"ArucoSceneContext : aucun UI_DropdownSeasonMenu assigne, '{asker}' ne peut pas changer la vue du terrain.", this);
            }
            return seasonMenu;
        }
    }
}
