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
        

        public void ChangeSeasonDropdown(int season)
        {
            switch (season)
            {
                case 0:
                    sandbox.SetSandboxShader(normalShader);
                    Season = season;
                    break;
                case 1:
                    sandbox.SetSandboxShader(springShader);
                    Season = season;
                    break;
                case 2:
                    sandbox.SetSandboxShader(winterShader);
                    Season = season;
                    break;
                case 3:
                    sandbox.SetSandboxShader(blackAndWhiteShader);
                    Season = season;
                    break;
            }
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