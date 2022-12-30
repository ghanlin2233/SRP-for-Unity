using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SRP
{
    [ExecuteAlways]
    public class ClusterDebug : MonoBehaviour
    {
        ClusterLight clusterLight;
        Camera cam;
        // Start is called before the first frame update
        void Start()
        {
            if(clusterLight == null)
                clusterLight = new ClusterLight();
            cam = Camera.main;
        }

        // Update is called once per frame
        void Update()
        {
            var lights = FindObjectsOfType<Light>();
            clusterLight.UpdataLightBuffer(lights);
            //划分cluster
            clusterLight.CalculateCluster(cam);
            
            //分配光源
            clusterLight.LightAssign();

            //debug
            clusterLight.DebugCluster();
            clusterLight.DebugLightAssign();

        }
    }
}
