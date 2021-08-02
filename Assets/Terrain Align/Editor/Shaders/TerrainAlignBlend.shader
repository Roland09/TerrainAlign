Shader "Hidden/Rowlan/TerrainAlign/TerrainBlend"
{
    Properties
    {
        _MainTex("MainTexture", 2D) = "white" {}
        _BlendTex("BlendTexture", 2D) = "white" {}
        _BlendMode("BlendMode", Int) = 0
        _Blend("BlendTexturesAmount", Range(0.0,1.0)) = 0.5

    }
        SubShader
        {
            Tags { "RenderType" = "Transparent" }
            LOD 100

            Pass
            {
                CGPROGRAM
                #pragma vertex vert
                #pragma fragment frag

                #include "UnityCG.cginc"

                static const int BLEND_MODE_EXCLUSIVE = 0;
                static const int BLEND_MODE_VALUE = 1;
                static const int BLEND_MODE_ADD = 2;

                struct appdata
                {
                    float4 vertex : POSITION;
                    float2 uv : TEXCOORD0;
                };

                struct v2f
                {
                    float2 uv : TEXCOORD0;
                    float4 vertex : SV_POSITION;
                };

                sampler2D _MainTex;
                sampler2D _BlendTex;
                half _Blend;
                int _BlendMode;
                float4 _MainTex_ST;

                v2f vert(appdata v)
                {
                    v2f o;
                    o.vertex = UnityObjectToClipPos(v.vertex);
                    o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                    return o;
                }

                fixed4 frag(v2f i) : SV_Target
                {
                    // sample the texture
                    fixed4 c = tex2D(_MainTex, i.uv); // color
                    fixed4 b = tex2D(_BlendTex, i.uv); // blend

                    if (_BlendMode == BLEND_MODE_VALUE) {
                        c.rgb =(c.rgb * (1 - _Blend)) + (b.rgb * (_Blend)); 
                        c.a = (c.a * (1 - _Blend)) + (b.a * _Blend);
                    }
                    else if (_BlendMode == BLEND_MODE_ADD) {
                        c.rgb = c.rgb + b.rgb;
                        c.a = c.a + b.a;
                    }
                    else if (_BlendMode == BLEND_MODE_EXCLUSIVE) {
                        c.rgb = b.rgb == 0 ? c.rgb : b.rgb;
                    }

                    return c;
                }
                ENDCG
            }
        }
}
