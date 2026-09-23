using UnityEngine;

namespace ARSandbox.Aruco
{
    /// <summary>
    /// Une detection de marqueur, telle que renvoyee par le serveur Python
    /// (cf. SandboxClient : identifiant + centre du marqueur en pixels).
    ///
    /// Struct volontairement minimale : si le serveur renvoie un jour les
    /// coins ou l'orientation, ils s'ajoutent ici sans toucher au reste.
    /// </summary>
    public struct ArucoMarkerReading
    {
        /// <summary>Identifiant dans le dictionnaire DICT_4X4_50 (0 a 49).</summary>
        public int Id;

        /// <summary>Centre du marqueur, en pixels de l'image envoyee au serveur.</summary>
        public Vector2 PixelPosition;

        /// <summary>
        /// Centre du marqueur en coordonnees monde, quand la conversion a pu
        /// etre faite (meme repere que HandInputGesture.WorldPosition).
        /// Utile aux actions locales, qui agissent la ou l'objet est pose.
        /// </summary>
        public Vector3 WorldPosition;

        /// <summary>Vrai si WorldPosition a ete renseignee.</summary>
        public bool HasWorldPosition;

        public ArucoMarkerReading(int id, Vector2 pixelPosition)
        {
            Id = id;
            PixelPosition = pixelPosition;
            WorldPosition = Vector3.zero;
            HasWorldPosition = false;
        }

        public ArucoMarkerReading(int id, Vector2 pixelPosition, Vector3 worldPosition)
        {
            Id = id;
            PixelPosition = pixelPosition;
            WorldPosition = worldPosition;
            HasWorldPosition = true;
        }
    }
}
