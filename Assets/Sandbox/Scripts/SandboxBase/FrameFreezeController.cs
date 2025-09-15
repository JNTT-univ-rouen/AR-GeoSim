using UnityEngine;
using UnityEngine.UI;

namespace ARSandbox
{
    /// <summary>
    /// Provides UI controls for the frame freeze system
    /// </summary>
    public class FrameFreezeController : MonoBehaviour
    {
        [Header("References")]
        public Sandbox sandbox;
        public FreezeFrame freezeFrame;
        
        [Header("UI Elements")]
        public Button freezeButton;
        public Button unfreezeButton;
        public Text statusText;
        public Image freezeIndicator;
        
        [Header("Visual Feedback")]
        public Color frozenColor = Color.red;
        public Color liveColor = Color.green;
        
        private void Start()
        {
            if (sandbox == null)
                sandbox = FindObjectOfType<Sandbox>();
                
            UpdateUI();
        }
        
        public void ToggleFreeze()
        {
            if (sandbox != null)
            {
                sandbox.ToggleFrameFreeze();
                UpdateUI();
            }
        }
        
        private void UpdateUI()
        {
            if (sandbox == null) return;
            
            bool isFrozen = freezeFrame.isFrameFrozen;
            
            // Update button states
            if (freezeButton != null)
                freezeButton.interactable = !isFrozen;
            if (unfreezeButton != null)
                unfreezeButton.interactable = isFrozen;
                
            // Update status text
            if (statusText != null)
                statusText.text = isFrozen ? "FRAME FROZEN" : "LIVE";
                
            // Update visual indicator
            if (freezeIndicator != null)
                freezeIndicator.color = isFrozen ? frozenColor : liveColor;
        }
    }
}
