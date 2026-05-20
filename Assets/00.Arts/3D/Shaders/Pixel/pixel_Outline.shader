Shader "JOJO/Pixel/pixel_Outline"
{
    Properties
    {
        _OutlineColor  ("Pixel Outline Color", Color) = (0,0,0,1)
        _OutlinePixels ("Pixel Outline Width", Range(0.5,500)) = 2.0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 200

        Pass
        {
            Name "Outline"
            Tags { "LightMode"="ForwardBase" }
            Cull Front

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _OutlineColor)
            UNITY_INSTANCING_BUFFER_END(Props)

            float  _OutlinePixels;
            float  _OutlinePixelsZoomUse;
            float  _OutlinePixelsZoomed;
            float  _PixelRTWidth;
            float  _PixelRTHeight;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);

                float4 clipPos = UnityObjectToClipPos(v.vertex);

                float3 viewN = normalize(mul((float3x3)UNITY_MATRIX_IT_MV, v.normal));
                float2 clipN = mul((float2x2)UNITY_MATRIX_P, viewN.xy);
                float len = length(clipN);
                float2 dir = (len > 1e-5) ? (clipN / len) : float2(0, 0);

                float outlinePx = _OutlinePixels;
                if (_OutlinePixelsZoomUse > 0.5)
                    outlinePx = _OutlinePixelsZoomed;

                // PixelCamera가 _PixelRTHeight/_PixelRTWidth 를 제공하면(픽셀 포스트/스냅 활성),
                // 최종 픽셀화된 화면에서 "픽셀 단위"로 두께가 유지되도록 PixelRT 기준으로 계산한다.
                // (SceneView 등 PixelRT 정보가 없으면 화면 해상도 기준으로 폴백)
                float ndcPerPixel = (_PixelRTWidth > 0.5 && _PixelRTHeight > 0.5)
                    ? (2.0 / _PixelRTHeight)
                    : (2.0 / _ScreenParams.y);

                clipPos.xy += dir * outlinePx * ndcPerPixel * clipPos.w;

                if (_PixelRTWidth > 0 && _PixelRTHeight > 0)
                {
                    float2 rtRes = float2(_PixelRTWidth, _PixelRTHeight);
                    clipPos.xy   = round(clipPos.xy / clipPos.w * rtRes) / rtRes * clipPos.w;
                }

                o.pos = clipPos;
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                return UNITY_ACCESS_INSTANCED_PROP(Props, _OutlineColor);
            }
            ENDCG
        }
    }
    FallBack "Standard"
}
