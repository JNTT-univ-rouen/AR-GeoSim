//  
//  HandDetector.cs
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
//  MERCHANTABILITY or FITNESS FOR ANY PARTICULAR PURPOSE.  See the
//  GNU General Public License for more details.
// 
//  You should have received a copy of the GNU General Public License
//  along with sensilab-ar-sandbox.  If not, see <https://www.gnu.org/licenses/>.
//

using UnityEngine;
using Windows.Kinect;

namespace ARSandbox
{
    public class HandDetector : MonoBehaviour
    {
        [Header("Detection Settings")]
        public float maxDistance = 1000f;           // maximum distance from Kinect in mm
        public bool enableMask = true;
        
        // References
        private KinectManager kinectManager;
        
        // Processing buffers
        private bool[] foregroundMask;
        
        void Start()
        {
            kinectManager = FindObjectOfType<KinectManager>();
            
            if (kinectManager == null)
            {
                Debug.LogError("HandDetector: KinectManager not found!");
                return;
            }
        }
        
        void Update()
        {
            if (kinectManager == null || !kinectManager.NewDataReady() || !enableMask)
                return;
                
            // Get current depth data and create foreground mask
            ushort[] rawDepthData = kinectManager.GetCurrentData();
            if (rawDepthData != null)
            {
                CreateForegroundMask(rawDepthData);
            }
        }
        
        private void CreateForegroundMask(ushort[] rawDepth)
        {
            if (rawDepth == null)
                return;
                
            FrameDescription frameDesc = kinectManager.GetKinectFrameDescriptor();
            if (frameDesc == null)
                return;
                
            int totalPixels = frameDesc.Width * frameDesc.Height;
            
            // Initialize mask if needed
            if (foregroundMask == null || foregroundMask.Length != totalPixels)
            {
                foregroundMask = new bool[totalPixels];
            }
            
            // Create foreground mask based on distance from Kinect
            // Anything closer than the threshold is considered foreground
            for (int i = 0; i < totalPixels; i++)
            {
                if (i < rawDepth.Length)
                {
                    // Convert depth to millimeters and check if it's close enough
                    float depthInMM = rawDepth[i] * 0.1f; // Kinect depth is in 0.1mm units
                    foregroundMask[i] = depthInMM > 0 && depthInMM < maxDistance;
                }
                else
                {
                    foregroundMask[i] = false;
                }
            }
        }
        
        // Public getter for the foreground mask
        public bool[] GetForegroundMask()
        {
            return foregroundMask;
        }
        
        // Get mask dimensions
        public int GetMaskWidth()
        {
            if (kinectManager != null)
            {
                FrameDescription frameDesc = kinectManager.GetKinectFrameDescriptor();
                return frameDesc != null ? frameDesc.Width : 0;
            }
            return 0;
        }
        
        public int GetMaskHeight()
        {
            if (kinectManager != null)
            {
                FrameDescription frameDesc = kinectManager.GetKinectFrameDescriptor();
                return frameDesc != null ? frameDesc.Height : 0;
            }
            return 0;
        }
    }
}
