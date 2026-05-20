Shader "JOJO/Pixel/PixelObject2"
{
    Properties
    {
        [Header(Base)]
        _MainTex        ("Albedo", 2D)                 = "white" {}
        _Color          ("Tint", Color)               = (1,1,1,1)

        [Header(Lit)]
        _LitColor       ("Lit Color", Color)           = (1,1,1,1)
        _LitStrength    ("Lit Brightness", Range(1,2.5)) = 1.45
        _LitAreaStart   ("Lit Area Start", Range(0.01,1)) = 0.5
        _LitAreaEnd     ("Lit Area End", Range(0.01,1))   = 1.0

        [Header(Light Shadow)]
        _LightShadowColor   ("Shadow Color", Color)                = (0.15,0.1,0.2,1)
        _LightShadowStrength("Shadow Strength", Range(0,1))      = 0.6
        _LightCelStep       ("Cel Step", Range(0.01,1))            = 0.5

        [Header(Rim Silhouette)]
        _RimShadowColor     ("Rim Shadow Color", Color)           = (0.05,0.05,0.15,1)
        _RimCelStep         ("Rim Cel Step", Range(0.01,1))       = 0.35
        _RimShadowStrength  ("Rim Shadow Strength", Range(0,1))   = 0.5

        [Header(Distance Fog)]
        _FogColor       ("Fog Color", Color)           = (0.55,0.65,0.8,1)
        _FogStart       ("Fog Start", Float)           = 8.0
        _FogEnd         ("Fog End", Float)             = 30.0
        _FogStrength    ("Fog Strength", Range(0,1))   = 0.7
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 100

        Pass
        {
            Name "FORWARD"
            Tags { "LightMode"="ForwardBase" }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase
            #pragma multi_compile_fog
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"
            // 저사양: ShadeSH9·V·fog·distance → vert / smoothstep→step / ForwardAdd는 fwdadd만
            // TEXCOORD: uv0 + nWs + Vws + ambFog(ambient.xyz+fog) + Shadow + Fog → 슬롯 한계 근접.
            //           기능 추가 시 새 TEXCOORD 추가 대신 ambFog 등 기존 슬롯 재패킹.

            sampler2D _MainTex;
            float4    _MainTex_ST;
            half4     _Color;
            half4     _LitColor;
            half      _LitStrength;
            half      _LitAreaStart;
            half      _LitAreaEnd;
            half4     _LightShadowColor;
            half      _LightShadowStrength;
            half      _LightCelStep;
            half4     _RimShadowColor;
            half      _RimShadowStrength;
            half      _RimCelStep;
            half4     _FogColor;
            float     _FogStart;
            float     _FogEnd;
            half      _FogStrength;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos      : SV_POSITION;
                half2  uv       : TEXCOORD0;
                half3  nWs      : TEXCOORD1;
                half3  Vws      : TEXCOORD2;
                half4  ambFog   : TEXCOORD3;
                SHADOW_COORDS(4)
                UNITY_FOG_COORDS(5)
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv  = half2(TRANSFORM_TEX(v.uv, _MainTex));

                float3 wN = UnityObjectToWorldNormal(v.normal);
                float3 N  = normalize(wN);
                float3 wP = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.nWs = half3(N);
                o.Vws = half3(normalize(_WorldSpaceCameraPos - wP));

                float3 amb = ShadeSH9(float4(N, 1.0)) * 0.2;
                o.ambFog.xyz = half3(amb);
                float camDist = distance(wP, _WorldSpaceCameraPos);
                half fogT = saturate((camDist - _FogStart) / max(_FogEnd - _FogStart, 0.001));
                o.ambFog.w = fogT * fogT * _FogStrength;

                TRANSFER_SHADOW(o);
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                half4 tex = half4(tex2D(_MainTex, float2(i.uv))) * _Color;
                // 라이트 쉐도우(NdotL): 월드 노멀을 픽셀에서 정규화 — 보간 노멀 길이/정밀도로 카메라 각도에 경계가 흔들이는 현상 완화
                float3 Nws = normalize(float3(i.nWs));
                float3 L   = float3(_WorldSpaceLightPos0.xyz);
                float3 V   = float3(i.Vws);
                float  atten = SHADOW_ATTENUATION(i);

                float ndl         = (dot(Nws, L) * 0.5 + 0.5) * atten;
                float lightMask   = step(float(_LightCelStep), ndl);
                float litMid      = float(_LitAreaStart + _LitAreaEnd) * 0.5;
                float litRamp     = step(litMid, ndl);
                // 방향광 밝기(베이스 알베도 유지) → Lit Color는 litRamp=1인 ‘최고 밝음’ 구간에서만 블렌드
                half3 dirLit      = tex.rgb * half3(_LightColor0.rgb);
                half3 brightCore  = dirLit * lerp(half(1.0), _LitStrength, half(litRamp));
                half3 litCol      = lerp(brightCore, brightCore * _LitColor.rgb, half(litRamp));
                // 쉐도우 셀: Shadow Color / Strength (스크린샷과 동일 계열)
                half3 lightShadCol = tex.rgb * lerp(half3(1,1,1), _LightShadowColor.rgb, _LightShadowStrength);
                half3 litSide      = lerp(lightShadCol, litCol, half(lightMask));
                half  NdotV       = half(saturate(dot(Nws, V)));
                half  rimMask     = step(_RimCelStep, NdotV);
                half3 rimShadCol  = litSide * lerp(half3(1,1,1), _RimShadowColor.rgb, _RimShadowStrength);
                half3 withRim     = lerp(rimShadCol, litSide, rimMask);
                half3 col         = lerp(litSide, withRim, half(lightMask));

                col += i.ambFog.xyz * tex.rgb;
                col = lerp(col, _FogColor.rgb, i.ambFog.w);

                float4 output = float4(col, tex.a);
                UNITY_APPLY_FOG(i.fogCoord, output);
                return output;
            }
            ENDCG
        }

        // ForwardAdd: Point / Spot
        Pass
        {
            Name "FORWARD_ADD"
            Tags { "LightMode"="ForwardAdd" }
            Blend One One
            ZWrite Off

            CGPROGRAM
            #pragma vertex vert_add
            #pragma fragment frag_add
            #pragma multi_compile_fwdadd
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"

            sampler2D _MainTex;
            float4    _MainTex_ST;
            half4     _Color;
            half4     _LitColor;
            half      _LightCelStep;

            struct appdata_a
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv     : TEXCOORD0;
            };

            struct v2f_a
            {
                float4 pos : SV_POSITION;
                half2  uv  : TEXCOORD0;
                half3  wN  : TEXCOORD1;
                float3 wP  : TEXCOORD2;
                UNITY_LIGHTING_COORDS(3, 4)
                UNITY_FOG_COORDS(5)
            };

            v2f_a vert_add(appdata_a v)
            {
                v2f_a o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv  = half2(TRANSFORM_TEX(v.uv, _MainTex));
                o.wN  = half3(UnityObjectToWorldNormal(v.normal));
                o.wP  = mul(unity_ObjectToWorld, v.vertex).xyz;
                UNITY_TRANSFER_LIGHTING(o, v.uv);
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            float4 frag_add(v2f_a i) : SV_Target
            {
                half4 tex = half4(tex2D(_MainTex, float2(i.uv))) * _Color;
                half3 N   = normalize(i.wN);

                #ifdef USING_DIRECTIONAL_LIGHT
                half3 L = half3(_WorldSpaceLightPos0.xyz);
                #else
                half3 L = half3(normalize(_WorldSpaceLightPos0.xyz - i.wP));
                #endif

                UNITY_LIGHT_ATTENUATION(atten, i, i.wP);

                half NdotL = saturate(dot(N, L));
                half cell  = step(_LightCelStep, NdotL);
                half3 add  = tex.rgb * half3(_LightColor0.rgb) * half(atten) * cell;
                half3 col  = lerp(add, add * _LitColor.rgb, cell);

                float4 output = float4(col, 0);
                UNITY_APPLY_FOG(i.fogCoord, output);
                return output;
            }
            ENDCG
        }

        Pass
        {
            Name "SHADOW_CASTER"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual

            CGPROGRAM
            #pragma vertex vs
            #pragma fragment fs
            #pragma multi_compile_shadowcaster
            #include "UnityCG.cginc"
            struct v2fs { V2F_SHADOW_CASTER; };
            v2fs vs(appdata_base v) { v2fs o; TRANSFER_SHADOW_CASTER_NORMALOFFSET(o) return o; }
            float4 fs(v2fs i) : SV_Target { SHADOW_CASTER_FRAGMENT(i) }
            ENDCG
        }
    }
    FallBack "Diffuse"
}
