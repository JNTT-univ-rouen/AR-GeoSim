using UnityEngine;

namespace ARSandbox.Aruco.Actions
{
    /// <summary>
    /// Action de test : se contente d'ecrire dans la console. Sert de modele
    /// pour les vraies actions, et permet de valider la chaine complete
    /// (serveur Python -> SandboxClient -> routeur -> action) avant d'ecrire
    /// le moindre effet visuel.
    /// </summary>
    [CreateAssetMenu(fileName = "LogArucoAction", menuName = "Sandbox/ArUco/Action de test (console)")]
    public class LogArucoAction : ArucoAction
    {
        public override void OnMarkerAppeared(ArucoMarkerReading reading)
        {
            Debug.Log($"{this} : apparition en {reading.PixelPosition} px" +
                      (reading.HasWorldPosition ? $" / monde {reading.WorldPosition}" : ""));
        }

        public override void OnMarkerDisappeared()
        {
            Debug.Log($"{this} : disparition");
        }
    }
}
