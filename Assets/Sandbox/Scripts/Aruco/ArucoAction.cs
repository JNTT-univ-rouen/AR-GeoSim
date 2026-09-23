using UnityEngine;

namespace ARSandbox.Aruco
{
    /// <summary>
    /// Ce que declenche UN marqueur ArUco. Chaque action est un asset
    /// (ScriptableObject) : on en cree un par marqueur, on le range dans le
    /// catalogue, et le routeur s'occupe du reste.
    ///
    /// Pour ajouter une fonctionnalite : creer une classe qui herite de
    /// celle-ci, avec son propre [CreateAssetMenu], et redefinir les methodes
    /// utiles. Aucun autre fichier n'est a modifier - ni le routeur, ni le
    /// catalogue, ni SandboxClient.
    ///
    /// Les trois methodes suivent la vie du marqueur devant la camera :
    ///   Appeared -> Held (a chaque detection tant qu'il reste visible) -> Disappeared
    /// Elles sont vides par defaut : une action n'implemente que ce qui la
    /// concerne (un declencheur ponctuel n'a pas besoin de Held).
    /// </summary>
    public abstract class ArucoAction : ScriptableObject
    {
        [Header("Identification")]
        [Tooltip("Identifiant du marqueur dans le dictionnaire DICT_4X4_50 (0 a 49). Doit etre unique dans le catalogue.")]
        [SerializeField] private int markerId;

        [Tooltip("Objet physique associe, tel que note dans le suivi des marqueurs (ex : \"Averse orageuse\").")]
        [SerializeField] private string objectName;

        [Header("Comportement")]
        [SerializeField] private ArucoActionFamily family;

        [Tooltip("Globale : agit sur tout le bac. Locale : agit la ou l'objet est pose (necessite la position monde du marqueur).")]
        [SerializeField] private ArucoActionScope scope = ArucoActionScope.Globale;

        [Tooltip("Si coche, l'activation de cette action desactive l'autre action exclusive de la meme famille (ex : sol sec / poreux / artificialise).")]
        [SerializeField] private bool exclusiveInFamily;

        public int MarkerId => markerId;
        public string ObjectName => objectName;
        public ArucoActionFamily Family => family;
        public ArucoActionScope Scope => scope;
        public bool ExclusiveInFamily => exclusiveInFamily;

        /// <summary>Le marqueur vient d'apparaitre dans le champ de la camera.</summary>
        public virtual void OnMarkerAppeared(ArucoMarkerReading reading) { }

        /// <summary>
        /// Le marqueur est toujours la. Appele a chaque detection : sa position
        /// peut avoir bouge si l'objet a ete deplace.
        /// </summary>
        public virtual void OnMarkerHeld(ArucoMarkerReading reading) { }

        /// <summary>
        /// Le marqueur n'est plus detecte (retire du bac, ou masque au dela du
        /// delai de tolerance du routeur).
        /// </summary>
        public virtual void OnMarkerDisappeared() { }

        /// <summary>
        /// Appelee quand une autre action exclusive de la meme famille prend le
        /// relais. Par defaut, equivaut a une disparition.
        /// </summary>
        public virtual void OnSupersededInFamily() => OnMarkerDisappeared();

        public override string ToString() =>
            $"[{markerId}] {(string.IsNullOrEmpty(objectName) ? name : objectName)}";
    }
}
