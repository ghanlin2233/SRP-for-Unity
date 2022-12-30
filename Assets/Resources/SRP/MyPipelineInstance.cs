using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Profiling;

namespace SRP
{
    public class MyPipelineInstance : RenderPipeline
    {
        RenderTexture gDepth;   //深度附件
        RenderTexture[] gBuffers = new RenderTexture[4];  //颜色附件
        RenderTargetIdentifier gDepthID;
        RenderTargetIdentifier[] gBuffersID = new RenderTargetIdentifier[4];
        RenderTexture lightPassTex;
        RenderTexture hizTexture;

        Matrix4x4 vpMatrix;
        Matrix4x4 vpMatrixInv;
        Matrix4x4 vpMatrixPrev;     // 上一帧的 vp 矩阵
        Matrix4x4 vpMatrixInvPrev;

        // IBL 贴图
        public Cubemap diffuseIBL;
        public Cubemap specularIBL;
        public Texture brdfLut;

        // 噪声图
        public Texture blueNoiseTex;

        //阴影管理
        public float searchRadius;
        public float filterRadius;

        public int shadowMapResolution = 2048;
        public float orthoDistance = 500.0f;
        public float lightSize = 2.0f;

        RenderTexture[] shadowTextures = new RenderTexture[4];
        RenderTexture shadowStrength;
        RenderTexture shadowMask;
        

        CSM csm;
        public CSMSettings csmSettings;

        //光照管理
        ClusterLight _clusterLight;

        public InstanceData[] instanceDatas;


        public MyPipelineInstance()
        {
            //创建纹理
            gDepth = new RenderTexture(Screen.width, Screen.height, 24, RenderTextureFormat.Depth, RenderTextureReadWrite.Linear);
            gBuffers[0] = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            gBuffers[1] = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB2101010, RenderTextureReadWrite.Linear);
            gBuffers[2] = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB64, RenderTextureReadWrite.Linear);
            gBuffers[3] = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            //lightPassTex = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);

            //hiz Texture
            int size = Mathf.NextPowerOfTwo(Mathf.Max(Screen.width, Screen.height));
            hizTexture = new RenderTexture(size, size, 0, RenderTextureFormat.RHalf);
            hizTexture.autoGenerateMips = false;
            hizTexture.useMipMap = true;
            hizTexture.filterMode = FilterMode.Point;


