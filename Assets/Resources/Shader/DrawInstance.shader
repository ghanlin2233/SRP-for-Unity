Shader "Unlit/DrawInstance"
{
    SubShader
    {
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR0;
            };

            float4 _Colors[1023];

            v2f vert (appdata v, uint instanceID: SV_INSTANCEID)
            {
                //ALLOW instanceID
                UNITY_SETUP_INSTANCE_ID(i);
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.color = float4(1,0,0,1);

                #ifdef UNITY_INSTANCING_ENABLED
                    o.color = _Colors[instanceID];
                #endif
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                return i.color;
            }
            ENDCG
        }
    }
}
