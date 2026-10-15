namespace ARSandbox.Modes
{
    /// <summary>
    /// Mode actif de la sandbox, et les quelques regles qui en decoulent :
    /// quels composants doivent tourner, et qui a le droit d'ecrire le shader
    /// du terrain.
    ///
    /// Cette classe est volontairement sans dependance a Unity : c'est la
    /// partie des transitions de mode que l'on peut verifier par des tests,
    /// sans Kinect, sans projecteur et sans scene (cf. SandboxModeMachineTests).
    ///
    /// Le principe tient en une phrase : un seul mode peint le terrain a la
    /// fois, et quand on revient a l'accueil le terrain reprend le shader par
    /// defaut. Deduire cela de l'etat courant, plutot que de le retenir d'une
    /// activation a l'autre, evite les drapeaux qui restent vrais trop
    /// longtemps - exactement ce qui empechait le mode serveur d'appliquer son
    /// shader des qu'on etait passe par la simulation d'eau.
    /// </summary>
    public sealed class SandboxModeMachine
    {
        /// <summary>Mode en cours. <see cref="SandboxMode.None"/> = accueil.</summary>
        public SandboxMode Current { get; private set; }

        /// <summary>
        /// Entre dans un mode, en remplacant celui qui tournait. Entrer dans
        /// <see cref="SandboxMode.None"/> revient a <see cref="ReturnToMenu"/>.
        /// </summary>
        public void Enter(SandboxMode mode)
        {
            Current = mode;
        }

        /// <summary>Quitte le mode en cours et revient a l'accueil.</summary>
        public void ReturnToMenu()
        {
            Current = SandboxMode.None;
        }

        /// <summary>Vrai des qu'une simulation tourne.</summary>
        public bool ModeRunning => Current != SandboxMode.None;

        /// <summary>
        /// Le client serveur doit tourner en mode serveur, evidemment, mais
        /// aussi pendant la simulation d'eau : c'est lui qui recoit les images
        /// du serveur de detection et donc les marqueurs ArUco.
        /// </summary>
        public bool ServerClientNeeded =>
            Current == SandboxMode.Server || Current == SandboxMode.Water;

        /// <summary>
        /// Le client serveur ne peint le terrain que lorsqu'il est lui-meme le
        /// mode choisi. Pendant la simulation d'eau, le terrain appartient a la
        /// simulation : son shader de metaballs doit survivre a l'activation du
        /// client, et ne pas etre efface quand celui-ci s'eteint.
        /// </summary>
        public bool ServerClientPaintsTerrain => Current == SandboxMode.Server;

        /// <summary>
        /// Vrai quand plus personne ne peint : le terrain doit alors afficher
        /// le shader par defaut. C'est l'etat attendu a l'accueil.
        /// </summary>
        public bool DefaultShaderExpected => !ModeRunning;

        /// <summary>
        /// Mode qui a la main sur le shader du terrain, ou
        /// <see cref="SandboxMode.None"/> si le shader par defaut est attendu.
        /// </summary>
        public SandboxMode TerrainPaintedBy => Current;
    }
}
