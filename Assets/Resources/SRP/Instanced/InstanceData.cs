using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 实例化数据
/// </summary>
namespace SRP
{
    [CreateAssetMenu(menuName = "Rendering/InstanceData")]
    [System.Serializable]
    public class InstanceData : ScriptableObject
    {
        [HideInInspector] public ComputeBuffer matrixBuffer; //全部实例的变换矩阵
        [HideInInspector] public ComputeBuffer validMatrixBuffer; //剔除后实例的变化矩阵
        [HideInInspector] public ComputeBuffer argsBuffer; //绘制参数

        [HideInInspector] public int instanceCount = 0; //实例数目
        [HideInInspector] public int subMeshIndex = 0; //子网索引
        [HideInInspector] public Matrix4x4[] matrices; //变换矩阵

        public Mesh instanceMesh;
        public Material instanceMaterial;

        public Vector3 center = new Vector3(0, 0, 0);
        public int instanceNum = 5000;
        public float distanceMin = 5.0f;
        public float distanceMax = 50.0f;
        public float heightMin = -0.5f;
        public float heightMax = 0.5f;

        //随机数
        public void GenerateInstanceData()
        {
            instanceCount = instanceNum;

            //生成变换矩阵
            matrices = new Matrix4x4[instanceNum];
            for (int i = 0; i < instanceNum; i++)
            {
                float angle = Random.Range(0.0f, Mathf.PI * 2.0f);
                float distance = Mathf.Sqrt(Random.Range(0.0f, 1.0f)) * (distanceMax - distanceMin) + distanceMin;
                float height = Random.Range(heightMin, heightMax);

                Vector3 pos = new Vector3(Mathf.Sin(angle) * distance, height, Mathf.Cos(angle) * distance);
                Vector3 dir = pos - center;

                Quaternion quat = new Quaternion();
                quat.SetLookRotation(dir, new Vector3(0, 1, 0));

                Matrix4x4 matrix = Matrix4x4.Rotate(quat);
                matrix.SetColumn(3, new Vector4(pos.x, pos.y, pos.z, 1));
                matrices[i] = matrix;
            }
            matrixBuffer.Release();
            matrixBuffer = null;

            validMatrixBuffer.Release();
            validMatrixBuffer = null;
 
            argsBuffer.Release();
            argsBuffer = null;

            Debug.Log("Instance Data Generate Success");
        }
    }
}
