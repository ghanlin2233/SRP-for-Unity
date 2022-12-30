using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
///
/// </summary>
namespace SRP
{
    public class ClusterLight
    {
        public ComputeBuffer clusterBuffer;//cluster 列表
        public ComputeBuffer lightAssignBuffer;//光源分配列表
        public ComputeBuffer lightBuffer;//光源列表
        public ComputeBuffer assignTable;//光源分配索引表

        ComputeShader clusterGenerateCS;
        ComputeShader lightAssignCS;

        //public static int m_ClusterGridBlockSize = 16;
        private static int numClusterX;
        private static int numClusterY;
        private static int numClusterZ;
        private static int numClusters;

        public static int maxNumLights = 1024;
        public static int maxNumLightsPerCluster = 100;

        static int SIZE_OF_LIGHT = 32; // (3 + 3 + 1 + 1) * 4
        struct PointLight
        {
            public Vector3 position;
            public float intensity;
            public Vector3 color;
            public float radius;
        };

        static int SIZE_OF_CLUSTETBOX = 8 * 3 * 4;
        struct ClusterAABB
        {
            public Vector3 p0, p1, p2, p3, p4, p5, p6, p7;
        };

        static int SIZE_OF_LIGHTINX = sizeof(int) * 2;
        struct LightIndex
        {
            public int start;
            public int count;
        };

        public ClusterLight()
        {
            numClusterX = 32;
            numClusterY = 32;
            numClusterZ = 64;

            numClusters = numClusterX * numClusterY * numClusterZ;

            lightBuffer = new ComputeBuffer(maxNumLights, SIZE_OF_LIGHT);
            clusterBuffer = new ComputeBuffer(numClusters, SIZE_OF_CLUSTETBOX);
            lightAssignBuffer = new ComputeBuffer(numClusters * maxNumLightsPerCluster, sizeof(uint));
            assignTable = new ComputeBuffer(numClusters, SIZE_OF_LIGHTINX);

            clusterGenerateCS = Resources.Load<ComputeShader>("CS/ClusterGenerate");
            lightAssignCS = Resources.Load<ComputeShader>("CS/LightAssign");
        }
        ~ClusterLight()
        {
            lightBuffer.Release();
            clusterBuffer.Release();
            assignTable.Release();
            lightAssignBuffer.Release();
        }
        //根据相机参数生成Clusters
        public void CalculateCluster(Camera cam)
        {
            var viewMatrix = cam.worldToCameraMatrix;
            var viewMatrixInv = viewMatrix.inverse;
            var projMatrix = GL.GetGPUProjectionMatrix(cam.projectionMatrix, false);
            var vpMatrix = projMatrix * viewMatrix;
            var vpMatrixInv = vpMatrix.inverse;

            var kernel = clusterGenerateCS.FindKernel("ClusterGenerate");
            clusterGenerateCS.SetMatrix("_vpMatrixInv", vpMatrixInv);
            clusterGenerateCS.SetFloat("_numClusterX", numClusterX);
            clusterGenerateCS.SetFloat("_numClusterY", numClusterY);
            clusterGenerateCS.SetFloat("_numClusterZ", numClusterZ);


            clusterGenerateCS.SetBuffer(kernel, "_clusterBuffer", clusterBuffer);
            clusterGenerateCS.Dispatch(kernel, numClusterZ, 1, 1);

        }
        //更新光源
        public void UpdataLightBuffer(VisibleLight[] lights)
        {
            PointLight[] pointLight = new PointLight[maxNumLights];//
            int index = 0;
            foreach (var light in lights)
            {
                if (light.light.type != LightType.Point) continue;

                PointLight pl;

                pl.position = light.light.transform.position;
                pl.color = new Vector3(light.light.color.r, light.light.color.g, light.light.color.b);
                pl.radius = light.light.range;
                pl.intensity = light.light.intensity;

                pointLight[index++] = pl;
            }
            lightBuffer.SetData(pointLight);
            //传递光源数量
            lightAssignCS.SetInt("_numLights", index);
        }
        //debug
        public void UpdataLightBuffer(Light[] lights)
        {
            PointLight[] plights = new PointLight[maxNumLights];
            int cnt = 0;

            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i].type != LightType.Point) continue;

