using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Instance Draw
/// </summary>
namespace SRP
{
    public class InstanceDrawer
    {
        public static void CheckAndInit(InstanceData idata)
        {
            if (idata.matrixBuffer != null && idata.validMatrixBuffer != null && idata.argsBuffer != null) return;
            int sizeofMatrix4x4 = 4 * 4 * 4; //float * 4 * 4
            idata.matrixBuffer = new ComputeBuffer(idata.instanceCount, sizeofMatrix4x4);
            idata.validMatrixBuffer = new ComputeBuffer(idata.instanceCount, sizeofMatrix4x4, ComputeBufferType.Append);
            idata.argsBuffer = new ComputeBuffer(5, sizeof(uint), ComputeBufferType.IndirectArguments);

            // 传变换矩阵到 GPU
            idata.matrixBuffer.SetData(idata.matrices);

            uint[] args = new uint[5] { 0, 0, 0, 0, 0 };
            if (idata.instanceMesh != null)
            {
                args[0] = (uint)idata.instanceMesh.GetIndexCount(idata.subMeshIndex);
                args[1] = (uint)0;
                args[2] = (uint)idata.instanceMesh.GetIndexStart(idata.subMeshIndex);
                args[3] = (uint)idata.instanceMesh.GetBaseVertex(idata.subMeshIndex);
            }
            idata.argsBuffer.SetData(args);
        }

        // All-in drawing
        public static void Draw(InstanceData idata)
        {
            if (idata == null) return;
            CheckAndInit(idata);

            uint[] args = new uint[5] { 0, 0, 0, 0, 0 };
            idata.argsBuffer.GetData(args);
            args[1] = (uint)idata.instanceCount;
            idata.argsBuffer.SetData(args);

            idata.instanceMaterial.SetBuffer("_validMatrixBuffer", idata.matrixBuffer);

            Graphics.DrawMeshInstancedIndirect(
                idata.instanceMesh,
                idata.subMeshIndex,
                idata.instanceMaterial,
                new Bounds(Vector3.zero, new Vector3(100.0f, 100.0f, 100.0f)),
                idata.argsBuffer);
        }
        // frustum culling
        public static void Draw(InstanceData idata, Camera camera, ComputeShader cs)
        {
            if (idata == null || camera == null || cs == null) return;
            CheckAndInit(idata);

            // 清空绘制计数
            uint[] args = new uint[5] { 0, 0, 0, 0, 0 };
            idata.argsBuffer.GetData(args);
            args[1] = 0;
            idata.argsBuffer.SetData(args);
            idata.validMatrixBuffer.SetCounterValue(0);

            //计算视椎体平面
            Plane[] ps = GeometryUtility.CalculateFrustumPlanes(camera);
            Vector4[] planes = new Vector4[6];
            for (int i = 0; i < planes.Length; i++)
            {
                // Ax+By+Cz+D --> Vec4(A,B,C,D)
                planes[i] = new Vector4(ps[i].normal.x, ps[i].normal.y, ps[i].normal.z, ps[i].distance);
            }
            //计算包围盒
            Vector4[] bounds = BoundToPoint(idata.instanceMesh.bounds);

            int kernel = cs.FindKernel("InstanceCulling");
            cs.SetVectorArray("_bounds", bounds);
            cs.SetVectorArray("_planes", planes);
            cs.SetInt("_instanceCount", idata.instanceCount);
            cs.SetBuffer(kernel, "_matrixBuffer", idata.matrixBuffer);
            cs.SetBuffer(kernel, "_validMatrixBuffer", idata.validMatrixBuffer);
            cs.SetBuffer(kernel, "_argsBuffer", idata.argsBuffer);

            //剔除
            int Dispatch = (int)Mathf.Ceil((float)idata.instanceCount / 128);
            cs.Dispatch(kernel, Dispatch, 1, 1);

            idata.instanceMaterial.SetBuffer("_validMatrixBuffer", idata.validMatrixBuffer);

            Graphics.DrawMeshInstancedIndirect(idata.instanceMesh, idata.subMeshIndex,
                idata.instanceMaterial, new Bounds(Vector3.zero, new Vector3(100.0f, 100.0f, 100.0f)), idata.argsBuffer);

        }
        public static void Draw(InstanceData idata, Camera camera, ComputeShader cs, Matrix4x4 vpMatrix, 
            RenderTexture hizBuffer, ref CommandBuffer cmd)
        {
            if (idata == null || camera == null || cs == null) return;
            CheckAndInit(idata);

            // 清空绘制计数
            uint[] args = new uint[5] { 0, 0, 0, 0, 0 };
            idata.argsBuffer.GetData(args);
            args[1] = 0;
            idata.argsBuffer.SetData(args);
            idata.validMatrixBuffer.SetCounterValue(0);

            //计算视椎体平面
            Plane[] ps = GeometryUtility.CalculateFrustumPlanes(camera);
            Vector4[] planes = new Vector4[6];
            for (int i = 0; i < planes.Length; i++)
            {
                // Ax+By+Cz+D --> Vec4(A,B,C,D)
                planes[i] = new Vector4(ps[i].normal.x, ps[i].normal.y, ps[i].normal.z, ps[i].distance);
            }
            Vector4[] bounds = BoundToPoint(idata.instanceMesh.bounds);

            // 传送参数到 shader
            int kernel = cs.FindKernel("InstanceCulling");
            cs.SetMatrix("_vpMatrix", vpMatrix);
            cs.SetVectorArray("_bounds", bounds);
            cs.SetVectorArray("_planes", planes);
            cs.SetInt("_size", hizBuffer.width);
            cs.SetInt("_instanceCount", idata.instanceCount);
            cs.SetBuffer(kernel, "_matrixBuffer", idata.matrixBuffer);
            cs.SetBuffer(kernel, "_validMatrixBuffer", idata.validMatrixBuffer);
            cs.SetBuffer(kernel, "_argsBuffer", idata.argsBuffer);
            cs.SetTexture(kernel, "_hizTexture", hizBuffer);
            idata.instanceMaterial.SetBuffer("_validMatrixBuffer", idata.validMatrixBuffer);

            //剔除
            int Dispatch = (int)Mathf.Ceil((float)idata.instanceCount / 128);
            cs.Dispatch(kernel, Dispatch, 1, 1);

            cmd.DrawMeshInstancedIndirect(idata.instanceMesh, idata.subMeshIndex, 
                idata.instanceMaterial, -1, idata.argsBuffer);
        }
        public static Vector4[] BoundToPoint(Bounds b)
        {
            Vector4[] bounds = new Vector4[8];
            bounds[0] = new Vector4(b.min.x, b.min.y, b.min.z, 1);
            bounds[1] = new Vector4(b.max.x, b.max.y, b.max.z, 1);
            bounds[2] = new Vector4(bounds[0].x, bounds[0].y, bounds[1].z, 1);
            bounds[3] = new Vector4(bounds[0].x, bounds[1].y, bounds[0].z, 1);
            bounds[4] = new Vector4(bounds[1].x, bounds[0].y, bounds[0].z, 1);
            bounds[5] = new Vector4(bounds[0].x, bounds[1].y, bounds[1].z, 1);
            bounds[6] = new Vector4(bounds[1].x, bounds[0].y, bounds[1].z, 1);
            bounds[7] = new Vector4(bounds[1].x, bounds[1].y, bounds[0].z, 1);
            return bounds;
        }
    }
}
