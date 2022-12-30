using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

/// <summary>
///
/// </summary>
namespace SRP
{
    [CustomEditor(typeof(InstanceData))]
    public class InstanceDataEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            var script = (InstanceData)target;
            if (GUILayout.Button("生成实例数据", GUILayout.Height(20)))
            {
                script.GenerateInstanceData();
            }
        }
    }
}
