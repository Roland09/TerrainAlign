// Modify the bottom-up projection to align with top-down projection
Shader "Hidden/Rowlan/TerrainAlign/BottomUpToTopDown"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            struct appdata
            {
                float4 vertex : POSITION;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            sampler2D _MainTex;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord;

                // flip in y direction, adjust to projection camera rotation
                o.uv.y = 1 - o.uv.y;

                return o;
            }

            float _GameObjectPositionY;
            float _CameraFarClipPlane;
            float _CutOff;

            float frag(v2f i) : SV_Target
            {
                float col = 1 - tex2D(_MainTex, i.uv).r;

                if( col.r <= 0 || col.r >= 1)
                    return 0;

                float f = _GameObjectPositionY / _CameraFarClipPlane;
                if( (col - f) > _CutOff)
                    return 0;

                return col;
            }
            ENDCG
        }
    }
}
