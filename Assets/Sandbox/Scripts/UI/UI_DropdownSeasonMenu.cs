using UnityEngine;
using UnityEngine.Advertisements;
using UnityEngine.UI;

namespace ARSandbox
{
    public class UI_DropdownSeasonMenu : MonoBehaviour
    {
        public Sandbox sandbox;
        public Shader normalShader;
        public Shader winterShader;
        public Shader springShader;
        public Shader blackAndWhiteShader;
        public Image currentImage;

        public void ChangeSeasonDropdown(int season)
        {
            switch (season)
            {
                case 0:
                    sandbox.SetSandboxShader(normalShader);
                    break;
                case 1:
                    sandbox.SetSandboxShader(springShader);
                    break;
                case 2:
                    sandbox.SetSandboxShader(winterShader);
                    break;
                case 3:
                    sandbox.SetSandboxShader(blackAndWhiteShader);
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
    }
}