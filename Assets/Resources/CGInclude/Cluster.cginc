#ifndef Cluster
#define Cluster

#include "GlobalVariables.cginc"

struct PointLight
{
	float3 position;
	float intensity;
	float3 color;
	float radius;
};

struct LightIndex
{
	uint start;
	uint count;
};

StructuredBuffer<PointLight> _lightBuffer;
StructuredBuffer<uint> _lightAssignBuffer;
StructuredBuffer<LightIndex> _assignTable;

float _numClusterX;
float _numClusterY;
float _numClusterZ;


uint Index3DTo1D(uint3 i)
{
    return i.z * _numClusterX * _numClusterY
        + i.y * _numClusterX
        + i.x;
}


#endif