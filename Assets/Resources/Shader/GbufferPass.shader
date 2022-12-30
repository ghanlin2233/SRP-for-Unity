Shader "Deferred/GbufferPass"
{
    Properties
    {
        _Color("Color",color) = (1,1,1,1)	//颜色
        _MainTex("Albedo",2D) = "white"{}	//反照率
        _MetallicGlossMap("Metallic",2D) = "white"{} //金属图，r通道存储金属度，a通道存储光滑度
        _BumpMap("Normal Map",2D) = "bump"{}//法线贴图
        _HeightMap("Height Map",2D) = "white"{}//高度贴图
        _OcclusionMap("Occlusion",2D) = "white"{}//环境光遮挡纹理
        _MetallicStrength("MetallicStrength",Range(0,1)) = 1 //金属强度
        _GlossStrength("Smoothness",Range(0,1)) = 0.5 //光滑强度
        _BumpScale("Normal Scale",float) = 1 //法线影响大小
        _EmissionColor("Emission Color",color) = (0,0,0) //自发光颜色
        _EmissionMap("Emission Map",2D) = "white"{}//自发光贴图     
    }
    SubShader
    {

        Pass
        {
            Tags { "LightMode"="gbuffer" }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "../CGInclude/GlobalVariables.cginc"
            #include "UnityCG.cginc"

            half4 _Color;
			sampler2D _MainTex;
			float4 _MainTex_ST;
			sampler2D _MetallicGlossMap;
			sampler2D _BumpMap;
            sampler2D _HeightMap;
			sampler2D _OcclusionMap;
			half _MetallicStrength;
			half _GlossStrength;
			float _BumpScale;
			half4 _EmissionColor;
			sampler2D _EmissionMap;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float3 normal:NORMAL;
                float4 tangent:TANGENT;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 pos : SV_POSITION;
                float4 TtoW0 : TEXCOORD2;
	            float4 TtoW1 : TEXCOORD3;
	            float4 TtoW2 : TEXCOORD4;
                float3 normal : NORMAL;
            };
          
            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.texcoord,_MainTex);
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex);
                float3 worldNormal = UnityObjectToWorldNormal(v.normal);
                float3 worldTangent = UnityObjectToWorldDir(v.tangent);
                float3 worldBinormal = cross(worldNormal, worldTangent) * v.tangent.w;

                o.normal = UnityObjectToWorldNormal(v.normal);


                //前3x3存储着从切线空间到世界空间的矩阵，后3x1存储着世界坐标
	            o.TtoW0 = float4(worldTangent.x,worldBinormal.x,worldNormal.x,worldPos.x);
	            o.TtoW1 = float4(worldTangent.y,worldBinormal.y,worldNormal.y,worldPos.y);
	            o.TtoW2 = float4(worldTangent.z,worldBinormal.z,worldNormal.z,worldPos.z);

                return o;
            }

            void frag (v2f i,
            out float4 GT0:SV_TARGET0,
            out float4 GT1:SV_TARGET1,
            out float4 GT2:SV_TARGET2,
            out float4 GT3:SV_TARGET3
            )
            {
                // 贴图采样
                half3 worldPos = half3(i.TtoW0.w, i.TtoW1.w, i.TtoW2.w);
                half4 albedo = tex2D(_MainTex, i.uv) * _Color;//反照率
                half2 metallicGloss = tex2D(_MetallicGlossMap, i.uv).ra;
                half metallic = metallicGloss.x * _MetallicStrength;//金属率
                half roughness = 1 - metallicGloss.y * _GlossStrength;//粗糙度
                half occlusion = tex2D(_OcclusionMap, i.uv).g;//环境光遮蔽
                half3 emissionColor = tex2D(_EmissionMap, i.uv).rgb * _EmissionColor.rgb;

                //计算世界空间的向量
                half3 normalTangent = UnpackNormal(tex2D(_BumpMap, i.uv));
                half height = tex2D(_HeightMap, i.uv).r;
                normalTangent.xy *= _BumpScale * height;
                normalTangent.z = sqrt(1.0 - saturate(dot(normalTangent.xy,normalTangent.xy)));
                half3 worldNormal = normalize(half3(dot(i.TtoW0.xyz,normalTangent), dot(i.TtoW1.xyz,normalTangent),dot(i.TtoW2.xyz,normalTangent)));
                half3 normal = worldNormal * 0.5f + 0.5f;
                normal = i.normal;

                GT0 = albedo;
                GT1 = float4(normal * 0.5 + 0.5, 0);
                //GT1 = float4(normal, 0);
                GT2 = float4(1,1,roughness,metallic);
                GT3 = float4(emissionColor,occlusion);
            }
            ENDCG
        }

        pass
        {
            Tags{"LightMode"= "depthonly"}

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

             struct v2f
            {
                float4 pos : SV_POSITION;
                float2 depth : TEXCOORD0;
            };

            v2f vert (appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.depth = o.pos.zw;
                return o;
            }
            float4 frag(v2f i):SV_Target
            {
                float d = i.depth.x / i.depth.y;
                #if defined (UNITY_REVERSED_Z)
                    d = 1.0 - d;
                #endif
                //float4 c = EncodeFloatRGBA(d);
                //return c;
                float moment1 = d;
                float moment2 = d * d;
                return float4(moment1, moment2, 0, 0);
            }
            ENDCG
        }
    }
}
