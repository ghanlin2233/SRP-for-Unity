using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SRP
{
    [CreateAssetMenu(menuName = "Rendering/MyPipelineAsset")]
    public class MyPipelinAsset : RenderPipelineAsset
    {

        public Cubemap DiffuseIBL;
        public Cubemap SpecularIBL;
        public Texture BrdfLut;
        public Texture BlueNoiseTex;

        [SerializeField]
        public CSMSettings csmSettings;
        public InstanceData[] InstanceDatas;

        protected override RenderPipeline CreatePipeline()
        {
            MyPipelineInstance rp = new MyPipelineInstance();

            rp.diffuseIBL = DiffuseIBL;
            rp.specularIBL = SpecularIBL;
            rp.brdfLut = BrdfLut;
            rp.blueNoiseTex = BlueNoiseTex;

            rp.instanceDatas = InstanceDatas;
            rp.csmSettings = csmSettings;

            return rp;
        }
    }
}
