Shader "Deferred/shadowPass"
{
    CGINCLUDE
	    #include "UnityCG.cginc"
        #include "../CGInclude/MyBRDF.cginc"
        #include "../CGInclude/GlobalVariables.cginc"
        #include "Lighting.cginc"
        #include "../CGInclude/Shadow.cginc"

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
        Cull Off ZWrite Off ZTest Always

        pass
        {
            CGPROGRAM
            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }           

            float frag (v2f i) : SV_Target
            {
                // 从 Gbuffer 解码数据
                float2 uv = i.uv; 
                float3 normal = tex2D(_GT1, uv).rgb * 2 - 1;
                float d = UNITY_SAMPLE_DEPTH(tex2D(_gDepth, uv));
                float d_lin = Linear01Depth(d);

                // 反投影重建世界坐标
                float4 ndcPos = float4(uv*2-1, d, 1);
                float4 worldPos = mul(_vpMatrixInv, ndcPos);
                worldPos /= worldPos.w;

                // 向着法线偏移采样点
                float3 lightDir = normalize(_WorldSpaceLightPos0.xyz);
                float bias = max(0.001 * (1.0 - dot(normal, lightDir)), 0.001);
                float4 worldPosOffset = worldPos;
                worldPosOffset.xyz += normal * bias;

                if(dot(lightDir, normal) < 0.005) return 0;

                // 随机旋转角度
                float2 uv_noi = uv*float2(_screenWidth, _screenHeight)/_noiseTexResolution;
                float rotateAngle = tex2D(_noiseTex, uv_noi*0.5).r * UNITY_TWO_PI;   // blue noise
                //float mask = tex2D(_shadowMask, uv).r;
                    //if(mask < 0.0000005) return 0;
                    //if(mask > 0.9999995) return 1;
                //_searchRadius = 0.1f;
                //_filterRadius = 10;

                float shadow = 1.0;
                if(d_lin<_split0) 
                {
                    worldPosOffset.xyz += normal * _shadingPointNormalBias0;
                    //shadow *= ShadowMap(worldPosOffset, _shadowtex0, _shadowVpMatrix0);
                    //shadow *= PCF5x5(worldPosOffset, _shadowtex0, _shadowVpMatrix0, 2048, bias);
                    shadow *= ShadowMapPCSS(worldPosOffset, _shadowtex0, _shadowVpMatrix0, _orthoWidth0, rotateAngle, _pcssSearchRadius0, _pcssFilterRadius0);

                }
                else if(d_lin<_split0+_split1)
                {
                    worldPosOffset.xyz += normal * _shadingPointNormalBias1;
                    //shadow *= ShadowMap(worldPosOffset, _shadowtex1, _shadowVpMatrix1);
                    //shadow *= PCF5x5(worldPosOffset, _shadowtex1, _shadowVpMatrix1, 2048, bias);
                    shadow *= ShadowMapPCSS(worldPosOffset, _shadowtex1, _shadowVpMatrix1, _orthoWidth1, rotateAngle, _pcssSearchRadius1, _pcssFilterRadius1);
                }
                else if(d_lin<_split0+_split1+_split2) 
                {   
                    worldPosOffset.xyz += normal * _shadingPointNormalBias2;
                    //shadow *= ShadowMap(worldPosOffset, _shadowtex2, _shadowVpMatrix2);
                    //shadow *= PCF5x5(worldPosOffset, _shadowtex2, _shadowVpMatrix2, 2048, bias);
                    shadow *= ShadowMapPCSS(worldPosOffset, _shadowtex2, _shadowVpMatrix2, _orthoWidth2, rotateAngle, _pcssSearchRadius2, _pcssFilterRadius2);
                }
                else if(d_lin<_split0+_split1+_split2+_split3)
                {
                    worldPosOffset.xyz += normal * _shadingPointNormalBias3;
                    //shadow *= ShadowMap(worldPosOffset, _shadowtex3, _shadowVpMatrix3);
                    //shadow *= PCF5x5(worldPosOffset, _shadowtex3, _shadowVpMatrix3, 2048, bias);
                    shadow *= ShadowMapPCSS(worldPosOffset, _shadowtex3, _shadowVpMatrix3, _orthoWidth3, rotateAngle, _pcssSearchRadius3, _pcssFilterRadius3);
                }
                return shadow;
            }
            ENDCG
        }
    }
}
