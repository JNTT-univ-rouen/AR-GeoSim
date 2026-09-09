using System.Collections.Generic;
using UnityEngine;
using ARSandbox;

/// Bascule le type de terrain (verdoyant / sec) de l'hexagone du HexGrid
/// touché par un geste. Remplace l'ancien HexMapEditor (peinture de couleur
/// arbitraire, reliquat du tutoriel Catlike Coding) : ici on choisit
/// directement parmi les shaders de terrain déjà en place dans le Sandbox.
///
/// Plutôt que de refaire un raycast manuel (peu fiable : mauvaise caméra,
/// mauvais espace écran vs viewport de la RawImage qui affiche le Sandbox,
/// cf. UI_SandboxHandInput), on réutilise directement le pipeline de
/// détection tactile/main déjà utilisé par WaterSimulation/WindSimulation/
/// FireSimulation : HandInput calcule déjà une position monde fiable par
/// geste (souris OU main Kinect), on n'a plus qu'à la consommer.
public class HexTerrainPainter : MonoBehaviour
{
    public HexGrid hexGrid;
    public HandInput handInput;

    // Gestes déjà vus au tick précédent, pour ne déclencher qu'au tout
    // début de chaque geste (équivalent d'un "mouse down"), pas à chaque
    // frame tant qu'il dure.
    HashSet<int> knownGestureIDs = new HashSet<int>();

    void OnEnable()
    {
        HandInput.OnGesturesReady += HandleGesturesReady;
    }

    void OnDisable()
    {
        HandInput.OnGesturesReady -= HandleGesturesReady;
    }

    void HandleGesturesReady()
    {
        if (hexGrid == null || handInput == null) return;

        List<HandInputGesture> gestures = handInput.GetCurrentGestures();
        HashSet<int> currentIDs = new HashSet<int>();

        foreach (HandInputGesture gesture in gestures)
        {
            currentIDs.Add(gesture.GestureID);

            if (!knownGestureIDs.Contains(gesture.GestureID) && !gesture.OutOfBounds)
            {
                hexGrid.ToggleCellTerrainType(gesture.WorldPosition);
            }
        }

        knownGestureIDs = currentIDs;
    }
}
