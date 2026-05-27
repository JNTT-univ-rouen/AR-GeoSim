using System;
using UnityEngine;
using UnityEngine.Advertisements;
using UnityEngine.UI;

namespace ARSandbox
{
    public class UI_DropdownSeasonMenu : MonoBehaviour
    {
        public Sandbox sandbox;
        public Toggle waterAbsorbtionToggle;
        public Slider waterAbsorbtionSlider;
        public Slider precipitationSlider;
        public Shader normalShader;
        public Shader winterShader;
        public Shader springShader;
        public Shader blackAndWhiteShader;
        public Image currentImage;
        public int Season;

        [Header("Terrain photo (Green / Orange shaders)")]
        [Tooltip("Assign the PNG texture (Green.png), not a Material. Used when winterShader (green) is active.")]
        public Texture2D greenTerrainAlbedo;
        [Tooltip("Optional. Used when springShader (dried/orange) is active.")]
        public Texture2D driedTerrainAlbedo;
        [Range(0f, 1f)]
        public float greenTerrainAlbedoStrength = 0.35f;
        [Range(0f, 1f)]
        public float driedTerrainAlbedoStrength = 0.35f;
        [Tooltip("1 = stretch one image over the whole sandbox. Values above 1 tile the texture (e.g. 2 = two copies per side).")]
        public float greenTerrainAlbedoRepeat = 1f;
        public float driedTerrainAlbedoRepeat = 1f;

        public void ChangeSeasonDropdown(int season)
        {
            switch (season)
            {
                case 0:
                    sandbox.SetSandboxShader(normalShader);
                    sandbox.ClearTerrainAlbedo();
                    Season = season;
                    break;
                case 1:
                    sandbox.SetSandboxShader(springShader);
                    ApplyDriedTerrainAlbedo();
                    Season = season;
                    break;
                case 2:
                    sandbox.SetSandboxShader(winterShader);
                    ApplyGreenTerrainAlbedo();
                    Season = season;
                    break;
                case 3:
                    sandbox.SetSandboxShader(blackAndWhiteShader);
                    sandbox.ClearTerrainAlbedo();
                    Season = season;
                    break;
            }
        }

        private void ApplyGreenTerrainAlbedo()
        {
            Texture2D albedo = greenTerrainAlbedo != null
                ? greenTerrainAlbedo
                : Resources.Load<Texture2D>("Green");
            sandbox.ApplyTerrainAlbedo(albedo, greenTerrainAlbedoStrength, greenTerrainAlbedoRepeat);
        }

        private void ApplyDriedTerrainAlbedo()
        {
            if (driedTerrainAlbedo == null)
            {
                sandbox.ClearTerrainAlbedo();
                return;
            }
            sandbox.ApplyTerrainAlbedo(driedTerrainAlbedo, driedTerrainAlbedoStrength, driedTerrainAlbedoRepeat);
        }

        public void ChangeSeasonImage(int season)
        {
            switch (season)
            {
                case 0:
                    currentImage.sprite = Resources.Load<Sprite>("seasons");
                    break;
                case 1:
                    currentImage.sprite = Resources.Load<Sprite>("sun");
                    break;
                case 2:
                    currentImage.sprite = Resources.Load<Sprite>("snowflake");
                    break;
                case 3:
                    currentImage.sprite = Resources.Load<Sprite>("snowflake");
                    break;
            }
        }
        
        public void ChangeWeatherParameters(int season)
        {
            switch (season)
            {
                case 0:
                    waterAbsorbtionToggle.isOn = false;
                    precipitationSlider.value =  1.0f;
                    waterAbsorbtionSlider.value = 1.0f;
                    break;
                case 1:
                    waterAbsorbtionToggle.isOn = true; 
                    precipitationSlider.value = 1.5f;
                    waterAbsorbtionSlider.value = 0.125f;
                    break;
                case 2:
                    waterAbsorbtionToggle.isOn = true;
                    precipitationSlider.value = 1.0f;
                    waterAbsorbtionSlider.value = 0.28f;
                    break;
                case 3:
                    waterAbsorbtionToggle.isOn = false;
                    precipitationSlider.value =  1.0f;
                    waterAbsorbtionSlider.value = 1.0f;
                    break;
            }
        }
    }
}