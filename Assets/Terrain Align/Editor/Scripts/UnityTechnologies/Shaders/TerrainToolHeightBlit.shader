/// This class is based on the Unity Technologies Terrain Tools.
/// License:
/// 
///   Terrain Tools copyright © 2020 Unity Technologies ApS
///   Licensed under the Unity Companion License for Unity-dependent projects--see Unity Companion License.
///   Unless expressly provided otherwise, the Software under this license is made available strictly on an “AS IS” BASIS WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED. Please review the license for details on these and other terms and conditions.
///
/// https://docs.unity3d.com/Packages/com.unity.terrain-tools@3.0/license/LICENSE.html
///
Shader "Hidden/Rowlan/TerrainAlign/HeightBlit" {
    Properties
    {
        _MainTex ("Texture", any) = "" {}
    }
    SubShader {
        Pass {
            ZTest Always Cull Off ZWrite Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            UNITY_DECLARE_SCREENSPACE_TEXTURE(_MainTex);
            uniform float4 _MainTex_ST;
            uniform float _Height_Offset;
            uniform float _Height_Scale;

            struct appdata_t {
                float4 vertex : POSITION;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f {
                float4 vertex : SV_POSITION;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert (appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = TRANSFORM_TEX(v.texcoord.xy, _MainTex);
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float height = UnpackHeightmap(UNITY_SAMPLE_SCREENSPACE_TEXTURE(_MainTex, i.texcoord));
                height = height * _Height_Scale + _Height_Offset;
                return PackHeightmap(height);
            }
            ENDCG

        }
    }
    Fallback Off
}
