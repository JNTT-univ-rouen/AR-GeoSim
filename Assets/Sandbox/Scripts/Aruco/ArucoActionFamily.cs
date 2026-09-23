namespace ARSandbox.Aruco
{
    /// <summary>
    /// Familles d'actions, reprises du suivi des marqueurs (Notion /
    /// DictionnaireActions.ods). Sert surtout a regrouper les actions qui
    /// s'excluent entre elles : on ne peut pas avoir un sol sec ET poreux.
    ///
    /// Ajouter une famille ici ne casse rien : les actions existantes gardent
    /// la leur, et le routeur ne fait que comparer des valeurs.
    /// </summary>
    public enum ArucoActionFamily
    {
        LectureRelief,
        Precipitations,
        OccupationDuSol,
        QualiteDuSol
    }

    /// <summary>
    /// Portee d'une action : sur tout le bac, ou seulement autour de l'endroit
    /// ou l'objet est pose (dans ce cas ArucoMarkerReading.WorldPosition est
    /// necessaire).
    /// </summary>
    public enum ArucoActionScope
    {
        Globale,
        Locale
    }
}
