//
//  ModeSelector.cs
//
//	Copyright 2021 SensiLab, Monash University <sensilab@monash.edu>
//
//  This file is part of sensilab-ar-sandbox.
//
//  sensilab-ar-sandbox is free software: you can redistribute it and/or modify
//  it under the terms of the GNU General Public License as published by
//  the Free Software Foundation, either version 3 of the License, or
//  (at your option) any later version.
//
//  sensilab-ar-sandbox is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//  GNU General Public License for more details.
//
//  You should have received a copy of the GNU General Public License
//  along with sensilab-ar-sandbox.  If not, see <https://www.gnu.org/licenses/>.
//

using ARSandbox.Modes;
using Sandbox.Scripts.ServerClient;
using UnityEngine;

namespace ARSandbox
{
    public class ModeSelector : MonoBehaviour
    {

        public WaterSimulation.WaterSimulation WaterSimulation;
        public GeologySimulation.GeologySimulation GeologySimulation;
        public FireSimulation.FireSimulation FireSimulation;
        public TopographyBuilder.TopographyBuilder TopographyBuilder;
        public WindSimulation.WindSimulation WindSimulation;
        public SandboxClient SandboxClient;

        public GameObject CurrentMode;

        /// <summary>
        /// Mode en cours et regles qui en decoulent. Les composants peuvent le
        /// consulter depuis leur OnEnable pour savoir ce qu'on attend d'eux :
        /// l'etat est toujours pose avant l'activation (cf. EnterMode).
        /// </summary>
        public SandboxModeMachine Modes { get; } = new SandboxModeMachine();

        public void EnableWindSimulation()
        {
            EnterMode(SandboxMode.Wind, WindSimulation.gameObject);
        }
        public void EnableTopographyBuilder()
        {
            EnterMode(SandboxMode.Topography, TopographyBuilder.gameObject);
        }
        public void EnableGeologySimulation()
        {
            EnterMode(SandboxMode.Geology, GeologySimulation.gameObject);
        }
        public void EnableWaterSimulation()
        {
            EnterMode(SandboxMode.Water, WaterSimulation.gameObject);
        }
        public void EnableFireSimulation()
        {
            EnterMode(SandboxMode.Fire, FireSimulation.gameObject);
        }
        public void EnableServer()
        {
            EnterMode(SandboxMode.Server, SandboxClient.gameObject);
        }

        /// <summary>
        /// Entre dans un mode. L'etat est pose AVANT toute activation, parce que
        /// le OnEnable des composants le lit : le client serveur y decide s'il
        /// doit peindre le terrain ou laisser la main a la simulation. L'ancien
        /// code activait le client avant d'enregistrer le mode, qui lisait donc
        /// le mode precedent.
        /// </summary>
        private void EnterMode(SandboxMode mode, GameObject modeObject)
        {
            Modes.Enter(mode);
            CurrentMode = modeObject;

            modeObject.SetActive(true);

            // Le client serveur accompagne la simulation d'eau : c'est lui qui
            // recoit les images du serveur de detection, donc les marqueurs.
            if (Modes.ServerClientNeeded) SandboxClient.gameObject.SetActive(true);
        }

        public void DisableCurrentMode()
        {
            Modes.ReturnToMenu();

            if (CurrentMode != null) CurrentMode.SetActive(false);
            // Plus aucun mode ne tourne : le client serveur n'a plus de raison
            // d'etre, qu'il ait accompagne l'eau ou qu'il ait ete le mode.
            SandboxClient.gameObject.SetActive(false);

            // Sans cet oubli, un mode quitte restait "courant" a l'accueil et
            // les composants actives ensuite lisaient un mode qui ne tournait
            // plus.
            CurrentMode = null;
        }
        public void EnableCurrentMode()
        {
            if (CurrentMode != null) CurrentMode.gameObject.SetActive(true);
        }
    }
}
