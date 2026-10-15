using UnityEngine;

namespace ARSandbox.Aruco.Actions
{
    /// <summary>
    /// Change la qualite du sol de TOUT le bac : a quel point l'eau s'infiltre.
    /// Correspond aux marqueurs "sec", "artificialise" et "poreux" du suivi des
    /// marqueurs.
    ///
    /// Un seul de ces marqueurs peut etre actif a la fois : cocher
    /// "Exclusive In Family" sur les assets. Le routeur appelle alors
    /// OnSupersededInFamily sur le precedent, qui rend la main sans restaurer
    /// quoi que ce soit - c'est le nouveau venu qui impose son reglage.
    ///
    /// L'etat d'origine est releve au premier changement et restaure quand plus
    /// aucune carte de la famille n'est sur le bac.
    /// </summary>
    [CreateAssetMenu(fileName = "SoilQualityArucoAction", menuName = "Sandbox/ArUco/Qualite du sol")]
    public class SoilQualityArucoAction : ArucoAction
    {
        [Header("Absorption de l'eau par le sol")]
        [Tooltip("Le sol absorbe-t-il l'eau ? Decoche pour un sol impermeable (artificialise) : l'eau reste en surface et ruisselle.")]
        [SerializeField] private bool absorptionActive = true;

        [Tooltip("Vitesse d'infiltration, en gouttes absorbees par seconde. La simulation detruit une goutte toutes les WaterAbsorptionSpeed/60 secondes : 1 goutte/s = sol tres sec, 10 gouttes/s = sol tres poreux.")]
        [Range(0.2f, 30f)]
        [SerializeField] private float absorptionDropsPerSecond = 2f;

        [Tooltip("Coche : le reglage reste en place quand la carte quitte le bac, et seule une autre carte peut le changer. Decoche : l'etat d'avant la carte est restaure a son retrait.")]
        [SerializeField] private bool persistant = true;

        // Etat d'execution, partage par toutes les qualites de sol : une seule
        // est active a la fois, et c'est le reglage d'AVANT le premier marqueur
        // qu'il faut pouvoir rendre. S'il etait stocke par asset, enchainer
        // "sec" puis "poreux" ferait prendre le reglage du sec pour l'original.
        //
        // Statique et non serialise : l'editeur ecrirait sinon cet etat dans
        // l'asset et le conserverait entre deux sessions.
        [System.NonSerialized] private static bool hasSavedState;
        [System.NonSerialized] private static bool savedAbsorptionActive;
        [System.NonSerialized] private static float savedAbsorptionSpeed;
        [System.NonSerialized] private static bool savedAbsorptionDisabled;
        [System.NonSerialized] private static bool savedHadGlobalOverride;

        public override void OnMarkerAppeared(ArucoMarkerReading reading, ArucoSceneContext context)
        {
            if (context == null) return;

            ARSandbox.WaterSimulation.WaterSimulation water = context.RequireWaterSimulation(this);
            if (water == null) return;

            SaveStateOnce(water);

            water.UI_ToggleActivateWaterAbsorption(absorptionActive);
            ApplyAbsorption(water, absorptionDropsPerSecond);

            Debug.Log($"{this} : sol {(absorptionActive ? "absorbant" : "impermeable")} " +
                      $"({absorptionDropsPerSecond:F1} goutte(s)/s)");
        }

        public override void OnMarkerDisappeared(ArucoSceneContext context)
        {
            if (persistant || context == null) return;

            ARSandbox.WaterSimulation.WaterSimulation water = context.WaterSimulation;
            if (water == null || !hasSavedState) return;

            // Plus de carte de qualite du sol sur le bac : on remet ce qui etait
            // regle avant, pour ne pas laisser l'interface et la simulation
            // desynchronisees apres une demonstration.
            water.UI_ToggleActivateWaterAbsorption(savedAbsorptionActive);

            if (!savedHadGlobalOverride && water.terrainAbsorption != null)
            {
                // Aucune carte de sol avant celle qu'on retire : le terrain
                // reprend la main, cellule par cellule.
                water.terrainAbsorption.ClearGlobalOverride();
            }

            // Le curseur etait au minimum : on le remet a zero, et non a la
            // derniere vitesse connue, qui ne servait plus a rien.
            water.UI_SetWaterAbsorptionSpeed(
                savedAbsorptionDisabled ? 0f : SpeedToSliderValue(savedAbsorptionSpeed));
            hasSavedState = false;

            Debug.Log($"{this} : retour au reglage d'origine du sol");
        }

        /// <summary>
        /// Une autre qualite de sol prend le relais : elle impose son reglage
        /// juste apres. Ne rien restaurer ici, et surtout garder l'etat
        /// d'origine : c'est la derniere carte retiree qui le rendra.
        /// </summary>
        public override void OnSupersededInFamily(ArucoSceneContext context) { }

        private void SaveStateOnce(ARSandbox.WaterSimulation.WaterSimulation water)
        {
            if (hasSavedState) return;

            savedAbsorptionActive = water.WaterAbsorbtionToggle != null && water.WaterAbsorbtionToggle.isOn;
            savedAbsorptionSpeed = water.WaterAbsorptionSpeed;
            savedAbsorptionDisabled = water.WaterAbsorptionDisabled;
            savedHadGlobalOverride = water.terrainAbsorption != null
                                     && water.terrainAbsorption.HasGlobalOverride;
            hasSavedState = true;
        }

        /// <summary>
        /// Inverse de UI_SetWaterAbsorptionSpeed, qui fait WaterAbsorptionSpeed
        /// = 1 / valeur recue.
        /// </summary>
        private static float SpeedToSliderValue(float speed)
        {
            return speed > 0.0001f ? 1f / speed : 1f;
        }

        /// <summary>
        /// Applique le debit d'infiltration de cette carte a tout le bac.
        ///
        /// Le debit est impose au tableau par terrain (HexTerrainAbsorption),
        /// et non traduit en valeur de curseur : depuis que l'infiltration se
        /// lit cellule par cellule, le curseur n'est plus un debit absolu mais
        /// un facteur de 0 a 1. Y envoyer "10 gouttes par seconde" donnait un
        /// facteur de 0,17, et le sol poreux se retrouvait six fois plus sec
        /// que le reglage par defaut - l'inverse de ce qui est annonce.
        ///
        /// Sans tableau dans la scene, on retombe sur l'ancien chemin : le
        /// curseur global, ou la valeur est bien un debit.
        /// </summary>
        private static void ApplyAbsorption(ARSandbox.WaterSimulation.WaterSimulation water,
                                            float dropsPerSecond)
        {
            if (water.terrainAbsorption != null)
            {
                water.terrainAbsorption.SetGlobalOverride(dropsPerSecond);
                return;
            }

            water.UI_SetWaterAbsorptionSpeed(DropsPerSecondToSetting(dropsPerSecond));
        }

        /// <summary>
        /// Convertit des gouttes par seconde en valeur attendue par
        /// UI_SetWaterAbsorptionSpeed.
        ///
        /// La simulation attend WaterAbsorptionSpeed/60 secondes entre deux
        /// gouttes absorbees, donc WaterAbsorptionSpeed = 60 / gouttes par
        /// seconde. Et UI_SetWaterAbsorptionSpeed inverse ce qu'on lui donne.
        /// </summary>
        private static float DropsPerSecondToSetting(float dropsPerSecond)
        {
            if (dropsPerSecond <= 0.0001f) return 1f;
            return dropsPerSecond / 60f;
        }
    }
}
