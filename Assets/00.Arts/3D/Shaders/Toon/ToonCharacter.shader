Shader "JOJO/Toon/ToonCharacter"
{
    Properties
    {
        _MainTex        ("Albedo", 2D)                   = "white" {}
        _Color          ("Base Color", Color)            = (1,1,1,1)
        _ShadowColor    ("Shadow Color", Color)          = (0.45,0.38,0.60,1)
        _ShadowStrength ("Shadow Strength", Range(0,1))  = 0.75
        _ShadowSmooth   ("Shadow Smooth", Range(0,1.0))  = 0.12
        _ShadowThreshold("Shadow Threshold",Range(-1,1)) = 0.05
        _NormalSphere   ("Normal Sphere Blend", Range(0,1)) = 0.0
        _MidtoneColor   ("Midtone Tint", Color)          = (1.0,0.97,0.93,1)
        _HighlightColor ("Highlight Tint", Color)        = (1.0,0.98,0.92,1)
        _SpecColor2     ("Specular Color", Color)        = (1,0.98,0.90,1)
        _SpecSmooth     ("Specular Smooth", Range(0,1))  = 0.05
        _SpecThreshold  ("Specular Threshold",Range(0,1))= 0.82
        _SpecIntensity  ("Specular Intensity",Range(0,2))= 0.6
        _RimColor       ("Rim Color", Color)             = (1.0,0.75,0.4,1)
        _RimStrength    ("Rim Strength", Range(0,3))     = 1.2
        _RimThreshold   ("Rim Threshold", Range(0,1))    = 0.55
        _RimSmooth      ("Rim Smooth", Range(0,0.5))     = 0.08
        _AmbientColor   ("Ambient Color", Color)         = (0.3,0.28,0.38,1)
        _AmbientFactor  ("Ambient Factor", Range(0,1))   = 0.35
        _ShadowMaskTex  ("Shadow Mask (R=AO G=Spec B=Rim)", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 200

        Pass
        {
            Name "FORWARD"
            Tags { "LightMode"="ForwardBase" }
            Cull Back
            ZWrite On

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"

            sampler2D _MainTex;
            float4    _MainTex_ST;
            sampler2D _ShadowMaskTex;
            float4 _Color;
            float4 _ShadowColor;
            float  _ShadowStrength;
            float  _ShadowSmooth;
            float  _ShadowThreshold;
            float  _NormalSphere;
            float4 _MidtoneColor;
            float4 _HighlightColor;
            float4 _SpecColor2;
            float  _SpecSmooth;
            float  _SpecThreshold;
            float  _SpecIntensity;
            float4 _RimColor;
            float  _RimStrength;
            float  _RimThreshold;
            float  _RimSmooth;
            float4 _AmbientColor;
            float  _AmbientFactor;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos        : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 worldNormal: TEXCOORD1;
                float3 worldPos   : TEXCOORD2;
                SHADOW_COORDS(3)
                UNITY_FOG_COORDS(4)
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos         = UnityObjectToClipPos(v.vertex);
                o.uv          = TRANSFORM_TEX(v.uv, _MainTex);
                float3 worldPos   = mul(unity_ObjectToWorld, v.vertex).xyz;
                float3 objCenter  = mul(unity_ObjectToWorld, float4(0,0,0,1)).xyz;
                float3 sphereN    = normalize(worldPos - objCenter);
                float3 meshN      = UnityObjectToWorldNormal(v.normal);
                o.worldNormal     = normalize(lerp(meshN, sphereN, _NormalSphere));
                o.worldPos        = worldPos;
                TRANSFER_SHADOW(o);
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float4 albedo   = tex2D(_MainTex, i.uv) * _Color;
                float3 mask     = tex2D(_ShadowMaskTex, i.uv).rgb;
                float  aoMask   = mask.r;
                float  specMask = mask.g;
                float  rimMask  = mask.b;

                float3 N = normalize(i.worldNormal);
                float3 L = normalize(_WorldSpaceLightPos0.xyz);
                float3 V = normalize(_WorldSpaceCameraPos - i.worldPos);
                float3 H = normalize(L + V);
                float  atten = SHADOW_ATTENUATION(i);

                float NdotL = (dot(N, L) * 0.5 + 0.5) * aoMask * atten;
                float shadowT = smoothstep(
                    _ShadowThreshold - _ShadowSmooth,
                    _ShadowThreshold + _ShadowSmooth,
                    NdotL);

                float3 ambient   = albedo.rgb * _AmbientColor.rgb * _AmbientFactor;
                float3 shadowed  = albedo.rgb * _ShadowColor.rgb * _ShadowStrength + ambient;
                float3 midtone   = albedo.rgb * _MidtoneColor.rgb;
                float3 highlight = albedo.rgb * _HighlightColor.rgb;

                float  t2 = saturate((shadowT - 0.5) * 2.0);
                float3 litColor = lerp(shadowed, lerp(midtone, highlight, t2), shadowT);
                litColor *= _LightColor0.rgb;

                float NdotH = dot(N, H);
                float specT = smoothstep(
                    _SpecThreshold - _SpecSmooth,
                    _SpecThreshold + _SpecSmooth,
                    NdotH) * specMask * atten;
                float3 specContrib = specT * _SpecColor2.rgb * _SpecIntensity;

                float NdotV = 1.0 - saturate(dot(N, V));
                float rimT  = smoothstep(
                    _RimThreshold - _RimSmooth,
                    _RimThreshold + _RimSmooth,
                    NdotV) * rimMask * saturate(dot(L, V) * -1.0 + 0.5);
                float3 rimContrib = rimT * _RimColor.rgb * _RimStrength;

                float3 finalColor = litColor + specContrib + rimContrib;
                float4 output = float4(finalColor, albedo.a);
                UNITY_APPLY_FOG(i.fogCoord, output);
                return output;
            }
            ENDCG
        }

        Pass
        {
            Name "FORWARD_ADD"
            Tags { "LightMode"="ForwardAdd" }
            Blend One One
            Cull Back
            ZWrite Off

            CGPROGRAM
            #pragma vertex vert_add
            #pragma fragment frag_add
            #pragma multi_compile_fwdadd_fullshadows
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"

            sampler2D _MainTex;
            float4    _MainTex_ST;
            float4 _Color;
            float  _ShadowSmooth;
            float  _ShadowThreshold;
            float  _NormalSphere;

            struct appdata_a
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv     : TEXCOORD0;
            };

            struct v2f_a
            {
                float4 pos        : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 worldNormal: TEXCOORD1;
                float3 worldPos   : TEXCOORD2;
                SHADOW_COORDS(3)
                UNITY_FOG_COORDS(4)
            };

            v2f_a vert_add(appdata_a v)
            {
                v2f_a o;
                o.pos         = UnityObjectToClipPos(v.vertex);
                o.uv          = TRANSFORM_TEX(v.uv, _MainTex);
                float3 worldPos   = mul(unity_ObjectToWorld, v.vertex).xyz;
                float3 objCenter  = mul(unity_ObjectToWorld, float4(0,0,0,1)).xyz;
                float3 sphereN    = normalize(worldPos - objCenter);
                float3 meshN      = UnityObjectToWorldNormal(v.normal);
                o.worldNormal     = normalize(lerp(meshN, sphereN, _NormalSphere));
                o.worldPos        = worldPos;
                TRANSFER_SHADOW(o);
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            float4 frag_add(v2f_a i) : SV_Target
            {
                float4 albedo = tex2D(_MainTex, i.uv) * _Color;
                float3 N = normalize(i.worldNormal);
                float  atten = SHADOW_ATTENUATION(i);

                float3 lightDir;
                #ifdef USING_DIRECTIONAL_LIGHT
                    lightDir = normalize(_WorldSpaceLightPos0.xyz);
                #else
                    lightDir = normalize(_WorldSpaceLightPos0.xyz - i.worldPos);
                    float dist = length(_WorldSpaceLightPos0.xyz - i.worldPos);
                    atten /= (1.0 + dist * dist * 0.1);
                #endif

                float NdotL  = dot(N, lightDir) * 0.5 + 0.5;
                float shadowT = smoothstep(
                    _ShadowThreshold - _ShadowSmooth,
                    _ShadowThreshold + _ShadowSmooth,
                    NdotL);
                float3 col = albedo.rgb * _LightColor0.rgb * shadowT * atten * 0.4;

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
            #pragma vertex vert_shadow
            #pragma fragment frag_shadow
            #pragma multi_compile_shadowcaster
            #include "UnityCG.cginc"

            struct v2f_s { V2F_SHADOW_CASTER; };

            v2f_s vert_shadow(appdata_base v)
            {
                v2f_s o;
                TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)
                return o;
            }

            float4 frag_shadow(v2f_s i) : SV_Target
            {
                SHADOW_CASTER_FRAGMENT(i)
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}
