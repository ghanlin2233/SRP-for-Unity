#ifndef Shadow
#define Shadow

#define EPS 1e-5

float VsmShadow(float distance, float2 moments)
{
    float variance = moments.y - (moments.x * moments.x);//
    float d = moments.x - distance;
    float p_max = variance / (variance + d*d);
    return max(saturate(p_max), distance <= moments.x);
}

float ShadowMap(float4 worldPos, sampler2D _shadowtex, float4x4 _shadowVpMatrix)
{
    float4 shadowNdc = mul(_shadowVpMatrix, worldPos);
    shadowNdc /= shadowNdc.w;
    float3 coord;
    coord.xy = shadowNdc.xy * 0.5 + 0.5;
    coord.z = shadowNdc.z;
    float2 d_sample = tex2D(_shadowtex, coord.xy).rg;

#if defined (UNITY_REVERSED_Z)
    if(d_sample.r>coord.z) return 0;
#else
    if(d_sample.r<coord.z) return 0;
#endif

    return 1;
}

float PCF5x5(float4 worldPos, sampler2D _shadowtex, float4x4 _shadowVpMatrix, float shadowMapResolution, float bias)
{
    float4 shadowNdc = mul(_shadowVpMatrix, worldPos);
    shadowNdc /= shadowNdc.w;
    float2 uv = shadowNdc.xy * 0.5 + 0.5;

    float d_shadingPoint = shadowNdc.z;
    float shadow = 0.0;

    for(int i=-2; i<=2; i++)
    {
        for(int j=-2; j<=2; j++)
        {
            float2 offset = float2(i, j) / shadowMapResolution;
            float d_sample = tex2D(_shadowtex, uv+offset).r;

            #if defined (UNITY_REVERSED_Z)
                if(d_sample-bias>d_shadingPoint)
            #else
                if(d_sample<d_shadingPoint)
            #endif
                shadow += 1.0;
        }
    }

    return 1.0 - (shadow / 25.0);
}

//#define N_SAMPLE 16
static float2 poissonDisk1[16] = 
{
    float2( -0.94201624, -0.39906216 ),
    float2( 0.94558609, -0.76890725 ),
    float2( -0.094184101, -0.92938870 ),
    float2( 0.34495938, 0.29387760 ),
    float2( -0.91588581, 0.45771432 ),
    float2( -0.81544232, -0.87912464 ),
    float2( -0.38277543, 0.27676845 ),
    float2( 0.97484398, 0.75648379 ),
    float2( 0.44323325, -0.97511554 ),
    float2( 0.53742981, -0.47373420 ),
    float2( -0.26496911, -0.41893023 ),
    float2( 0.79197514, 0.19090188 ),
    float2( -0.24188840, 0.99706507 ),
    float2( -0.81409955, 0.91437590 ),
    float2( 0.19984126, 0.78641367 ),
    float2( 0.14383161, -0.14100790 )
};

