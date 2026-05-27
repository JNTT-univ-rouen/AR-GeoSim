using UnityEngine;
using System.IO;
using Windows.Kinect;

namespace ARSandbox
{
    public class ImageDepthLoader : MonoBehaviour
    {
        public Sandbox Sandbox;
        public CalibrationManager CalibrationManager;
        public float luminance = 0.1f;
        public bool Invert = true; // white=high by default; flip if needed

        public void LoadImageAsDepth()
        {
            string absolutePath = "C:/Users/sandbox/Documents/dev/AR-GeoSim - Unity6/Assets/test.png";
            
            if (!File.Exists(absolutePath)) return;

            byte[] bytes = File.ReadAllBytes(absolutePath);
            Texture2D src = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!src.LoadImage(bytes)) return;

            CalibrationDescriptor cal = CalibrationManager.GetCalibrationDescriptor();
            Point size = cal.DataSize;
            int w = size.x;
            int h = size.y;

            float minDepth = cal.MinDepth;
            float maxDepth = cal.MaxDepth;
            float range = maxDepth - minDepth;

            byte[] rawR16 = new byte[w * h * 2];

            for (int y = 0; y < h; y++)
            {
                float v = (y + 0.5f) / h;
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w;
                    Color c = src.GetPixelBilinear(u, v);

                    // luminance 0..1
                    float luma = c.grayscale-luminance;
                    if (Invert) luma = 1f - luma;

                    ushort depth = (ushort)Mathf.Clamp(
                        minDepth + luma * range,
                        minDepth,
                        maxDepth
                    );

                    int i = (y * w + x) * 2;
                    rawR16[i] = (byte)(depth & 0xFF);
                    rawR16[i + 1] = (byte)(depth >> 8);
                }
            }

            Texture2D depthTex = new Texture2D(w, h, TextureFormat.R16, false, true);
            depthTex.LoadRawTextureData(rawR16);
            depthTex.Apply();

            Sandbox.SetForcedHeightTexture(depthTex);
            Sandbox.SetForcedHeightEnabled(true);
        }

        public void UseLiveDepthAgain()
        {
            Sandbox.SetForcedHeightEnabled(false);
        }
    }
}