                PointLight pl;
                pl.color = new Vector3(lights[i].color.r, lights[i].color.g, lights[i].color.b);
                pl.intensity = lights[i].intensity;
                pl.position = lights[i].transform.position;
                pl.radius = lights[i].range;

                plights[cnt++] = pl;
            }
            lightBuffer.SetData(plights);

            // 传递光源数量
            lightAssignCS.SetInt("_numLights", cnt);
        }
        //分配光源
        public void LightAssign()
        {
            lightAssignCS.SetInt("_maxNumLightsPerCluster", maxNumLightsPerCluster);
            lightAssignCS.SetFloat("_numClusterX", numClusterX);
            lightAssignCS.SetFloat("_numClusterY", numClusterY);
            lightAssignCS.SetFloat("_numClusterZ", numClusterZ);

            var kernel = lightAssignCS.FindKernel("LightAssign");
            lightAssignCS.SetBuffer(kernel, "_clusterBuffer", clusterBuffer);
            lightAssignCS.SetBuffer(kernel, "_lightBuffer", lightBuffer);
            lightAssignCS.SetBuffer(kernel, "_lightAssignBuffer", lightAssignBuffer);
            lightAssignCS.SetBuffer(kernel, "_assignTable", assignTable);

            lightAssignCS.Dispatch(kernel, numClusterZ, 1, 1);
        }

        //向shader传送数据
        public void SetShaderParameters()
        {
            Shader.SetGlobalFloat(ShaderProperties._numClusterX, numClusterX);
            Shader.SetGlobalFloat(ShaderProperties._numClusterY, numClusterY);
            Shader.SetGlobalFloat(ShaderProperties._numClusterZ, numClusterZ);

            Shader.SetGlobalBuffer(ShaderProperties._lightBuffer, lightBuffer);
            Shader.SetGlobalBuffer(ShaderProperties._assignTable, assignTable);
            Shader.SetGlobalBuffer(ShaderProperties._lightAssignBuffer, lightAssignBuffer);
        }

        void DrawBox(ClusterAABB box, Color color)
        {
            Debug.DrawLine(box.p0, box.p1, color);
            Debug.DrawLine(box.p0, box.p2, color);
            Debug.DrawLine(box.p0, box.p4, color);

            Debug.DrawLine(box.p6, box.p2, color);
            Debug.DrawLine(box.p6, box.p7, color);
            Debug.DrawLine(box.p6, box.p4, color);

            Debug.DrawLine(box.p5, box.p1, color);
            Debug.DrawLine(box.p5, box.p7, color);
            Debug.DrawLine(box.p5, box.p4, color);

            Debug.DrawLine(box.p3, box.p1, color);
            Debug.DrawLine(box.p3, box.p2, color);
            Debug.DrawLine(box.p3, box.p7, color);
        }
        public void DebugCluster()
        {
            ClusterAABB[] boxes = new ClusterAABB[numClusters];
            clusterBuffer.GetData(boxes, 0, 0, numClusters);

            foreach (var box in boxes)
                DrawBox(box, Color.white);
        }
        public void DebugLightAssign()
        {
            //int numClusters = numClusterX * numClusterY * numClusterZ;

            ClusterAABB[] boxes = new ClusterAABB[numClusters];
            clusterBuffer.GetData(boxes, 0, 0, numClusters);

            LightIndex[] indices = new LightIndex[numClusters];
            assignTable.GetData(indices, 0, 0, numClusters);

            uint[] assignBuf = new uint[numClusters * maxNumLightsPerCluster];
            lightAssignBuffer.GetData(assignBuf, 0, 0, numClusters * maxNumLightsPerCluster);

            Color[] colors = { Color.red, Color.green, Color.blue, Color.yellow };

            for (int i = 0; i < indices.Length; i++)
            {
                if (indices[i].count > 0)
                {
                    uint firstLightId = assignBuf[indices[i].start];
                    DrawBox(boxes[i], colors[firstLightId % 4]);
                    //Debug.Log(assignBuf[indices[i].start]);   // log light id
                }

            }
        }
    }
}
