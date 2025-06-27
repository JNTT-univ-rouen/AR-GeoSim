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
using UnityEditor.ShaderKeywordFilter;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace ARSandbox.WaterSimulation
{
    public class WaterSimulation : MonoBehaviour
    {
        public Sandbox Sandbox;
        public HandInput HandInput;
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

        public int HandTresholdMin{ get;  set; }
        public int HandTresholdMax{ get;  set; }
        private int[] handDepthData ;
        
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
                HandTresholdMin = 800;
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
                
                yield return new WaitForSeconds(1*WaterAbsorptionSpeed/ 60.0f);
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

        private void Update()
        {
            //if(Sandbox.SandboxReady)CheckTreshold();
            //if(Sandbox.SandboxReady)HandWaterActivation();
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

            StartCoroutine(GetHandPositionCoroutine());

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

        private void DropWater(Vector3 position)
        {
            if (!Physics.CheckSphere(position + new Vector3(0, 0, -5), 1.0f))
            {
                WaterDroplet waterDroplet = Instantiate(WaterDroplet, position, Quaternion.identity);
                waterDroplet.SetShowMesh(showParticles);
                waterDroplets.Add(waterDroplet);
            }
        }
        private void OnGesturesReady()
        {
            foreach (HandInputGesture gesture in HandInput.GetCurrentGestures())
            {
                if (!gesture.OutOfBounds)
                {
                    DropWater(gesture.WorldPosition);
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
        
        private void HandWaterActivation()
        {

            float sumX = 0, sumY = 0, Z = 0;
            float totalPixels = 0;
            
            foreach (Vector3 vect in Sandbox.collMeshVertices.Where(mesh => mesh.z < 360 & mesh.z > 5).OrderBy(mesh => mesh.z).ToArray())
            {
                sumX += vect.x;
                sumY += vect.y;
                Z+=vect.z;
                totalPixels++;
                

            }
            float avgX = sumX / totalPixels;
            float avgY = sumY / totalPixels;
            Vector3 vector3 = new Vector3(avgX, avgY, Z);
            
            /*if(totalPixels>1)GameObject.CreatePrimitive(PrimitiveType.Sphere).transform.position = new Vector3(avgX, avgY, 360);*/
            if (totalPixels > 1)
            {
                if (!Physics.CheckSphere(vector3 + new Vector3(0, 0, -5), 1.0f))
                {
                    WaterDroplet waterDroplet = Instantiate(WaterDroplet, vector3, Quaternion.identity);
                    waterDroplet.SetShowMesh(showParticles);
                    waterDroplets.Add(waterDroplet);
                }
            }
        }

        private void CheckTreshold()
        {
            // division euclidienne par 231
            // X = le Reste, Y = le quotient
            //Exemple: 35574 (totalDataPoints) = 154(Y) *231 + 0 (X)
            
            //On verifie si il existe des point dans la zone du treshold choisi
            Tuple<int,int>[] tuple = new Tuple<int,int>[Sandbox.intDepthData.Where(depth => depth < HandTresholdMax & depth > HandTresholdMin).ToArray().Length];
            tuple = Sandbox.intDepthData.Select((depth, index) => new Tuple<int,int>(depth, index)).ToArray();

            tuple = tuple.Where(tupleTuple => tupleTuple.Item1 > HandTresholdMin & tupleTuple.Item1 < HandTresholdMax).ToArray();
            

            //int checkAvg = 0;
            float dist = 0f;
            //Si on trouve des points, on fait la moyenne des position de ses points pour trouver une position centralisé
            /*if (tuple.Length != 0)
            {
                int index = 0;
                foreach (Tuple<int, int> t in tuple)
                {
                    handDectectionVector[index] = new Vector3( t.Item2 %231,t.Item2/231,t.Item1);
                    index++;
                }
                float sumX = 0, sumY = 0, sumZ = 0;
                int totalPixels = 0;
            
                foreach (Vector3 vect in handDectectionVector)
                {
                        sumX += vect.x;
                        sumY += vect.y;
                        sumZ+=vect.z;
                        totalPixels++;

                }
                int avgX = (int)sumX / totalPixels;
                int avgY = (int)sumY / totalPixels;
                float avgZ = sumZ / totalPixels;
                Vector3 vector3 = Sandbox.DataPosToWorldPos(new Point(avgX, avgY));
                vector3.z = avgZ;*/
                if (tuple.Length != 0)
                {
                    Vector3[] listAvgPoint = new Vector3[5];
                    
                    //StartCoroutine(AveragePoint(tuple, listAvgPoint, checkAvg));
                    /*while (checkAvg < 5)
                    {
                        listAvgPoint[checkAvg] = AveragePoint(tuple,listAvgPoint);
                        checkAvg++;
                    }*/
                    StartCoroutine(GetHandPositionCoroutine());
                    if (listAvgPoint.Length==5)for (int i = 1; i < 5; i++)
                    {
                        dist =+ Vector3.Distance(listAvgPoint[i - 1], listAvgPoint[i]);
                        
                    }
                }

                //Si on a recupéré a proximativement la même position sur plusieurs frame on active l'eau
                if (dist > 1 & dist < 5)
                {
                     //DropWater();
                     Debug.Log(dist);
                }
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
                Vector3[] listAvgPoint = new Vector3[3];
                float dist = 0f;
                int getPosition = 0;
                Tuple<int, int>[] tuple = new Tuple<int, int>[Sandbox.intDepthData
                    .Where(depth => depth < HandTresholdMax & depth > HandTresholdMin).ToArray().Length];

                tuple = Sandbox.intDepthData.Select((depth, index) => new Tuple<int, int>(depth, index)).ToArray();
                tuple = tuple.Where(tupleTuple =>
                    tupleTuple.Item1 > HandTresholdMin & tupleTuple.Item1 < HandTresholdMax).ToArray();


                if (tuple.Length != 0)
                {
                    while (getPosition < 3)
                    {
                        tuple = Sandbox.intDepthData.Select((depth, index) => new Tuple<int, int>(depth, index)).ToArray();
                        tuple = tuple.Where(tupleTuple =>
                            tupleTuple.Item1 > HandTresholdMin & tupleTuple.Item1 < HandTresholdMax).ToArray();
                        if (tuple.Length != 0)
                        {
                            listAvgPoint[getPosition] = AveragePoint(tuple);
                        }
                        getPosition++;
                        yield return new WaitForSeconds(1/20.0f);
                    }
                    //Debug.Log(listAvgPoint[0]+ " " +listAvgPoint[1] + " " +listAvgPoint[2] );
                    
                    if (listAvgPoint.Length == getPosition)
                        for (int i = 1; i < getPosition; i++)
                        {
                            dist = +Vector3.Distance(listAvgPoint[i - 1], listAvgPoint[i]);

                        }
                }
                
                if (dist >0 )
                {
                    DropWater(listAvgPoint[getPosition-1]);
                }
                yield return new WaitForSeconds(1/60.0f);
            }
        }
    }
}
