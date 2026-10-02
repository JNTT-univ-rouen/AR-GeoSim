using ARSandbox.Aruco.Clouds;
using UnityEngine;

namespace ARSandbox.Aruco.Actions
{
    /// <summary>
    /// Famille "Precipitations" : poser la carte reserve une zone, la retirer
    /// declenche un nuage quelques secondes plus tard.
    ///
    /// L'action ne fait que transmettre l'intention : tout ce qui dure dans le
    /// temps (delai d'activation, pluie, reserve d'eau, disparition) est tenu
    /// par le CloudManager, un composant de scene. Un asset ne peut ni attendre
    /// ni garder d'etat de scene.
    ///
    /// Les reglages de pluie (debit, reserve, rayon) sont volontairement sur le
    /// CloudManager et non ici : ils se reglent en une fois pendant les tests,
    /// plutot que marqueur par marqueur.
    /// </summary>
    [CreateAssetMenu(fileName = "PrecipitationArucoAction", menuName = "Sandbox/ArUco/Precipitations")]
    public class PrecipitationArucoAction : ArucoAction
    {
        [Header("Type de nuage")]
        [Tooltip("Pluie : nuage clair, debit normal. Orage : nuage sombre avec eclair, debit multiplie (cf. CloudManager).")]
        [SerializeField] private SandboxCloud.CloudKind cloudKind = SandboxCloud.CloudKind.Pluie;

        public override void OnMarkerAppeared(ArucoMarkerReading reading, ArucoSceneContext context)
        {
            ReportSeen(reading, context, warnIfNoPosition: true);
        }

        /// <summary>
        /// Chaque detection compte : c'est en restant dans la meme cellule que
        /// la carte valide sa pose (cf. CloudManager.placementValidationDelay).
        /// </summary>
        public override void OnMarkerHeld(ArucoMarkerReading reading, ArucoSceneContext context)
        {
            ReportSeen(reading, context, warnIfNoPosition: false);
        }

        private void ReportSeen(ArucoMarkerReading reading, ArucoSceneContext context, bool warnIfNoPosition)
        {
            if (context == null) return;

            CloudManager clouds = context.RequireCloudManager(this);
            if (clouds == null) return;

            if (!reading.HasWorldPosition)
            {
                // Sans position monde, impossible de savoir quelle zone du bac
                // est visee : la carte est hors de la zone calibree.
                if (warnIfNoPosition) Debug.LogWarning($"{this} : carte sans position monde, nuage non reserve.");
                return;
            }

            clouds.OnPrecipitationMarkerSeen(reading.Id, reading.WorldPosition, cloudKind);
        }

        /// <summary>
        /// Le routeur transmet la derniere position connue du marqueur retire,
        /// car c'est elle qui designe la zone du nuage a activer.
        /// </summary>
        public override void OnMarkerDisappearedAt(ArucoMarkerReading lastReading, ArucoSceneContext context)
        {
            if (context == null) return;

            CloudManager clouds = context.CloudManager;
            if (clouds == null) return;

            clouds.OnPrecipitationMarkerRemoved(lastReading.Id);
        }
    }
}
