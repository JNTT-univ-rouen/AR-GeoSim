using System.Collections.Generic;
using UnityEngine;

namespace ARSandbox.Aruco
{
    /// <summary>
    /// La table ID -> action : un seul asset, qui liste les actions connues.
    /// C'est le pendant Unity du suivi des marqueurs tenu dans Notion.
    ///
    /// Creation : clic droit dans le Project > Create > Sandbox > ArUco >
    /// Catalogue d'actions. Ajouter un marqueur = deposer son asset d'action
    /// dans la liste.
    ///
    /// Le catalogue ne connait pas les ID : chaque action porte le sien. Il n'y
    /// a donc jamais deux endroits a garder synchronises.
    /// </summary>
    [CreateAssetMenu(fileName = "ArucoActionCatalog", menuName = "Sandbox/ArUco/Catalogue d'actions")]
    public class ArucoActionCatalog : ScriptableObject
    {
        [Tooltip("Une action par marqueur. L'ID est porte par l'action elle-meme.")]
        [SerializeField] private List<ArucoAction> actions = new List<ArucoAction>();

        private Dictionary<int, ArucoAction> actionsById;

        public IReadOnlyList<ArucoAction> Actions => actions;

        /// <summary>
        /// Retrouve l'action associee a un identifiant de marqueur.
        /// Renvoie false pour un marqueur inconnu : un marqueur non declare est
        /// un cas normal (carte d'un autre jeu, ID pas encore attribue), pas une
        /// erreur.
        /// </summary>
        public bool TryGetAction(int markerId, out ArucoAction action)
        {
            if (actionsById == null) BuildIndex();
            return actionsById.TryGetValue(markerId, out action);
        }

        /// <summary>
        /// Reconstruit l'index ID -> action. Appelee automatiquement au premier
        /// acces ; a rappeler si la liste est modifiee par code en cours de jeu.
        /// </summary>
        public void BuildIndex()
        {
            actionsById = new Dictionary<int, ArucoAction>();

            foreach (ArucoAction action in actions)
            {
                if (action == null) continue;

                if (actionsById.ContainsKey(action.MarkerId))
                {
                    // Deux actions sur le meme ID : le marqueur physique est
                    // ambigu, on garde la premiere et on signale le doublon.
                    Debug.LogError($"ArucoActionCatalog ({name}) : l'ID {action.MarkerId} est utilise par " +
                                   $"'{actionsById[action.MarkerId].name}' et '{action.name}'. La seconde est ignoree.", this);
                    continue;
                }

                actionsById.Add(action.MarkerId, action);
            }
        }

        void OnValidate()
        {
            // Rejoue l'index a chaque edition dans l'inspecteur : les doublons
            // d'ID sont signales tout de suite, pas le jour de la demo.
            BuildIndex();
        }
    }
}