#define N_SAMPLE 64
static float2 poissonDisk[N_SAMPLE] = {
    float2(-0.5119625f, -0.4827938f),
    float2(-0.2171264f, -0.4768726f),
    float2(-0.7552931f, -0.2426507f),
    float2(-0.7136765f, -0.4496614f),
    float2(-0.5938849f, -0.6895654f),
    float2(-0.3148003f, -0.7047654f),
    float2(-0.42215f, -0.2024607f),
    float2(-0.9466816f, -0.2014508f),
    float2(-0.8409063f, -0.03465778f),
    float2(-0.6517572f, -0.07476326f),
    float2(-0.1041822f, -0.02521214f),
    float2(-0.3042712f, -0.02195431f),
    float2(-0.5082307f, 0.1079806f),
    float2(-0.08429877f, -0.2316298f),
    float2(-0.9879128f, 0.1113683f),
    float2(-0.3859636f, 0.3363545f),
    float2(-0.1925334f, 0.1787288f),
    float2(0.003256182f, 0.138135f),
    float2(-0.8706837f, 0.3010679f),
    float2(-0.6982038f, 0.1904326f),
    float2(0.1975043f, 0.2221317f),
    float2(0.1507788f, 0.4204168f),
    float2(0.3514056f, 0.09865579f),
    float2(0.1558783f, -0.08460935f),
    float2(-0.0684978f, 0.4461993f),
    float2(0.3780522f, 0.3478679f),
    float2(0.3956799f, -0.1469177f),
    float2(0.5838975f, 0.1054943f),
    float2(0.6155105f, 0.3245716f),
    float2(0.3928624f, -0.4417621f),
    float2(0.1749884f, -0.4202175f),
    float2(0.6813727f, -0.2424808f),
    float2(-0.6707711f, 0.4912741f),
    float2(0.0005130528f, -0.8058334f),
    float2(0.02703013f, -0.6010728f),
    float2(-0.1658188f, -0.9695674f),
    float2(0.4060591f, -0.7100726f),
    float2(0.7713396f, -0.4713659f),
    float2(0.573212f, -0.51544f),
    float2(-0.3448896f, -0.9046497f),
    float2(0.1268544f, -0.9874692f),
    float2(0.7418533f, -0.6667366f),
    float2(0.3492522f, 0.5924662f),
    float2(0.5679897f, 0.5343465f),
    float2(0.5663417f, 0.7708698f),
    float2(0.7375497f, 0.6691415f),
    float2(0.2271994f, -0.6163502f),
    float2(0.2312844f, 0.8725659f),
    float2(0.4216993f, 0.9002838f),
    float2(0.4262091f, -0.9013284f),
    float2(0.2001408f, -0.808381f),
    float2(0.149394f, 0.6650763f),
    float2(-0.09640376f, 0.9843736f),
    float2(0.7682328f, -0.07273844f),
    float2(0.04146584f, 0.8313184f),
    float2(0.9705266f, -0.1143304f),
    float2(0.9670017f, 0.1293385f),
    float2(0.9015037f, -0.3306949f),
    float2(-0.5085648f, 0.7534177f),
    float2(0.9055501f, 0.3758393f),
    float2(0.7599946f, 0.1809109f),
    float2(-0.2483695f, 0.7942952f),
    float2(-0.4241052f, 0.5581087f),
    float2(-0.1020106f, 0.6724468f)
};


float2 RotateVec2(float2 v, float angle)
{
    float s = sin(angle);
    float c = cos(angle);
    return float2(v.x*c+v.y*s, -v.x*s+v.y*c);
}
        
float2 AverageBlockerDepth(float4 shadowNdc, sampler2D _shadowtex, float d_shadingPoint, float searchWidth, float rotateAngle)
{
    float2 uv = shadowNdc.xy * 0.5 + 0.5;
    float step = 3.0;
    float d_average = 0.0;
    float count = 0.0005;   // 防止 ÷ 0

    for(int i=0; i<16; i++)
    {                
        float2 unitOffset = RotateVec2(poissonDisk1[i], rotateAngle);  // map to [-1, 1]
        float2 offset = unitOffset * searchWidth;
        float2 uvo = uv + offset;

        float d_sample = tex2D(_shadowtex, uvo).r;
        if(d_sample>d_shadingPoint)
        {
            count += 1;
            d_average += d_sample;
        }              
    }
    return float2(d_average / count, count);
}
float ShadowMapPCSS(float4 worldPos, sampler2D _shadowtex, float4x4 _shadowVpMatrix, float orthoWidth, float rotateAngle, float searchRadius, float filterRadius)
{
    float4 shadowNdc = mul(_shadowVpMatrix, worldPos);
    shadowNdc /= shadowNdc.w;
    float d_shadingPoint = shadowNdc.z;  // 着色点深度
    float2 uv = shadowNdc.xy * 0.5 + 0.5;
    float2 d_sample_d = tex2D(_shadowtex, uv).r;
    
    // 计算平均遮挡深度
    float searchWidth = searchRadius / orthoWidth;
    float2 blocker = AverageBlockerDepth(shadowNdc, _shadowtex, d_shadingPoint, searchWidth, rotateAngle);
    float d_average = blocker.x;
    float blockCnt = blocker.y;
    //return blockCnt / 49;
    if(blockCnt<1) return 1.0;    // 没有遮挡则直接返回

    // 世界空间下的距离, 计算 PCSS 用, 注意 Reverse Z
    float d_receiver = 1.0 - d_shadingPoint;
    float d_blocker = 1.0 - d_average;

    // 世界空间下的 filter 半径
    float w = (d_receiver - d_blocker) * filterRadius / d_blocker;
    
    // 深度图上的 filter 半径
    float radius = w / orthoWidth;

    float shadow = 0.0f;

    for(int i=0; i<N_SAMPLE; i++)
    {
        float2 offset = RotateVec2(poissonDisk[i], rotateAngle);
        float2 uvo = uv + offset * radius;
        float d_sample = tex2D(_shadowtex, uvo).r;
        if(d_sample>d_shadingPoint) shadow += 1.0f;
    }
    shadow /= N_SAMPLE;  
    return 1.0 - shadow;        
}


#endif
