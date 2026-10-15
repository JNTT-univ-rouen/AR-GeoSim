namespace ARSandbox.Modes
{
    /// <summary>
    /// Les modes de la sandbox, tels que les boutons du menu principal les
    /// proposent. <see cref="None"/> est l'accueil : aucune simulation ne
    /// tourne, le terrain est rendu avec le shader par defaut.
    /// </summary>
    public enum SandboxMode
    {
        None = 0,
        Water,
        Geology,
        Fire,
        Wind,
        Topography,
        Server,
    }
}
