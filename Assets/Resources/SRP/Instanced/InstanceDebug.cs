using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
///
/// </summary>
namespace SRP
{
    [ExecuteAlways]
    public class InstanceDebug : MonoBehaviour
    {
        public InstanceData idata;
        public ComputeShader cs;
        public Camera camera;

        public bool usingCulling = false;


        //private void Start()
        //{
        //    cs = Resources.Load<ComputeShader>("Shaders/InstanceCulling");

        //}

        private void Update()
        {
            if (camera == null) camera = Camera.main;
            if (cs == null) cs = Resources.Load<ComputeShader>("CS/InstanceCulling");

            if (usingCulling)
                InstanceDrawer.Draw(idata, camera, cs);
            else
                InstanceDrawer.Draw(idata);
        }
    }
}
