//  
//  WaterSimulation.cs
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

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Security.Policy;
using TMPro.SpriteAssetUtilities;
//using UnityEditor.ShaderKeywordFilter;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace ARSandbox.WaterSimulation
{
    public class WaterSimulation : MonoBehaviour
    {
        public Sandbox Sandbox;
        public HandInput HandInput;
        public FreezeFrame FreezeFrame;
        public CalibrationManager CalibrationManager;
        public WaterDroplet WaterDroplet;
        public Camera MetaballCamera;
        public Shader MetaballShader;
        public ComputeShader WaterSurfaceComputeShader;
        public Texture2D WaterColorTexture;

        private SandboxDescriptor sandboxDescriptor;
        private List<WaterDroplet> waterDroplets;
        private RenderTexture metaballRT;
        private int currSubsection;
        private bool showParticles;

        private RenderTexture waterBufferRT0;
        private RenderTexture waterBufferRT1;
        private bool swapBuffers;

        private IEnumerator RunSimulationCoroutine;
        private bool initialised;
        
        private IEnumerator RunAbsorptionCoroutine;
        private bool _soilAbsorptionActive;
        public float WaterAbsorptionSpeed { get; private set; }
        public Toggle WaterAbsorbtionToggle ;

        [Header("Hand Detection Settings")]
        public int HandTresholdMin = 600;
        public int HandTresholdMax = 1150;
        public float handDetectionInterval = 0.1f; // Detection every 100ms instead of every frame
        public float minHandMovement = 5f; // Minimum movement to trigger water drop
        public float maxHandMovement = 25f; // Maximum movement to reset the stability check
        public float maxHandDistance = 50f; // Maximum distance from sandbox center
        
        [Header("HandInput Integration")]
        public bool enableHandInputIntegration = true; // Enable integration with HandInput system
        public float gestureCleanupInterval = 0.5f; // How often to clean up old gestures
        public float gestureMaxAge = 2.0f; // Maximum age of gestures before cleanup
        
        private int[] handDepthData;
        private Vector3 lastHandPosition = Vector3.zero;
        private int nextGestureID = 1000; // Start from 1000 to avoid conflicts with UI gestures
        private float lastCleanupTime = 0f;

        private int stabilityThreshold = 60;
        
        private const int MaxMetaballs = 2000;


        void InitialiseSimulation()
        {
            if (!initialised)
            {
                waterDroplets = new List<WaterDroplet>();
                currSubsection = 0;
                showParticles = false;

                CreateWaterSurfaceRenderTextures();
                swapBuffers = false;
                
                WaterAbsorptionSpeed = 1.0f;
                HandTresholdMin = 600;
                HandTresholdMax = 1150;
            }
            initialised = true;
        }

        IEnumerator RunSimulation()
        {
            while (true)
            {
                CullStrayMetaballs();

                KeepMetaballsAboveSandbox();

                StepWaterSurfaceSimulation();
                if (Random.value < 2 / 60.0f) DisturbWaterSurfaceSimulation();

                yield return new WaitForSeconds(1 / 60.0f);
            }
        }

        IEnumerator ReduceWater()
        {
            while (waterDroplets.Count > 0 & _soilAbsorptionActive)
            {
                Destroy(waterDroplets[waterDroplets.Count - 1]);
                waterDroplets.RemoveAt(waterDroplets.Count - 1);
                
                yield return new WaitForSeconds(WaterAbsorptionSpeed/ 60.0f);
            }

            this.WaterAbsorbtionToggle.isOn = false;
        }

        void OnEnable()
        {
            InitialiseSimulation();

            HandInput.OnGesturesReady += OnGesturesReady;
            CalibrationManager.OnCalibration += OnCalibration;
            Sandbox.OnSandboxReady += OnSandboxReady;
            sandboxDescriptor = Sandbox.GetSandboxDescriptor();

            SetUpMetaballCamera();
            MetaballCamera.gameObject.SetActive(true);
            
            StartCoroutine(GetHandPositionCoroutine());
            StartCoroutine(RunSimulationCoroutine = RunSimulation());

        }


        void OnDisable()
        {
            HandInput.OnGesturesReady -= OnGesturesReady;
            CalibrationManager.OnCalibration -= OnCalibration;
            Sandbox.OnSandboxReady -= OnSandboxReady;
            Sandbox.SetDefaultShader();
            MetaballCamera.gameObject.SetActive(false);

            DestroyWaterDroplets();

            StopCoroutine(RunSimulationCoroutine);
        }

        private void OnCalibration()
        {
            HandInput.OnGesturesReady -= OnGesturesReady;
            MetaballCamera.gameObject.SetActive(false);
            DestroyWaterDroplets();
            Sandbox.SetDefaultShader();

            StopCoroutine(RunSimulationCoroutine);
        }

        private void OnSandboxReady()
        {
            InitialiseSimulation();

            HandInput.OnGesturesReady += OnGesturesReady;
            SetUpMetaballCamera();
            sandboxDescriptor = Sandbox.GetSandboxDescriptor();

            StartCoroutine(RunSimulationCoroutine = RunSimulation());

        }

        private void CreateWaterSurfaceRenderTextures()
        {
            waterBufferRT0 = new RenderTexture(256, 256, 0, RenderTextureFormat.RHalf, RenderTextureReadWrite.Linear);
            waterBufferRT0.filterMode = FilterMode.Bilinear;
            waterBufferRT0.wrapMode = TextureWrapMode.Repeat;
            waterBufferRT0.enableRandomWrite = true;
            waterBufferRT0.Create();

            WaterSurfaceCSHelper.Run_FillRenderTexture(WaterSurfaceComputeShader, waterBufferRT0, 0.5f);

            waterBufferRT1 = new RenderTexture(256, 256, 0, RenderTextureFormat.RHalf, RenderTextureReadWrite.Linear);
            waterBufferRT1.filterMode = FilterMode.Bilinear;
            waterBufferRT1.wrapMode = TextureWrapMode.Repeat;
            waterBufferRT1.enableRandomWrite = true;
            waterBufferRT1.Create();

            WaterSurfaceCSHelper.Run_FillRenderTexture(WaterSurfaceComputeShader, waterBufferRT1, 0.5f);
        }

        private void StepWaterSurfaceSimulation()
        {
            if (swapBuffers)
            {
                WaterSurfaceCSHelper.Run_StepWaterSim(WaterSurfaceComputeShader, waterBufferRT0, waterBufferRT1, 1 / 60f, 1, 20, 0.999f, true);
                Sandbox.SetShaderTexture("_WaterSurfaceTex", waterBufferRT0);
            }
            else
            {
                WaterSurfaceCSHelper.Run_StepWaterSim(WaterSurfaceComputeShader, waterBufferRT1, waterBufferRT0, 1 / 60f, 1, 20, 0.999f, true);
                Sandbox.SetShaderTexture("_WaterSurfaceTex", waterBufferRT1);
            }
            swapBuffers = !swapBuffers;
        }

        private void DisturbWaterSurfaceSimulation()
        {
            Vector2 point0 = new Vector2(32 + Random.value * 224.0f, 32 + Random.value * 224.0f);
            Vector2 point1 = new Vector2(32 + Random.value * 224.0f, 32 + Random.value * 224.0f);
            Vector2 point2 = new Vector2(32 + Random.value * 224.0f, 32 + Random.value * 224.0f);
            Vector2 point3 = new Vector2(32 + Random.value * 224.0f, 32 + Random.value * 224.0f);

            Vector2[] centres = new Vector2[4] { point0, point1, point2, point3 };
            float[] radii = new float[4] { 5, 5, 10, 10 };
            float[] powers = new float[4] { 0.1f * Random.value, -0.1f * Random.value, 0.25f, -0.25f };

            WaterSurfaceCSHelper.Run_DisplaceWater(WaterSurfaceComputeShader, waterBufferRT0, waterBufferRT1, centres, radii, powers, 2);
        }

        private void KeepMetaballsAboveSandbox()
        {
            if (currSubsection == Sandbox.COLL_MESH_DELAY - 1)
            {
                if (waterDroplets.Count < 200)
                {
                    foreach (WaterDroplet droplet in waterDroplets)
                    {
                        float sandboxDepth = Sandbox.GetDepthFromWorldPos(droplet.transform.position);
                        if (droplet.transform.position.z > sandboxDepth)
                        {
                            droplet.SetZPosition(sandboxDepth - WaterDroplet.DROPLET_RADIUS * 1.25f);
                        }
                    }
                }

                currSubsection = 0;
            }
            else
            {
                if (waterDroplets.Count >= 200)
                {
                    int dropletStep = waterDroplets.Count / Sandbox.COLL_MESH_DELAY;
                    for (int i = currSubsection * dropletStep; i < (currSubsection + 1) * dropletStep; i++)
                    {
                        if (i < waterDroplets.Count)
                        {
                            WaterDroplet droplet = waterDroplets[i];
                            float sandboxDepth = Sandbox.GetDepthFromWorldPos(droplet.transform.position);
                            // Constant of 10 is a small buffer to allow stacked water to rest.
                            if (droplet.transform.position.z > sandboxDepth - WaterDroplet.DROPLET_RADIUS + 10)
                            {
                                droplet.SetZPosition(sandboxDepth - WaterDroplet.DROPLET_RADIUS);
                            }
                        }
                    }
                }
                currSubsection += 1;
            }
        }

        private void CullStrayMetaballs()
        {
            float minX = sandboxDescriptor.MeshStart.x - 5;
            float minY = sandboxDescriptor.MeshStart.y - 5;
            float maxX = sandboxDescriptor.MeshEnd.x + 5;
            float maxY = sandboxDescriptor.MeshEnd.y + 5;
            int metaballCount = 0;

            for (int i = waterDroplets.Count - 1; i >= 0; i--)
            {
                WaterDroplet droplet = waterDroplets[i];
                Vector3 position = droplet.transform.position;
                if (position.x < minX || position.y < minY || position.x > maxX || position.y > maxY)
                {
                    Destroy(droplet.gameObject);
                    Destroy(droplet);
                    waterDroplets.RemoveAt(i);
                } else
                {
                    metaballCount++;
                    if (metaballCount > MaxMetaballs)
                    {
                        Destroy(droplet.gameObject);
                        Destroy(droplet);
                        waterDroplets.RemoveAt(i);
                    }
                }
            }
        }
        private void SetUpMetaballCamera()
        {
            CalibrationManager.SetUpDataCamera(MetaballCamera);
            CreateMetaballRT();
            MetaballCamera.targetTexture = metaballRT;
            Sandbox.SetSandboxShader(MetaballShader);
            Sandbox.SetShaderTexture("_MetaballTex", metaballRT);
            Sandbox.SetShaderTexture("_WaterColorTex", WaterColorTexture);

            Vector2 waterSurfaceTexScaling = new Vector2((float)sandboxDescriptor.DataSize.x / (float)sandboxDescriptor.DataSize.y * 1f, 1f);
            Sandbox.SetTextureProperties("_WaterSurfaceTex", Vector2.zero, waterSurfaceTexScaling);
        }

        private void CreateMetaballRT()
        {
            float aspectRatio = (float)sandboxDescriptor.DataSize.x / (float)sandboxDescriptor.DataSize.y;
            if (metaballRT != null)
            {
                metaballRT.DiscardContents();
                metaballRT.Release();
            }

            metaballRT = new RenderTexture((int)(256.0f * aspectRatio), 256, 0);
        }

        public void DropWater(Vector3 position)
        {
            if (!Physics.CheckSphere(position + new Vector3(0, 0, -5), 1.0f))
            {
                WaterDroplet waterDroplet = Instantiate(WaterDroplet, position, Quaternion.identity);
                waterDroplet.SetShowMesh(showParticles);
                waterDroplets.Add(waterDroplet);
            }
        }

        /// <summary>
        /// Creates a HandInputGesture from detected hand position and adds it to HandInput
        /// </summary>
        private void AddHandToHandInput(Vector3 worldPosition)
        {
            if (!enableHandInputIntegration || HandInput == null)
                return;

            // Check if a gesture with this position already exists (avoid duplicates)
            List<HandInputGesture> currentGestures = HandInput.GetCurrentGestures();
            bool gestureExists = currentGestures.Any(gesture => 
                Vector3.Distance(gesture.WorldPosition, worldPosition) < minHandMovement);

            if (gestureExists)
                return;

            // Create new gesture
            Vector2 normalisedPosition = Sandbox.WorldPosToNormalisedPos(worldPosition);
            Point dataPosition = Sandbox.WorldPosToDataPos(worldPosition);
            float depth = worldPosition.z / Sandbox.MESH_Z_SCALE;
            bool outOfBounds = !IsHandWithinBounds(worldPosition);

            HandInputGesture newGesture = new HandInputGesture(
                nextGestureID++, 
                worldPosition, 
                normalisedPosition, 
                depth, 
                dataPosition, 
                outOfBounds, 
                false // Not a UI gesture
            );

            // Add to HandInput's CurrentGestures list
            // We need to access the private CurrentGestures list, so we'll use reflection or add a public method
            AddGestureToHandInput(newGesture);
        }

        /// <summary>
        /// Adds a gesture to HandInput's CurrentGestures list
        /// </summary>
        private void AddGestureToHandInput(HandInputGesture gesture)
        {
            if (HandInput != null)
            {
                HandInput.AddDetectedHandGesture(gesture);
            }
        }

        /// <summary>
        /// Check if hand position is within sandbox bounds
        /// </summary>
        private bool IsHandWithinBounds(Vector3 handPosition)
        {
            if (sandboxDescriptor == null)
                return false;

            // Check if hand is within sandbox area
            bool withinX = handPosition.x >= sandboxDescriptor.MeshStart.x && 
                          handPosition.x <= sandboxDescriptor.MeshEnd.x;
            bool withinY = handPosition.y >= sandboxDescriptor.MeshStart.y && 
                          handPosition.y <= sandboxDescriptor.MeshEnd.y;
            
            // Check if hand is within reasonable depth range
            bool withinDepth = handPosition.z >= 0 && handPosition.z <= 2000; // 0-2m range

            return withinX && withinY && withinDepth;
        }

        /// <summary>
        /// Clean up old detected hand gestures from HandInput
        /// </summary>
        private void CleanupOldGestures()
        {
            if (!enableHandInputIntegration || HandInput == null)
                return;

            // Only cleanup at specified intervals
            if (Time.time - lastCleanupTime < gestureCleanupInterval)
                return;

            lastCleanupTime = Time.time;

            List<HandInputGesture> currentGestures = HandInput.GetCurrentGestures();
            List<int> gesturesToRemove = new List<int>();

            // Find old detected hand gestures to remove
            foreach (HandInputGesture gesture in currentGestures)
            {
                // Only cleanup detected hand gestures (not UI gestures)
                if (!gesture.IsUIGesture && gesture.GestureID >= 1000)
                {
                    // Check if gesture is too old
                    float gestureAge = Time.time - (gesture.Age * 0.033f); // Approximate age in seconds
                    if (gestureAge > gestureMaxAge)
                    {
                        gesturesToRemove.Add(gesture.GestureID);
                    }
                }
            }

            // Remove old gestures
            foreach (int gestureID in gesturesToRemove)
            {
                HandInput.RemoveGesture(gestureID);
                Debug.Log($"Cleaned up old detected hand gesture with ID {gestureID}");
            }
        }
        private void OnGesturesReady()
        {
            foreach (HandInputGesture gesture in HandInput.GetCurrentGestures())
            {
                if (!gesture.OutOfBounds)
                {
                    // Check if frame is frozen - store gesture instead of dropping water immediately
                    if (FreezeFrame.isFrameFrozen)
                    {
                        //Sandbox.StoreGesture(gesture.WorldPosition);
                    }
                    else
                    {
                        // Normal water drop when not frozen
                        DropWater(gesture.WorldPosition);
                    }
                }
            }
        }

        private void DestroyWaterDroplets()
        {
            foreach (WaterDroplet droplet in waterDroplets)
            {
                Destroy(droplet);
            }
            waterDroplets.Clear();
        }

        public void UI_DestroyWaterDroplets()
        {
            DestroyWaterDroplets();
        }

        public void UI_ToggleShowParticles(bool showParticles)
        {
            this.showParticles = showParticles;
            Debug.Log(showParticles);
            foreach (WaterDroplet droplet in waterDroplets)
            {
                droplet.SetShowMesh(showParticles);
            }
        }

        public void UI_ToggleActivateWaterAbsorption(bool activateWaterAbsorption)
        {
            this._soilAbsorptionActive = activateWaterAbsorption;
            if (activateWaterAbsorption)
            {
                StartCoroutine(ReduceWater());
            }

        }
        public void UI_SetWaterAbsorptionSpeed(float waterSpeedMultiplier)
        {
            WaterAbsorptionSpeed = 1/waterSpeedMultiplier;
        }
        


        
        
        private Vector3 GetHandCurrentWorldPosition()
        {
            // Get depth points within threshold range
            var validPoints = Sandbox.intDepthData
                .Select((depth, index) => new { Depth = depth, Index = index })
                .Where(p => p.Depth > HandTresholdMin && p.Depth < HandTresholdMax)
                .ToArray();

            if (validPoints.Length == 0) return Vector3.zero;

            // Calculate average position
            var avgX = validPoints.Average(p => p.Index % 231);
            var avgY = validPoints.Average(p => p.Index / 231);
            var currentPosition = Sandbox.DataPosToWorldPos(new Point((int)avgX, (int)avgY));
            currentPosition.z = HandTresholdMin;
            
            return currentPosition;
        }
        
        private Vector3 AveragePoint(Tuple<int, int>[] tuple)
        {
                Vector3[] handDetectionVectors = new Vector3[tuple.Length];
                int index = 0;
                foreach (Tuple<int, int> t in tuple)
                {
                    handDetectionVectors[index] = new Vector3(t.Item2 % 231, t.Item2 / 231, t.Item1);
                    index++;
                }

                float sumX = 0, sumY = 0, sumZ = 0;
                int totalPixels = 0;

                foreach (Vector3 vect in handDetectionVectors)
                {
                    sumX += vect.x;
                    sumY += vect.y;
                    sumZ += vect.z;
                    totalPixels++;

                }

                int avgX = (int)sumX / totalPixels;
                int avgY = (int)sumY / totalPixels;
                float avgZ = sumZ / totalPixels;
                Vector3 vector3 = Sandbox.DataPosToWorldPos(new Point(avgX, avgY));
                vector3.z = 1000;
                
                return vector3;
        }

        IEnumerator GetHandPositionCoroutine()
        {
            while (true)
            {
                // Clean up old gestures periodically
                //CleanupOldGestures();

                Vector3 currentHandPosition = GetHandCurrentWorldPosition();
                Vector2 currentHandPosition2 = GetHandCurrentWorldPosition();
                if (Vector3.Distance(currentHandPosition, lastHandPosition) < minHandMovement && currentHandPosition!= Vector3.zero)
                {
                    stabilityThreshold--;
                    if (stabilityThreshold < 0)
                    {
                        Debug.Log("Stable hand detected, can drop water");
                        //Vector3 handPosition = listAvgPoint[getPosition-1];
                        int gestureID = nextGestureID;
                        
                        // Add detected hand to HandInput system
                        if (enableHandInputIntegration)
                        {
                            HandInput.OnHandHovered(gestureID, currentHandPosition2);
                            Debug.Log("Gesture ID: " + gestureID);
                        }
                        
                        // Check if frame is frozen - store gesture instead of dropping water immediately
                        if (FreezeFrame.isFrameFrozen)
                        {
                            //Sandbox.StoreGesture(currentHandPosition, gestureID);
                        }
                        else
                        {
                            // Normal water drop when not frozen
                            //DropWater(currentHandPosition);
                        }
                    }  
                }
                if(Vector3.Distance(currentHandPosition, lastHandPosition) > maxHandMovement)
                {
                    CleanupOldGestures();
                    stabilityThreshold = 60;
                }

                lastHandPosition = currentHandPosition ;

                yield return null;
            }
        }
    }
}
