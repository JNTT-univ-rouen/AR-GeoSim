using UnityEngine;

namespace ARSandbox.Aruco.Actions
{
    /// <summary>
    /// Enchaine plusieurs actions sur un meme marqueur : "sol sec" change la vue
    /// du terrain ET regle l'infiltration, sans qu'aucune des deux classes n'ait
    /// a connaitre l'autre.
    ///
    /// Les actions filles sont des assets ordinaires. Leur propre identifiant de
    /// marqueur est ignore : c'est celui porte par cette action composite qui
    /// compte, puisque c'est elle qui figure au catalogue.
    /// </summary>
    [CreateAssetMenu(fileName = "CompositeArucoAction", menuName = "Sandbox/ArUco/Action composee")]
    public class CompositeArucoAction : ArucoAction
    {
        [Tooltip("Actions declenchees ensemble, dans l'ordre de la liste.")]
        [SerializeField] private ArucoAction[] actions = new ArucoAction[0];

        public override void OnMarkerAppeared(ArucoMarkerReading reading, ArucoSceneContext context)
        {
            foreach (ArucoAction action in actions)
            {
                if (action != null) action.OnMarkerAppeared(reading, context);
            }
        }

        public override void OnMarkerHeld(ArucoMarkerReading reading, ArucoSceneContext context)
        {
            foreach (ArucoAction action in actions)
            {
                if (action != null) action.OnMarkerHeld(reading, context);
            }
        }

        public override void OnMarkerDisappearedAt(ArucoMarkerReading lastReading, ArucoSceneContext context)
        {
            foreach (ArucoAction action in actions)
            {
                if (action != null) action.OnMarkerDisappearedAt(lastReading, context);
            }
        }

        public override void OnSupersededInFamily(ArucoSceneContext context)
        {
            foreach (ArucoAction action in actions)
            {
                if (action != null) action.OnSupersededInFamily(context);
            }
        }
    }
}
