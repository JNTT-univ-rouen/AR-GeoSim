using NUnit.Framework;

namespace ARSandbox.Modes.Tests
{
    /// <summary>
    /// Transitions entre l'accueil et les differents modes de la sandbox.
    ///
    /// Ce qu'on verifie ici tient a une seule question : apres un enchainement
    /// de passages d'un mode a l'autre, qui a la main sur le shader du terrain ?
    /// Les deux bugs qui ont motive ces tests venaient d'un etat garde trop
    /// longtemps d'une activation a la suivante, et non du calcul lui-meme :
    /// ils n'apparaissaient donc qu'apres un certain ordre de navigation. D'ou
    /// des tests par sequences plutot que par appel isole.
    /// </summary>
    public class SandboxModeMachineTests
    {
        SandboxModeMachine modes;

        [SetUp]
        public void SetUp()
        {
            modes = new SandboxModeMachine();
        }

        [Test]
        public void AuDemarrage_OnEstALAccueil_EtLeTerrainPrendLeShaderParDefaut()
        {
            Assert.AreEqual(SandboxMode.None, modes.Current);
            Assert.IsFalse(modes.ModeRunning);
            Assert.IsTrue(modes.DefaultShaderExpected);
            Assert.IsFalse(modes.ServerClientNeeded);
            Assert.IsFalse(modes.ServerClientPaintsTerrain);
        }

        [Test]
        public void EnSimulationDEau_LeClientServeurTourne_MaisNePeintPasLeTerrain()
        {
            modes.Enter(SandboxMode.Water);

            // Le client doit tourner : c'est lui qui apporte les marqueurs.
            Assert.IsTrue(modes.ServerClientNeeded);
            // Mais le terrain appartient a la simulation, pas a lui.
            Assert.IsFalse(modes.ServerClientPaintsTerrain);
            Assert.AreEqual(SandboxMode.Water, modes.TerrainPaintedBy);
            Assert.IsFalse(modes.DefaultShaderExpected);
        }

        [Test]
        public void DeLEauVersLAccueil_LeTerrainRedevientParDefaut_EtLeClientServeurSArrete()
        {
            modes.Enter(SandboxMode.Water);
            modes.ReturnToMenu();

            Assert.AreEqual(SandboxMode.None, modes.Current);
            Assert.IsTrue(modes.DefaultShaderExpected);
            Assert.IsFalse(modes.ServerClientNeeded);
            Assert.IsFalse(modes.ServerClientPaintsTerrain);
        }

        [Test]
        public void ApresUnPassageParLEau_LeModeServeurAppliqueQuandMemeSonShader()
        {
            // Regression : le client serveur retenait "on est en simulation
            // d'eau" dans un drapeau jamais remis a faux. Une fois passe par
            // l'eau, le mode serveur n'appliquait plus jamais son shader.
            modes.Enter(SandboxMode.Water);
            modes.ReturnToMenu();
            modes.Enter(SandboxMode.Server);

            Assert.IsTrue(modes.ServerClientPaintsTerrain);
            Assert.AreEqual(SandboxMode.Server, modes.TerrainPaintedBy);
        }

        [Test]
        public void ApresUnPassageParLeServeur_LaSimulationDEauGardeLaMainSurLeTerrain()
        {
            // Regression symetrique : au retour dans l'eau, le client serveur
            // ne doit pas reprendre la main et ecraser le shader de metaballs.
            modes.Enter(SandboxMode.Server);
            modes.ReturnToMenu();
            modes.Enter(SandboxMode.Water);

            Assert.IsFalse(modes.ServerClientPaintsTerrain);
            Assert.IsTrue(modes.ServerClientNeeded);
            Assert.AreEqual(SandboxMode.Water, modes.TerrainPaintedBy);
        }

        [Test]
        public void DuServeurVersLAccueil_LeTerrainRedevientParDefaut()
        {
            modes.Enter(SandboxMode.Server);
            modes.ReturnToMenu();

            Assert.IsTrue(modes.DefaultShaderExpected);
            Assert.IsFalse(modes.ServerClientNeeded);
            Assert.IsFalse(modes.ServerClientPaintsTerrain);
        }

        [Test]
        public void DixAllersRetoursEntreLEauEtLAccueil_NeLaissentAucuneTrace()
        {
            // Un etat qui derive ne se voit pas au premier passage. On insiste.
            for (int i = 0; i < 10; i++)
            {
                modes.Enter(SandboxMode.Water);
                Assert.IsTrue(modes.ServerClientNeeded, "passage " + i);
                Assert.IsFalse(modes.ServerClientPaintsTerrain, "passage " + i);

                modes.ReturnToMenu();
                Assert.IsTrue(modes.DefaultShaderExpected, "passage " + i);
                Assert.IsFalse(modes.ServerClientNeeded, "passage " + i);
            }
        }

