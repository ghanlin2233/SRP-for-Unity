Shader "MyRP/PBRshder"
{
    Properties
    {
        
    }
    SubShader
    {

        Pass
        {
            Tags { "LightMode"="depthonly" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "UnityStandardBRDF.cginc"

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 depth : TEXCOORD0;
            };

            v2f vert (appdata_base v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.depth = o.vertex.zw;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float d = i.depth.x / i.depth.y;
            #if defined (UNITY_REVERSED_Z)
                d = 1.0 - d;
            #endif
                fixed4 c = EncodeFloatRGBA(d);
                //return float4(d,0,0,1);   // for debug
                return c;
            }
            ENDCG 
        }

        Pass
        {
            Tags { "LightMode"="ggbuffer" }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float3 normal : NORMAL;
                float4 tangent:TANGENT;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 pos : SV_POSITION;
                float3 N : TEXCOORD1;
                float3 T :TEXCOORD2;
                float3 B :TEXCOORD3;
                float3 wPos:TEXCOORD4;
            };

            float4 _MainTex_ST;

            sampler2D _MainTex;
            sampler2D _MetallicGlossMap;
            sampler2D _EmissionMap;
            sampler2D _OcclusionMap;
            sampler2D _BumpMap;

            float _Use_Ao_Map;
            float _Use_Metal_Map;
            float _Use_Normal_Map;
            float _Metallic_global;
            float _Roughness_global;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.N = UnityObjectToWorldNormal(v.normal);
                o.T = UnityObjectToWorldDir(v.tangent.xyz);
                o.B = normalize(cross(o.T, o.N));
                o.wPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            void frag (
                v2f i,
                out float4 GT0 : SV_Target0,
                out float4 GT1 : SV_Target1,
                out float4 GT2 : SV_Target2,
                out float4 GT3 : SV_Target3
            )
            {
                //贴图采样
                float4 texMap = tex2D(_MainTex, i.uv);
                float4 emMap = tex2D(_EmissionMap, i.uv);
                float4 aoMap = tex2D(_OcclusionMap, i.uv);
                float4 metalMap = tex2D(_MetallicGlossMap, i.uv);
                metalMap.a *= _Roughness_global;
                float4 normalMap = tex2D(_BumpMap, i.uv);

                //向量准备
                float metallic = _Metallic_global;
                float roughness = _Roughness_global;
                if(_Use_Metal_Map)
                {
                    metallic = metalMap.r;
                    roughness = 1.0 - metalMap.a;
                }

                float3 normal = i.N;
                if(_Use_Normal_Map)
                {
                    float3x3 TBN = float3x3(i.T, i.B, i.N);
                    normal = UnpackNormal(normalMap);
                    normal = normalize(mul(normal, TBN));
                }

                float ao = 1.0f;
                if(_Use_Ao_Map)
                {
                    ao = aoMap.r;
                }

                GT0 = texMap;
                GT1 = float4(normal, 0);
                GT2 = float4(metalMap);
                GT3 = float4(emMap.rgb, ao);
            }
            ENDCG
        }
    }
}
