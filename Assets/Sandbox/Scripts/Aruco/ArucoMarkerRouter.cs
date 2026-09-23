using System.Collections.Generic;
using UnityEngine;

namespace ARSandbox.Aruco
{
    /// <summary>
    /// Aiguille les marqueurs detectes vers les actions du catalogue, et tient
    /// le cycle de vie de chacun : apparition, maintien, disparition.
    ///
    /// Le fournisseur des detections n'est pas cable ici : SandboxClient (ou un
    /// test, ou une future source) appelle simplement SubmitDetections() a
    /// chaque reponse du serveur Python. Ce decouplage permet de tester le
    /// routage sans Kinect ni serveur.
    ///
    /// Ce composant ne sait rien de ce que font les actions : ajouter une
    /// fonctionnalite ne demande pas d'y toucher.
    /// </summary>
    public class ArucoMarkerRouter : MonoBehaviour
    {
        [SerializeField] private ArucoActionCatalog catalog;

        [Tooltip("Delai avant de considerer qu'un marqueur a disparu. Evite qu'une main qui passe au-dessus du bac, ou une detection ratee sur une image, ne coupe l'action.")]
        [SerializeField] private float lostAfterSeconds = 1.0f;

        [Tooltip("Journalise apparitions et disparitions dans la console.")]
        [SerializeField] private bool logEvents;

        // Marqueurs actuellement consideres comme presents.
        private readonly Dictionary<int, ActiveMarker> activeMarkers = new Dictionary<int, ActiveMarker>();

        // Action exclusive en cours pour chaque famille (cf. ExclusiveInFamily).
        private readonly Dictionary<ArucoActionFamily, ArucoAction> exclusiveByFamily =
            new Dictionary<ArucoActionFamily, ArucoAction>();

        // Tampon reutilise a chaque passage, pour ne pas allouer par frame.
        private readonly List<int> lostBuffer = new List<int>();

        private class ActiveMarker
        {
            public ArucoAction Action;
            public float LastSeenTime;
        }

        /// <summary>
        /// Point d'entree unique : la liste complete des marqueurs vus sur la
        /// derniere image. Les marqueurs absents de la liste sont consideres
        /// comme partis une fois le delai de tolerance ecoule.
        /// </summary>
        public void SubmitDetections(IReadOnlyList<ArucoMarkerReading> readings)
        {
            if (catalog == null)
            {
                Debug.LogWarning("ArucoMarkerRouter : aucun catalogue assigne, detections ignorees.", this);
                return;
            }

            if (readings != null)
            {
                foreach (ArucoMarkerReading reading in readings)
                {
                    if (!catalog.TryGetAction(reading.Id, out ArucoAction action)) continue;

                    if (activeMarkers.TryGetValue(reading.Id, out ActiveMarker active))
                    {
                        active.LastSeenTime = Time.time;
                        active.Action.OnMarkerHeld(reading);
                    }
                    else
                    {
                        Activate(action, reading);
                    }
                }
            }

            ExpireMissingMarkers();
        }

        /// <summary>Force la disparition de tous les marqueurs (changement de mode, arret du client...).</summary>
        public void ClearAll()
        {
            foreach (ActiveMarker active in activeMarkers.Values)
            {
                active.Action.OnMarkerDisappeared();
            }
            activeMarkers.Clear();
            exclusiveByFamily.Clear();
        }

        private void Activate(ArucoAction action, ArucoMarkerReading reading)
        {
            if (action.ExclusiveInFamily)
            {
                // Une seule action exclusive par famille : la precedente cede sa place.
                if (exclusiveByFamily.TryGetValue(action.Family, out ArucoAction previous) && previous != action)
                {
                    previous.OnSupersededInFamily();
                    activeMarkers.Remove(previous.MarkerId);
                }
                exclusiveByFamily[action.Family] = action;
            }

            activeMarkers[action.MarkerId] = new ActiveMarker { Action = action, LastSeenTime = Time.time };
            action.OnMarkerAppeared(reading);

            if (logEvents) Debug.Log($"ArUco : apparition de {action}", this);
        }

        private void ExpireMissingMarkers()
        {
            lostBuffer.Clear();

            foreach (KeyValuePair<int, ActiveMarker> entry in activeMarkers)
            {
                if (Time.time - entry.Value.LastSeenTime > lostAfterSeconds) lostBuffer.Add(entry.Key);
            }

            foreach (int markerId in lostBuffer)
            {
                ArucoAction action = activeMarkers[markerId].Action;
                activeMarkers.Remove(markerId);

                if (action.ExclusiveInFamily &&
                    exclusiveByFamily.TryGetValue(action.Family, out ArucoAction current) && current == action)
                {
                    exclusiveByFamily.Remove(action.Family);
                }

                action.OnMarkerDisappeared();

                if (logEvents) Debug.Log($"ArUco : disparition de {action}", this);
            }
        }

        void OnDisable()
        {
            ClearAll();
        }
    }
}
