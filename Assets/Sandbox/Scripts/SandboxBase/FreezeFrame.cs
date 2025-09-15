using UnityEngine;
using System.Collections.Generic;
using ARSandbox.WaterSimulation;

namespace ARSandbox
{
    public class FreezeFrame : MonoBehaviour
    {
        [Header("Frame Freeze System")]
        public bool enableFrameFreeze = true;
        public bool isFrameFrozen = false;
        public KeyCode freezeFrameKey = KeyCode.F;

        public Sandbox sandbox;
        private WaterSimulation.WaterSimulation waterSim;
        public RenderTexture frozenDepthRT;
        public RenderTexture frozenProcessedRT;
        public bool hasFrozenFrame = false;

        // Gesture storage system
        private List<StoredGesture> storedGestures = new List<StoredGesture>();
        public int unfreezeFrameCount = 0;
        private const int GESTURE_DELAY_FRAMES = 15;

        private void Awake()
        {
            waterSim = FindObjectOfType<WaterSimulation.WaterSimulation>();
        }
        public void OnFreezeFrame(Texture2D rawDepthsTex, RenderTexture processedDepthsRT)
        {
            if (!isFrameFrozen && sandbox.SandboxReady)
            {
                isFrameFrozen = true;
                
                if (frozenDepthRT == null)
                    frozenDepthRT = CreateDepthRT(sandbox.GetSandboxDescriptor().DataSize);
                if (frozenProcessedRT == null)
                    frozenProcessedRT = CreateDepthRT(sandbox.GetSandboxDescriptor().DataSize);
                
                Graphics.Blit(rawDepthsTex, frozenDepthRT);
                Graphics.Blit(processedDepthsRT, frozenProcessedRT);
                
                hasFrozenFrame = true;
            }
        }

        public void UnfreezeFrame()
        {
            if (isFrameFrozen)
            {
                isFrameFrozen = false;
                hasFrozenFrame = false;
                unfreezeFrameCount = 0;
            }
        }

        

        public void StoreGesture(Vector3 worldPosition, int gestureID = -1)
        {
            storedGestures.Add(new StoredGesture(worldPosition, Time.time, GESTURE_DELAY_FRAMES, gestureID));
        }

        public void ProcessStoredGestures()
        {
            if (!isFrameFrozen || storedGestures.Count == 0) return;

            unfreezeFrameCount++;
            for (int i = storedGestures.Count - 1; i >= 0; i--)
            {
                storedGestures[i].delayFrames--;
                if (storedGestures[i].delayFrames <= 0)
                {
                    TriggerWaterDrop(storedGestures[i].worldPosition);
                    storedGestures.RemoveAt(i);
                }
            }
        }

        private void TriggerWaterDrop(Vector3 worldPosition)
        {
            if (waterSim != null)
                waterSim.DropWater(worldPosition);
        }

        public RenderTexture GetFrozenDepthTexture() => frozenDepthRT;
        public RenderTexture GetFrozenProcessedTexture() => frozenProcessedRT;
        public int GetStoredGestureCount() => storedGestures.Count;
        public void ClearStoredGestures() => storedGestures.Clear();

        private RenderTexture CreateDepthRT(Point size)
        {
            var rt = new RenderTexture(size.x, size.y, 0, RenderTextureFormat.RHalf);
            rt.enableRandomWrite = true;
            rt.Create();
            return rt;
        }
    }

    [System.Serializable]
    public class StoredGesture
    {
        public Vector3 worldPosition;
        public float timestamp;
        public int delayFrames;
        public int gestureID;

        public StoredGesture(Vector3 position, float time, int delay, int id = -1)
        {
            worldPosition = position;
            timestamp = time;
            delayFrames = delay;
            gestureID = id;
        }
    }
}