        [Test]
        public void UneNavigationComplete_RevientToujoursAuShaderParDefautALAccueil()
        {
            SandboxMode[] parcours =
            {
                SandboxMode.Water, SandboxMode.Server, SandboxMode.Geology,
                SandboxMode.Fire, SandboxMode.Water, SandboxMode.Wind,
                SandboxMode.Topography, SandboxMode.Server, SandboxMode.Water,
            };

            foreach (SandboxMode mode in parcours)
            {
                modes.Enter(mode);
                Assert.AreEqual(mode, modes.TerrainPaintedBy, mode.ToString());
                Assert.IsFalse(modes.DefaultShaderExpected, mode.ToString());

                modes.ReturnToMenu();
                Assert.IsTrue(modes.DefaultShaderExpected, "retour depuis " + mode);
                Assert.IsFalse(modes.ServerClientNeeded, "retour depuis " + mode);
            }
        }

        [TestCase(SandboxMode.Water)]
        [TestCase(SandboxMode.Geology)]
        [TestCase(SandboxMode.Fire)]
        [TestCase(SandboxMode.Wind)]
        [TestCase(SandboxMode.Topography)]
        [TestCase(SandboxMode.Server)]
        public void ToutModeActifPeintLeTerrain(SandboxMode mode)
        {
            modes.Enter(mode);

            Assert.IsTrue(modes.ModeRunning);
            Assert.IsFalse(modes.DefaultShaderExpected);
            Assert.AreEqual(mode, modes.TerrainPaintedBy);
        }

        [TestCase(SandboxMode.Water, true)]
        [TestCase(SandboxMode.Server, true)]
        [TestCase(SandboxMode.Geology, false)]
        [TestCase(SandboxMode.Fire, false)]
        [TestCase(SandboxMode.Wind, false)]
        [TestCase(SandboxMode.Topography, false)]
        public void SeulsLEauEtLeServeurOntBesoinDuClientServeur(SandboxMode mode, bool attendu)
        {
            modes.Enter(mode);

            Assert.AreEqual(attendu, modes.ServerClientNeeded);
        }

        [TestCase(SandboxMode.Server, true)]
        [TestCase(SandboxMode.Water, false)]
        [TestCase(SandboxMode.Geology, false)]
        [TestCase(SandboxMode.None, false)]
        public void SeulLeModeServeurLaisseLeClientPeindreLeTerrain(SandboxMode mode, bool attendu)
        {
            modes.Enter(mode);

            Assert.AreEqual(attendu, modes.ServerClientPaintsTerrain);
        }

        [Test]
        public void PasserDirectementDUnModeAUnAutre_RemplaceLePrecedent()
        {
            // L'interface passe par l'accueil, mais rien dans le code ne
            // l'impose : le mode precedent ne doit pas survivre.
            modes.Enter(SandboxMode.Water);
            modes.Enter(SandboxMode.Geology);

            Assert.AreEqual(SandboxMode.Geology, modes.Current);
            Assert.IsFalse(modes.ServerClientNeeded);
            Assert.IsFalse(modes.ServerClientPaintsTerrain);
        }

        [Test]
        public void EntrerDansLeMemeModeDeuxFois_NeChangeRien()
        {
            modes.Enter(SandboxMode.Water);
            modes.Enter(SandboxMode.Water);

            Assert.AreEqual(SandboxMode.Water, modes.Current);
            Assert.IsTrue(modes.ServerClientNeeded);
            Assert.IsFalse(modes.ServerClientPaintsTerrain);
        }

        [Test]
        public void RevenirALAccueilDepuisLAccueil_EstSansEffet()
        {
            modes.ReturnToMenu();
            modes.ReturnToMenu();

            Assert.AreEqual(SandboxMode.None, modes.Current);
            Assert.IsTrue(modes.DefaultShaderExpected);
        }

        [Test]
        public void EntrerDansNone_EquivautAUnRetourALAccueil()
        {
            modes.Enter(SandboxMode.Water);
            modes.Enter(SandboxMode.None);

            Assert.IsTrue(modes.DefaultShaderExpected);
            Assert.IsFalse(modes.ServerClientNeeded);
        }
    }
}
