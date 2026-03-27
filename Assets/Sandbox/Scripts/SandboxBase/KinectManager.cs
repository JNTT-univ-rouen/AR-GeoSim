//  
//  KinectManager.cs
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

using UnityEngine;
using System.Collections;
using System.IO;
using Windows.Kinect;

namespace ARSandbox
{
    public class KinectManager : MonoBehaviour
    {
        public bool UseSavedData;
        public TextAsset SavedData;

        public delegate void OnDataStarted_Delegate();
        public static event OnDataStarted_Delegate OnDataStarted;

        private FrameDescription kinectFrameDesc;
        private KinectSensor kinectSensor;
        private DepthFrameReader depthFrameReader;
        private ColorFrameReader colorFrameReader;
        private InfraredFrameReader infraredFrameReader;
        private ushort[] depthData;
        private bool dataReady = false;
        private bool newData = false;
        
        //Infrared frame data
        private FrameDescription infraredFrameDesc;
        private ushort[] infraredData;
        private Texture2D infraredTexture;

        // Color frame data
        private FrameDescription colorFrameDesc;
        private byte[] colorData;           // BGRA32 byte buffer
        private bool newColorData = false;
        private Texture2D colorTexture;     // Convenience texture for saving / previewing

        void Start()
        {
            if (GetFrameDescriptor())
            {
                if (UseSavedData)
                {
                    LoadDepthData();
                    StartCoroutine(Emulate30Hz());
                }
                else
                {
                    SetUpKinectBuffer();
                }
            }
        }

        void Update()
        {
            if (!UseSavedData)
            {
                if (depthFrameReader != null)
                {
                    DepthFrame frame = depthFrameReader.AcquireLatestFrame();
                    if (frame != null)
                    {
                        if (!dataReady)
                        {
                            dataReady = true;
                            if (OnDataStarted != null) OnDataStarted();
                        }
                        frame.CopyFrameDataToArray(depthData);
                        newData = true;
                        frame.Dispose();
                        frame = null;
                    }
                }

                if (infraredFrameReader != null)
                {
                    InfraredFrame infraredFrame = infraredFrameReader.AcquireLatestFrame();
                    if (infraredFrame != null)
                    {
                        infraredFrame.CopyFrameDataToArray(infraredData);
                        infraredFrame.Dispose();
                        infraredFrame = null;
                    }
                }

                // Also grab the latest color frame each Update (for freeze-frame capture)
                if (colorFrameReader != null)
                {
                    ColorFrame colorFrame = colorFrameReader.AcquireLatestFrame();
                    if (colorFrame != null)
                    {
                        if (colorFrameDesc == null)
                        {
                            colorFrameDesc = colorFrame.FrameDescription;
                        }

                        if (colorData == null)
                        {
                            int length = colorFrameDesc.Width * colorFrameDesc.Height * 4; // BGRA32
                            colorData = new byte[length];
                        }

                        colorFrame.CopyConvertedFrameDataToArray(colorData, ColorImageFormat.Bgra);
                        newColorData = true;

                        if (colorTexture == null)
                        {
                            colorTexture = new Texture2D(colorFrameDesc.Width, colorFrameDesc.Height, TextureFormat.BGRA32, false);
                        }

                        //colorTexture.LoadRawTextureData(colorData);
                        //colorTexture.Apply();

                        colorFrame.Dispose();
                        colorFrame = null;
                    }
                }

                if (Input.GetKeyUp(KeyCode.S))
                {
                    //SaveDepthData();
                }
            }
        }

        void OnApplicationQuit()
        {
            if (!UseSavedData)
            {
                if (depthFrameReader != null)
                {
                    depthFrameReader.Dispose();
                    depthFrameReader = null;
                }

                if (kinectSensor != null)
                {
                    if (kinectSensor.IsOpen)
                    {
                        kinectSensor.Close();
                    }

                    kinectSensor = null;
                }
            }
        }
        private IEnumerator Emulate30Hz()
        {
            while (true)
            {
                newData = true;
                yield return new WaitForSeconds(1 / 30.0f);

                if (!dataReady)
                {
                    dataReady = true;
                    if (OnDataStarted != null) OnDataStarted();
                }
            }
        }
        public FrameDescription GetKinectFrameDescriptor()
        {
            return kinectFrameDesc;
        }
        public Point GetKinectFrameSize()
        {
            return new Point(kinectFrameDesc.Width, kinectFrameDesc.Height);
        }
        public ushort[] GetCurrentData()
        {
            newData = false;
            return depthData;
        }

        public ushort[] GetCurrentInfraredData()
        {
            newData = false;
            return infraredData;
        }

        // --- Color helpers for freeze-frame capture ---

        public bool NewColorDataReady()
        {
            return newColorData;
        }

        // Returns the latest color Texture2D (BGRA32), or null if none yet.
        public Texture2D GetCurrentColorTexture()
        {
            newColorData = false;
            return colorTexture;
        }

        public bool StreamStarted()
        {
            if (UseSavedData)
                return true;

            return dataReady;
        }

        public bool NewDataReady()
        {
            return newData;
        }

        private bool GetFrameDescriptor()
        {
            kinectSensor = KinectSensor.GetDefault();
            if (kinectSensor != null)
            {
                kinectFrameDesc = kinectSensor.DepthFrameSource.FrameDescription;
                return true;
            }
            else
            {
                print("Error: KinectSensor not found. Make sure Kinect has been installed correctly");
                return false;
            }
        }

        private void SetUpKinectBuffer()
        {
            if (kinectSensor != null)
            {
                if (!kinectSensor.IsOpen)
                {
                    kinectSensor.Open();
                }

                depthFrameReader = kinectSensor.DepthFrameSource.OpenReader();
                infraredFrameReader = kinectSensor.InfraredFrameSource.OpenReader();
                depthData = new ushort[kinectSensor.DepthFrameSource.FrameDescription.LengthInPixels];
                infraredData = new ushort[kinectSensor.InfraredFrameSource.FrameDescription.LengthInPixels];

                // Set up color reader for per-frame color data
                colorFrameReader = kinectSensor.ColorFrameSource.OpenReader();
            }
        }

        private void LoadDepthData()
        {
            using (Stream s = new MemoryStream(SavedData.bytes))
            {
                using (BinaryReader br = new BinaryReader(s))
                {
                    int length = br.ReadInt32();
                    depthData = new ushort[length];
                    for (int i = 0; i < length; i++)
                    {
                        depthData[i] = br.ReadUInt16();
                    }
                }
            }
        }
        private void SaveDepthData()
        {
            using (FileStream fs = new FileStream(Application.dataPath + "/Depth.txt", FileMode.OpenOrCreate, FileAccess.Write))
            {
                using (BinaryWriter bw = new BinaryWriter(fs))
                {
                    bw.Write(depthData.Length);
                    foreach (ushort value in depthData)
                    {
                        bw.Write(value);
                    }
                }
            }
        }
    }
}
