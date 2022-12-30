using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
///
/// </summary>
public class DrawMeshInstancedDemo : MonoBehaviour
{
    [Range(0f, 1023.0f)]
    public int population;

    public float range;
    public Material material;
    private Matrix4x4[] matrices;
    public Mesh mesh;
    private MaterialPropertyBlock block;

    private void Setup()
    {
        this.mesh = mesh;
        matrices = new Matrix4x4[population];
        Vector4[] colors = new Vector4[population];
        block = new MaterialPropertyBlock();

        for (int i = 0; i < population; i++)
        {
            Vector3 position = new Vector3(Random.Range(-range, range), Random.Range(-range, range), Random.Range(-range, range));
            Quaternion rotation = Quaternion.Euler(Random.Range(-180, 180), Random.Range(-180, 180), Random.Range(-180, 180));
            Vector3 scale = Vector3.one;

            matrices[i] = Matrix4x4.TRS(position, rotation, scale);
            colors[i] = Color.Lerp(Color.red, Color.Lerp(Color.blue, Color.green, Random.value), Random.value);
        }
        block.SetVectorArray("_Colors", colors);
    }

    private void Start()
    {
        Setup();
    }
    private void Update()
    {
        Graphics.DrawMeshInstanced(mesh, 0, material, matrices, population, block);
    }
}

