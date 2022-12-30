#ifndef GlobalVariable
#define GlobalVariable

sampler2D _gDepth;
sampler2D _GT0;
sampler2D _GT1;
sampler2D _GT2;
sampler2D _GT3;

samplerCUBE _diffuseIBL;
samplerCUBE _specularIBL;
sampler2D _brdfLut;
sampler2D _noiseTex;

sampler2D _shadowtex0;
sampler2D _shadowtex1;
sampler2D _shadowtex2;
sampler2D _shadowtex3;
sampler2D _shadowStrength;
sampler2D _shadowMask;

float4x4 _shadowVpMatrix0;
float4x4 _shadowVpMatrix1;
float4x4 _shadowVpMatrix2;
float4x4 _shadowVpMatrix3;
        
float4x4 _vpMatrix;
float4x4 _vpMatrixInv;

float _split0;
float _split1;
float _split2;
float _split3;

float _orthoWidth0;
float _orthoWidth1;
float _orthoWidth2;
float _orthoWidth3;

float _shadingPointNormalBias0;
float _shadingPointNormalBias1;
float _shadingPointNormalBias2;
float _shadingPointNormalBias3;

float _screenWidth;
float _screenHeight;
float _noiseTexResolution;

float _pcssFilterRadius0;
float _pcssFilterRadius1;
float _pcssFilterRadius2;
float _pcssFilterRadius3;

float _pcssSearchRadius0;
float _pcssSearchRadius1;
float _pcssSearchRadius2;
float _pcssSearchRadius3;

//**************Cluster*****************
static half2 SignNotZero(half2 xy)
{
    return xy >= 0 ? 1:-1;
}
static half2 PackNormalOct(half3 normalWS){
    half l = dot(abs(normalWS),1); //l = abs(x) + abs(y) + abs(z)
    half3 normalOct = normalWS * rcp(l); //投影到八面体
    if(normalWS.z > 0){ //八面体的上部分投影到xy平面
        return normalOct.xy; 
    }else{ //八面体下部分按对角线翻转投影到xy平面
        return (1 - abs(normalOct.yx)) * SignNotZero(normalOct.xy);
    }
}
static half3 UnpackNormalOct(half2 e)
{
    half3 v = half3(e.xy,1 - abs(e.x) - abs(e.y));
    if(v.z <= 0)
    {
        v.xy = SignNotZero(v.xy) *(1 - abs(v.yx));
    } 
    return normalize(v);
}
static half2 PackNormalAccurate(half3 normalWS)
{
    return PackNormalOct(normalWS) * 0.5 + 0.5;
}
static half3 UnpackNormalAccurate(half2 e)
{
    return UnpackNormalOct(e * 2 - 1);
}

#endif