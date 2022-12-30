Shader "Deferred/lightPass"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
     CGINCLUDE
	    #include "UnityCG.cginc"
        #include "../CGInclude/MyBRDF.cginc"
        #include "../CGInclude/GlobalVariables.cginc"
        #include "Lighting.cginc"
        #include "../CGInclude/Shadow.cginc"
        #include "../CGInclude/Cluster.cginc"

        #pragma vertex vert
        #pragma fragment frag
        struct appdata
        {
            float4 vertex : POSITION;
            float2 uv : TEXCOORD0;
        };
        struct v2f
        {
            float4 pos : SV_POSITION;
            float2 uv : TEXCOORD0;
        };
    ENDCG

    SubShader
    {        
        Cull off ZWrite on ZTest Always
        pass
        {        
            CGPROGRAM
            
            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }
            half4 frag(v2f i, out float depthOut : SV_Depth):SV_Target
            {
                float2 uv = i.uv;
                float4 GT2 = tex2D(_GT2, uv);
                float4 GT3 = tex2D(_GT3, uv);

                // 从 Gbuffer 解码数据
                float3 albedo = tex2D(_GT0, uv).rgb;
                float3 normal =  tex2D(_GT1, uv).rgb * 2 - 1;
                normal = normalize(normal);
                float2 motionVec = GT2.rg;
                float roughness = GT2.b;
                float metallic = GT2.a;
                float3 emission = GT3.rgb;
                float occlusion = GT3.a;

                float d = UNITY_SAMPLE_DEPTH(tex2D(_gDepth, uv));
                float d_lin = Linear01Depth(d);
                depthOut = d;
                // 反投影重建世界坐标
                float4 ndcPos = float4(uv*2-1, d, 1);
                float4 worldPos = mul(_vpMatrixInv, ndcPos);
                worldPos /= worldPos.w;

                //计算参数
                half3 lightDir = normalize(UnityWorldSpaceLightDir(worldPos));
                //lightDir = normalize(_WorldSpaceLightPos0.xyz);
                half3 viewDir = normalize(UnityWorldSpaceViewDir(worldPos));
                //viewDir = normalize(_WorldSpaceCameraPos.xyz - worldPos.xyz);
                half3 halfDir = normalize(lightDir + viewDir);
                half3 refDir = reflect(-viewDir, normal);
                half3 radiance = _LightColor0.rgb;

                //计算直接光照
                float3 color = float3(0,0,0);
                half3 direct = PBR(normal, viewDir, lightDir, albedo, radiance, roughness, metallic);

                // 计算环境光照
                float3 ambient = IBL(normal, viewDir, albedo, roughness, metallic, _diffuseIBL, _specularIBL, _brdfLut);

                //计算阴影
                float shadow = tex2D(_shadowStrength, uv).r;

                color += direct * shadow;
                color += ambient * occlusion;
                color += emission;

                //计算Cluster
                uint x = floor(uv.x * _numClusterX);
                uint y = floor(uv.y * _numClusterY);
                uint z = floor((1-d_lin) * _numClusterZ); //z反转

                uint3 clusterId_3D = uint3(x, y, z);
                uint clusterId_1D = Index3DTo1D(clusterId_3D);
                LightIndex lightIndex = _assignTable[clusterId_1D];

                uint start = lightIndex.start;
                uint end = lightIndex.start + lightIndex.count;
                for(int j = start; j<end; j++)
                {
                    uint lightId = _lightAssignBuffer[j]; //灯光id
                    PointLight lit = _lightBuffer[lightId];

                    lightDir = normalize(lit.position - worldPos.xyz);
                    radiance = lit.color;

                    //灯光衰减
                    float dis = distance(lit.position, worldPos.xyz);
                    float d2 = dis * dis;
                    float r2 = lit.radius * lit.radius;
                    float dying = saturate(1 - (d2/r2) * (d2/r2));
                    dying *= dying;

                    color += PBR(normal, viewDir, lightDir, albedo, radiance, roughness, metallic) * lit.intensity * dying;                   
                }
                
                return half4(color, 1);
            }
            ENDCG
        }
    }
}
