Shader "JOJO/Shaders/Cartoon_RiverWater"
{
    Properties
    {
        [Header(Water Colors)]
        _ShallowColor ("Shallow Color", Color) = (0.3, 0.85, 0.9, 0.6)
        _DeepColor ("Deep Color", Color) = (0.05, 0.2, 0.6, 0.9)
        _DepthDistance ("Depth Distance", Range(0.1, 30)) = 5.0
        _DepthExponent ("Depth Exponent", Range(0.1, 5)) = 1.5
        [Header(Foam Shore)]
        _FoamColor ("Foam Color", Color) = (1, 1, 1, 1)
        _FoamDistance ("Foam Width", Range(0, 5)) = 1.0
        _FoamNoiseScale ("Foam Noise Scale", Range(0.1, 50)) = 10.0
        _FoamNoiseSpeed ("Foam Noise Speed", Range(0, 2)) = 0.3
        _FoamCutoff ("Foam Cutoff", Range(0, 1)) = 0.5
        _FoamSoftness ("Foam Softness", Range(0, 0.5)) = 0.1
        [Header(Surface Waves)]
        _WaveNoiseTex ("Wave Noise Texture", 2D) = "white" {}
        _WaveScale ("Wave Scale", Range(0.01, 1)) = 0.1
        _WaveSpeed ("Wave Speed", Range(0, 1)) = 0.1
        _WaveStrength ("Wave Distortion", Range(0, 0.1)) = 0.02
        _WaveColor ("Wave Highlight Color", Color) = (0.6, 0.9, 1.0, 0.3)
        _WaveCutoff ("Wave Cutoff", Range(0, 1)) = 0.6
        [Header(Refraction)]
        _RefractionStrength ("Refraction Strength", Range(0, 0.2)) = 0.05
        [Header(Specular)]
        _SpecColor2 ("Specular Color", Color) = (1, 1, 1, 1)
        _SpecPower ("Specular Power", Range(1, 500)) = 200
        _SpecIntensity ("Specular Intensity", Range(0, 5)) = 1.5
        [Header(Rim Foam)]
        _RimColor ("Rim Color", Color) = (0.5, 0.8, 1.0, 1)
        _RimPower ("Rim Power", Range(0.5, 10)) = 3.0
        _RimIntensity ("Rim Intensity", Range(0, 2)) = 0.5
    }
    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }
        GrabPass { "_WaterGrabTex" }
        Pass
        {
            Name "TOON_WATER"
            Tags { "LightMode" = "ForwardBase" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite On
            Cull Back
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_fwdbase
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float3 worldPos : TEXCOORD2;
                float4 screenPos : TEXCOORD3;
                float4 grabPos : TEXCOORD4;
                float3 worldViewDir : TEXCOORD5;
                float eyeDepth : TEXCOORD6;
                UNITY_FOG_COORDS(7)
            };
            // Textures
            sampler2D _WaveNoiseTex;
            float4 _WaveNoiseTex_ST;
            sampler2D _WaterGrabTex;
            sampler2D _CameraDepthTexture;
            // Colors
            fixed4 _ShallowColor;
            fixed4 _DeepColor;
            fixed4 _FoamColor;
            fixed4 _WaveColor;
            fixed4 _SpecColor2;
            fixed4 _RimColor;
            // Parameters
            float _DepthDistance;
            float _DepthExponent;
            float _FoamDistance;
            float _FoamNoiseScale;
            float _FoamNoiseSpeed;
            float _FoamCutoff;
            float _FoamSoftness;
            float _WaveScale;
            float _WaveSpeed;
            float _WaveStrength;
            float _WaveCutoff;
            float _RefractionStrength;
            float _SpecPower;
            float _SpecIntensity;
            float _RimPower;
            float _RimIntensity;
            // ---- Procedural noise (no texture needed for foam) ----
            float2 hash22(float2 p)
            {
                p = float2(dot(p, float2(127.1, 311.7)),
                           dot(p, float2(269.5, 183.3)));
                return -1.0 + 2.0 * frac(sin(p) * 43758.5453123);
            }
            float gradientNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float n00 = dot(hash22(i + float2(0, 0)), f - float2(0, 0));
                float n10 = dot(hash22(i + float2(1, 0)), f - float2(1, 0));
                float n01 = dot(hash22(i + float2(0, 1)), f - float2(0, 1));
                float n11 = dot(hash22(i + float2(1, 1)), f - float2(1, 1));
                return lerp(lerp(n00, n10, u.x), lerp(n01, n11, u.x), u.y) * 0.5 + 0.5;
            }
            // Layered noise for organic foam
            float foamNoise(float2 uv, float time)
            {
                float n = 0;
                n += gradientNoise(uv * 1.0 + time * 0.8) * 0.5;
                n += gradientNoise(uv * 2.3 - time * 0.6) * 0.3;
                n += gradientNoise(uv * 4.7 + time * 1.1) * 0.2;
                return n;
            }
            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldViewDir = normalize(UnityWorldSpaceViewDir(o.worldPos));
                o.screenPos = ComputeScreenPos(o.pos);
                o.grabPos = ComputeGrabScreenPos(o.pos);
                COMPUTE_EYEDEPTH(o.eyeDepth);
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                float3 normal = normalize(i.worldNormal);
                float3 viewDir = normalize(i.worldViewDir);
                float ndotv = saturate(dot(normal, viewDir));
                // ============================================
                // DEPTH
                // ============================================
                float2 screenUV = i.screenPos.xy / i.screenPos.w;
                float sceneDepthRaw = tex2D(_CameraDepthTexture, screenUV).r;
                float sceneDepth = LinearEyeDepth(sceneDepthRaw);
                float waterDepth = max(0, sceneDepth - i.eyeDepth);
                float depthFactor = saturate(pow(waterDepth / _DepthDistance, _DepthExponent));
                // ============================================
                // REFRACTION (GrabPass)
                // ============================================
                float2 grabUV = i.grabPos.xy / i.grabPos.w;
                // Distort based on surface normal and depth
                float2 refrOffset = normal.xz * _RefractionStrength * saturate(waterDepth);
                fixed3 refrColor = tex2D(_WaterGrabTex, grabUV + refrOffset).rgb;
                // ============================================
                // WATER COLOR (depth-based blend)
                // ============================================
                fixed3 waterColor = lerp(_ShallowColor.rgb, _DeepColor.rgb, depthFactor);
                float waterAlpha = lerp(_ShallowColor.a, _DeepColor.a, depthFactor);
                // Blend water color with refracted background
                fixed3 col = lerp(refrColor, waterColor, waterAlpha);
                // ============================================
                // SURFACE WAVES (scrolling noise pattern)
                // ============================================
                float2 waveUV1 = i.worldPos.xz * _WaveScale + _Time.y * _WaveSpeed * float2(1, 0.5);
                float2 waveUV2 = i.worldPos.xz * _WaveScale * 0.8 + _Time.y * _WaveSpeed * float2(-0.5, 0.7);
                float wave1 = tex2D(_WaveNoiseTex, waveUV1).r;
                float wave2 = tex2D(_WaveNoiseTex, waveUV2).r;
                float waveCombined = (wave1 + wave2) * 0.5;
                // Toon-style step for wave highlights
                float waveHighlight = smoothstep(_WaveCutoff, _WaveCutoff + 0.05, waveCombined);
                col = lerp(col, _WaveColor.rgb, waveHighlight * _WaveColor.a * (1.0 - depthFactor * 0.5));
                // ============================================
                // SHORE FOAM (depth intersection)
                // ============================================
                float foamMask = 1.0 - saturate(waterDepth / _FoamDistance);
                // Procedural noise for foam edge
                float2 foamUV = i.worldPos.xz * _FoamNoiseScale;
                float foamTime = _Time.y * _FoamNoiseSpeed;
                float noise = foamNoise(foamUV, foamTime);
                // Noisy foam with smooth cutoff
                float noisyFoam = foamMask * noise;
                float foamLine = smoothstep(_FoamCutoff - _FoamSoftness, _FoamCutoff + _FoamSoftness, noisyFoam);
                // Sharp inner foam line (solid edge closest to intersection)
                float solidEdge = smoothstep(0.0, 0.1, foamMask) * step(0.7, foamMask);
                // Combine foam
                float finalFoam = max(foamLine, solidEdge);
                col = lerp(col, _FoamColor.rgb, finalFoam * _FoamColor.a);
                // ============================================
                // SPECULAR (toon-style)
                // ============================================
                float3 lightDir = normalize(_WorldSpaceLightPos0.xyz);
                float3 halfDir = normalize(lightDir + viewDir);
                float ndoth = saturate(dot(normal, halfDir));
                float spec = pow(ndoth, _SpecPower) * _SpecIntensity;
                // Toon step
                spec = smoothstep(0.3, 0.35, spec);
                col += _SpecColor2.rgb * spec * _LightColor0.rgb;
                // ============================================
                // RIM LIGHT
                // ============================================
                float rim = pow(1.0 - ndotv, _RimPower) * _RimIntensity;
                col += _RimColor.rgb * rim;
                // ============================================
                // FINAL ALPHA
                // ============================================
                float alpha = lerp(_ShallowColor.a, _DeepColor.a, depthFactor);
                alpha = max(alpha, finalFoam * _FoamColor.a);
                alpha = max(alpha, waveHighlight * _WaveColor.a * 0.5);
                alpha = saturate(alpha + rim * 0.3);
                UNITY_APPLY_FOG(i.fogCoord, col);
                return fixed4(col, alpha);
            }
            ENDCG
        }
    }
    FallBack "Transparent/Diffuse"
}