            //创建阴影贴图
            csm = new CSM();
            for (int i = 0; i < shadowTextures.Length; i++)
                shadowTextures[i] = new RenderTexture(shadowMapResolution, shadowMapResolution, 24, RenderTextureFormat.Depth, RenderTextureReadWrite.Linear);
            shadowStrength = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.R8, RenderTextureReadWrite.Linear);
            shadowMask = new RenderTexture(Screen.width / 4, Screen.height / 4, 0, RenderTextureFormat.R8, RenderTextureReadWrite.Linear);

            //给纹理赋值
            gDepthID = gDepth;
            for (int i = 0; i < 4; i++) gBuffersID[i] = gBuffers[i];

            //光照管理
            _clusterLight = new ClusterLight();

        }

        protected override void Render(ScriptableRenderContext context, Camera[] cameras)
        {
            foreach (var camera in cameras)
            {

                SetGlobalBuffer(camera);

                ClusterLightPass(context, camera);

                CSMPass(context, camera);

                GbufferPass(context, camera);

                InstanceDrawPass(context, Camera.main);

                if (!Handles.ShouldRenderGizmos())
                {
                    //HizPass(context, camera);
                    vpMatrixPrev = vpMatrix;
                }


                ShadowMappingPass(context, camera);

                LightPass(context, camera);

                SkyBoxPass(context, camera);
            }
        }
        public void SetGlobalBuffer(Camera cam)
        {
            Matrix4x4 viewMatrix = cam.worldToCameraMatrix;
            Matrix4x4 projMatrix = GL.GetGPUProjectionMatrix(cam.projectionMatrix, false);
            vpMatrix = projMatrix * viewMatrix;
            vpMatrixInv = vpMatrix.inverse;
            // 设置相机矩阵
            Shader.SetGlobalMatrix(ShaderProperties._vpMatrix, vpMatrix);
            Shader.SetGlobalMatrix(ShaderProperties._vpMatrixInv, vpMatrixInv);
            Shader.SetGlobalMatrix(ShaderProperties._vpMatrixPrev, vpMatrixPrev);
            Shader.SetGlobalMatrix(ShaderProperties._vpMatrixInvPrev, vpMatrixInvPrev);

            //阴影设置
            Shader.SetGlobalFloat(ShaderProperties._far, cam.farClipPlane);
            Shader.SetGlobalFloat(ShaderProperties._near, cam.nearClipPlane);
            Shader.SetGlobalFloat(ShaderProperties._screenWidth, Screen.width);
            Shader.SetGlobalFloat(ShaderProperties._screenHeight, Screen.height);
            Shader.SetGlobalTexture(ShaderProperties._noiseTex, blueNoiseTex);
            Shader.SetGlobalFloat(ShaderProperties._noiseTexResolution, blueNoiseTex.width);
            Shader.SetGlobalFloat(ShaderProperties._searchRadius, searchRadius);
            Shader.SetGlobalFloat(ShaderProperties._filterRadius, filterRadius);

            // 设置 IBL 贴图
            Shader.SetGlobalTexture(ShaderProperties._diffuseIBL, diffuseIBL);
            Shader.SetGlobalTexture(ShaderProperties._specularIBL, specularIBL);
            Shader.SetGlobalTexture(ShaderProperties._brdfLut, brdfLut);

            //Gbuffer
            Shader.SetGlobalTexture(ShaderProperties._gDepth, gDepth);
            for (int i = 0; i < 4; i++) Shader.SetGlobalTexture("_GT" + i, gBuffers[i]);

            //CSM相关参数
            Shader.SetGlobalTexture(ShaderProperties._shadowMask, shadowMask);
            Shader.SetGlobalTexture(ShaderProperties._shadowStrength, shadowStrength);
            Shader.SetGlobalFloat(ShaderProperties._shadowMapResolution, shadowMapResolution);
            for (int i = 0; i < 4; i++)
            {
                Shader.SetGlobalTexture("_shadowtex" + i, shadowTextures[i]);
                Shader.SetGlobalFloat("_split" + i, csm.depthDivide[i]);
            }
        }

        //Cluster Pass
        void ClusterLightPass(ScriptableRenderContext context, Camera camera)
        {       
            //裁剪光源
            camera.TryGetCullingParameters(out var cullingParameters);
            var cullingResults = context.Cull(ref cullingParameters);
            //更新光源
            _clusterLight.UpdataLightBuffer(cullingResults.visibleLights.ToArray());

            //划分Cluster
            _clusterLight.CalculateCluster(camera);

            //分配光源
            _clusterLight.LightAssign();

            //传递参数
            _clusterLight.SetShaderParameters();
        }

        //Gbuffer Pass
        void GbufferPass(ScriptableRenderContext context, Camera camera)
        {
            Profiler.BeginSample("Gbuffer");

            context.SetupCameraProperties(camera);
            CommandBuffer cmd = new CommandBuffer
            {
                name = "Gbuffer"
            };

            //清屏
            cmd.SetRenderTarget(gBuffersID, gDepthID);
            cmd.ClearRenderTarget(true, true, Color.clear);
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();

            // 剔除
            camera.TryGetCullingParameters(out var cullingParameters);
            var cullingResults = context.Cull(ref cullingParameters);

            //// config settings
            ShaderTagId shaderTagId = new ShaderTagId("gbuffer");   // 使用 LightMode 为 gbuffer 的 shader
            SortingSettings sortingSettings = new SortingSettings(camera);
            DrawingSettings drawingSettings = new DrawingSettings(shaderTagId, sortingSettings);
            FilteringSettings filteringSettings = FilteringSettings.defaultValue;

            //绘制
            context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);
            context.Submit();

            Profiler.EndSample();
        }

        //Instance Pass
        void InstanceDrawPass(ScriptableRenderContext context, Camera camera)
        {
            CommandBuffer cmd = new CommandBuffer();
            cmd.name = "instance gbuffer";
            cmd.SetRenderTarget(gBuffersID, gDepth);

            Matrix4x4 viewMatrix = camera.worldToCameraMatrix;
            Matrix4x4 projMatrix = GL.GetGPUProjectionMatrix(camera.projectionMatrix, false);
            Matrix4x4 vp = projMatrix * viewMatrix;

            //绘制
            ComputeShader cullingCs = Resources.Load<ComputeShader>("CS/InstanceCulling");
            for (int i = 0; i < instanceDatas.Length; i++)
            {
                InstanceDrawer.Draw(instanceDatas[i], camera, cullingCs, vpMatrixPrev, hizTexture, ref cmd);
            }
            context.ExecuteCommandBuffer(cmd);
            context.Submit();
        }
        void HizPass(ScriptableRenderContext context, Camera camera)
        {
            CommandBuffer cmd = new CommandBuffer();
            cmd.name = "HizPass";

            int size = hizTexture.width;
            int level = (int)Mathf.Log(size, 2);
            RenderTexture[] mipmap = new RenderTexture[level];
            for (int i = 0; i < level; i++)
            {
                int mipmapSize = size / (int)Mathf.Pow(2, i);
                mipmap[i] = RenderTexture.GetTemporary(mipmapSize, mipmapSize, 0, RenderTextureFormat.RHalf, RenderTextureReadWrite.Linear);
                mipmap[i].filterMode = FilterMode.Point;
            }

            //生成mipmap
            Material mat = new Material(Shader.Find("Deferred/Hiz"));
            cmd.Blit(gDepth, mipmap[0]);
            for (int i = 1; i < level; i++)
            {
                cmd.Blit(mipmap[i - 1], mipmap[i], mat);
            }

            //拷贝
            for (int i = 0; i < level; i++)
            {
                cmd.CopyTexture(mipmap[i], 0, 0, hizTexture, 0, i);
                RenderTexture.ReleaseTemporary(mipmap[i]);
            }
            context.ExecuteCommandBuffer(cmd);
            context.Submit();          
        }

        //Lighting Pass
        void LightPass(ScriptableRenderContext context, Camera camera)
        {
            //使用BLIT
            CommandBuffer cmd = new CommandBuffer();
            cmd.name = "lightPass";

            cmd.Blit(gBuffersID[0], BuiltinRenderTextureType.CameraTarget, new Material(Shader.Find("Deferred/lightPass")));
            context.ExecuteCommandBuffer(cmd);

            context.Submit();
        }

        //shadow pass
        void CSMPass(ScriptableRenderContext context, Camera camera)
        {
            Profiler.BeginSample("MyPieceOfCode");

            //获取光源信息
            Light light = Object.FindObjectOfType<Light>();
            Vector3 lightDir = light.transform.rotation * Vector3.forward;

            //更新shadowMap分割
            csm.Update(camera, lightDir);
            csmSettings.Setup();

            csm.SaveCameraSettings(ref camera);
            for (int level = 0; level < 4; level++)
            {
                //将相机移到光源方向
                csm.ConfigCameraToShadowSpace(ref camera, lightDir, level, orthoDistance, shadowMapResolution);

                // 设置阴影矩阵, 视锥分割参数
                Matrix4x4 v = camera.worldToCameraMatrix;
                Matrix4x4 p = GL.GetGPUProjectionMatrix(camera.projectionMatrix, false);
                Shader.SetGlobalMatrix("_shadowVpMatrix" + level, p * v);
                Shader.SetGlobalFloat("_orthoWidth" + level, csm.orthoWidth[level]);


                //绘制前准备
                CommandBuffer cmd = new CommandBuffer
                {
                    name = "shadowmap" + level
                };
                context.SetupCameraProperties(camera);
                cmd.SetRenderTarget(shadowTextures[level]);
                cmd.ClearRenderTarget(true, true, Color.clear);
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                // 剔除
                camera.TryGetCullingParameters(out var cullingParameters);
                var cullingResults = context.Cull(ref cullingParameters);
                // config settings
                ShaderTagId shaderTagId = new ShaderTagId("depthonly");
                SortingSettings sortingSettings = new SortingSettings(camera);
                DrawingSettings drawingSettings = new DrawingSettings(shaderTagId, sortingSettings);
                FilteringSettings filteringSettings = FilteringSettings.defaultValue;

                // 绘制
                context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);
                context.Submit();   // 每次 set camera 之后立即提交
            }
            csm.RevertCameraSettings(ref camera);
            Profiler.EndSample();
        }
        void ShadowMappingPass(ScriptableRenderContext context, Camera camera)
        {
            //绘制前准备
            CommandBuffer cmd = new CommandBuffer
            {
                name = "shadowMappingPass"
            };

            //RenderTexture tempTex1 = RenderTexture.GetTemporary(Screen.width/4, Screen.height/4, 0, RenderTextureFormat.R8, RenderTextureReadWrite.Linear);
            //RenderTexture tempTex2 = RenderTexture.GetTemporary(Screen.width/4, Screen.height/4, 0, RenderTextureFormat.R8, RenderTextureReadWrite.Linear);
            RenderTexture tempTex3 = RenderTexture.GetTemporary(Screen.width, Screen.height, 0, RenderTextureFormat.RG16, RenderTextureReadWrite.Linear);
            RenderTexture tempTex4 = RenderTexture.GetTemporary(Screen.width, Screen.height, 0, RenderTextureFormat.RG16, RenderTextureReadWrite.Linear);


            // 生成阴影
            //cmd.Blit(gBuffersID[0], tempTex1, new Material(Shader.Find("Deferred/shadowMask")));
            //cmd.Blit(tempTex1, tempTex2, new Material(Shader.Find("Deferred/gaussianBlurN1")));
            //cmd.Blit(tempTex2, shadowMask, new Material(Shader.Find("Deferred/gaussianBlur1N")));

            cmd.Blit(gBuffersID[0], tempTex3, new Material(Shader.Find("Deferred/shadowPass")));
            cmd.Blit(tempTex3, tempTex4, new Material(Shader.Find("Deferred/gaussianBlurN1")));
            cmd.Blit(tempTex4, shadowStrength, new Material(Shader.Find("Deferred/gaussianBlur1N")));

            RenderTexture.ReleaseTemporary(tempTex3);
            RenderTexture.ReleaseTemporary(tempTex4);
            //RenderTexture.ReleaseTemporary(tempTex3);
            //RenderTexture.ReleaseTemporary(tempTex4);

            context.ExecuteCommandBuffer(cmd);
            context.Submit();
        }


        //sky box
        void SkyBoxPass(ScriptableRenderContext context, Camera camera)
        {
            if (camera.clearFlags == CameraClearFlags.Skybox && RenderSettings.skybox != null)
            {
                context.DrawSkybox(camera);
            }

            if (Handles.ShouldRenderGizmos())
            {
                context.DrawGizmos(camera, GizmoSubset.PreImageEffects);
                context.DrawGizmos(camera, GizmoSubset.PostImageEffects);
            }
            context.Submit();
        }
    }
    static public class ShaderProperties
    {
        //*************************Shadow Transform**********************************
        public static readonly int _gDepth = Shader.PropertyToID("_gDepth");
        public static readonly int _screenWidth = Shader.PropertyToID("_screenWidth");
        public static readonly int _screenHeight = Shader.PropertyToID("_screenHeight");
        public static readonly int _shadowVpMatrix = Shader.PropertyToID("_shadowVpMatrix");
        public static readonly int _orthoWidth = Shader.PropertyToID("_orthoWidth");
        public static readonly int _shadowMask = Shader.PropertyToID("_shadowMask");
        public static readonly int _shadowStrength = Shader.PropertyToID("_shadowStrength");
        public static readonly int _shadowMapResolution = Shader.PropertyToID("_shadowMapResolution");

        public static readonly int _searchRadius = Shader.PropertyToID("_searchRadius");
        public static readonly int _filterRadius = Shader.PropertyToID("_filterRadius");
        public static readonly int _noiseTex = Shader.PropertyToID("_noiseTex");
        public static readonly int _noiseTexResolution = Shader.PropertyToID("_noiseTexResolution");

        //*************************Matrix Transfomation*****************************
        public static readonly int _viewMatrix = Shader.PropertyToID("_viewMatrix");
        public static readonly int _viewMatrixInv = Shader.PropertyToID("_viewMatrixInv");
        public static readonly int _vpMatrix = Shader.PropertyToID("_vpMatrix");
        public static readonly int _vpMatrixInv = Shader.PropertyToID("_vpMatrixInv");
        public static readonly int _vpMatrixPrev = Shader.PropertyToID("_vpMatrixPrev");
        public static readonly int _vpMatrixInvPrev = Shader.PropertyToID("_vpMatrixInvPrev");
        public static readonly int _near = Shader.PropertyToID("_near");
        public static readonly int _far = Shader.PropertyToID("_far");
        public static readonly int _fovh = Shader.PropertyToID("_fovh");


        //***********************Cluster Light*****************************************
        public static readonly int _maxNumLightsPerCluster = Shader.PropertyToID("_maxNumLightsPerCluster");
        public static readonly int _numClusterX = Shader.PropertyToID("_numClusterX");
        public static readonly int _numClusterY = Shader.PropertyToID("_numClusterY");
        public static readonly int _numClusterZ = Shader.PropertyToID("_numClusterZ");
        public static readonly int _numLights = Shader.PropertyToID("_numLights");
        public static readonly int _clusterBuffer = Shader.PropertyToID("_clusterBuffer");
        public static readonly int _lightBuffer = Shader.PropertyToID("_lightBuffer");
        public static readonly int _lightAssignBuffer = Shader.PropertyToID("_lightAssignBuffer");
        public static readonly int _assignTable = Shader.PropertyToID("_assignTable");

        public static readonly int _diffuseIBL = Shader.PropertyToID("_diffuseIBL");
        public static readonly int _specularIBL = Shader.PropertyToID("_specularIBL");
        public static readonly int _brdfLut = Shader.PropertyToID("_brdfLut");
    }


}
