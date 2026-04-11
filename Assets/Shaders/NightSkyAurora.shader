Shader "Custom/NightSkyAurora"
{
    Properties
    {
        [Header(Sky Gradient)]
        [Space(5)]
        _SkyTopColor     ("Top Color",        Color) = (0.005, 0.005, 0.03, 1)
        _SkyMidColor     ("Mid Color",        Color) = (0.01,  0.01,  0.05, 1)
        _SkyHorizonColor ("Horizon Color",    Color) = (0.04,  0.02,  0.07, 1)
        _SkyGradientExp  ("Gradient Exponent", Range(0.25, 8)) = 2.0

        [Header(Stars)]
        [Space(5)]
        _StarDensity       ("Density",                  Range(10, 400))      = 120
        _StarBrightness    ("Brightness",               Range(0, 5))         = 1.8
        _StarSizeMin       ("Min Size",                 Range(0.0001, 0.003))= 0.0005
        _StarSizeMax       ("Max Size",                 Range(0.001, 0.015)) = 0.005
        _StarTwinkleSpeed  ("Twinkle Speed",            Range(0, 10))        = 3.0
        _StarTwinkleAmount ("Twinkle Amount",           Range(0, 1))         = 0.6
        _StarColorVar      ("Color Temperature Variation", Range(0, 1))      = 0.5
        _StarTint          ("Tint",                     Color)               = (1, 1, 1, 1)
        _StarHorizonFade   ("Horizon Fade Height",      Range(0, 0.5))       = 0.1

        [Header(Moon)]
        [Space(5)]
        [NoScaleOffset] _MoonTex ("Moon Texture", 2D) = "white" {}
        _MoonDirX       ("Direction X",       Range(-1, 1))    = 0.25
        _MoonDirY       ("Direction Y",       Range(0.01, 1))  = 0.55
        _MoonDirZ       ("Direction Z",       Range(-1, 1))    = 0.35
        _MoonSize       ("Angular Size",      Range(0.01, 0.25)) = 0.07
        _MoonSharpness  ("Edge Sharpness",    Range(0.5, 50))  = 25
        _MoonBrightness ("Brightness",        Range(0, 10))    = 2.5
        _MoonColor      ("Tint",              Color)           = (1, 0.97, 0.9, 1)
        _MoonRotation   ("Texture Rotation",  Range(0, 360))   = 0
        _MoonTexScale   ("Texture Scale",     Range(0.5, 20))  = 8
        _MoonTexStrength("Texture Strength",  Range(0, 1))     = 1
        _MoonHaloSize   ("Halo Spread",       Range(0.01, 0.6))= 0.18
        _MoonHaloInt    ("Halo Intensity",    Range(0, 3))     = 0.45
        _MoonHaloFall   ("Halo Falloff",      Range(1, 20))    = 4
        _MoonHaloColor  ("Halo Color",        Color)           = (0.12, 0.12, 0.25, 1)

        [Header(Aurora Borealis)]
        [Space(5)]
        [Toggle(_AURORA_ON)] _AuroraEnabled ("Enabled", Float) = 1
        _AuroraIntensity  ("Intensity",            Range(0, 5))   = 1.3
        _AuroraSpeed      ("Animation Speed",      Range(0, 5))   = 0.4
        _AuroraScale      ("Horizontal Scale",     Range(0.5, 10))= 3.0
        _AuroraColor1     ("Color 1 (Green)",      Color)         = (0.05, 0.85, 0.35, 1)
        _AuroraColor2     ("Color 2 (Blue)",       Color)         = (0.1, 0.35, 0.95, 1)
        _AuroraColor3     ("Color 3 (Purple)",     Color)         = (0.55, 0.1, 0.85, 1)
        _AuroraColorSpeed ("Color Shift Speed",    Range(0, 2))   = 0.25
        _AuroraHeight     ("Height",               Range(0.05, 0.75)) = 0.35
        _AuroraBandWidth  ("Band Width",           Range(0.02, 0.4))  = 0.14
        _AuroraCoverage   ("Coverage",             Range(0, 1))       = 0.55
        _AuroraWaviness   ("Waviness",             Range(0, 15))      = 5.0
        _AuroraRayStr     ("Vertical Ray Strength", Range(0, 1.5))    = 0.7
        _AuroraLayers     ("Layers (1-5)",         Range(1, 5))       = 3
        _AuroraFlicker    ("Flicker",              Range(0, 1))       = 0.25

        [Header(Atmosphere and Exposure)]
        [Space(5)]
        _Exposure         ("Exposure",             Range(0.1, 5)) = 1.0
        _HorizonGlow      ("Horizon Glow",         Range(0, 3))   = 0.4
        _HorizonGlowColor ("Horizon Glow Color",   Color)         = (0.03, 0.02, 0.08, 1)
        _HorizonGlowFall  ("Horizon Glow Falloff",  Range(1, 20)) = 5
    }

    SubShader
    {
        Tags
        {
            "Queue"          = "Background"
            "RenderType"     = "Background"
            "PreviewType"    = "Skybox"
            "RenderPipeline" = "UniversalPipeline"
        }

        Cull Off
        ZWrite Off

        Pass
        {
            Name "NightSkyAurora"

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target   3.5
            #pragma shader_feature_local _AURORA_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // --------------------------------------------------------
            //  Structs
            // --------------------------------------------------------
            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 viewDirWS  : TEXCOORD0;
            };

            // --------------------------------------------------------
            //  Textures
            // --------------------------------------------------------
            TEXTURE2D(_MoonTex);
            SAMPLER(sampler_MoonTex);

            // --------------------------------------------------------
            //  Material CBUFFER  (SRP Batcher compatible)
            // --------------------------------------------------------
            CBUFFER_START(UnityPerMaterial)
                half4  _SkyTopColor;
                half4  _SkyMidColor;
                half4  _SkyHorizonColor;
                float  _SkyGradientExp;

                float  _StarDensity;
                float  _StarBrightness;
                float  _StarSizeMin;
                float  _StarSizeMax;
                float  _StarTwinkleSpeed;
                float  _StarTwinkleAmount;
                float  _StarColorVar;
                half4  _StarTint;
                float  _StarHorizonFade;

                float  _MoonDirX;
                float  _MoonDirY;
                float  _MoonDirZ;
                float  _MoonSize;
                float  _MoonSharpness;
                float  _MoonBrightness;
                half4  _MoonColor;
                float  _MoonRotation;
                float  _MoonTexScale;
                float  _MoonTexStrength;
                float  _MoonHaloSize;
                float  _MoonHaloInt;
                float  _MoonHaloFall;
                half4  _MoonHaloColor;

                float  _AuroraEnabled;
                float  _AuroraIntensity;
                float  _AuroraSpeed;
                float  _AuroraScale;
                half4  _AuroraColor1;
                half4  _AuroraColor2;
                half4  _AuroraColor3;
                float  _AuroraColorSpeed;
                float  _AuroraHeight;
                float  _AuroraBandWidth;
                float  _AuroraCoverage;
                float  _AuroraWaviness;
                float  _AuroraRayStr;
                float  _AuroraLayers;
                float  _AuroraFlicker;

                float  _Exposure;
                float  _HorizonGlow;
                half4  _HorizonGlowColor;
                float  _HorizonGlowFall;
            CBUFFER_END

            // ================================================================
            //  HASH  &  NOISE
            // ================================================================
            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);

                float a = Hash21(i);
                float b = Hash21(i + float2(1, 0));
                float c = Hash21(i + float2(0, 1));
                float d = Hash21(i + float2(1, 1));

                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float FBM(float2 p, int octaves)
            {
                float v = 0.0;
                float a = 0.5;
                float f = 1.0;
                for (int i = 0; i < octaves; i++)
                {
                    v += a * ValueNoise(p * f);
                    f *= 2.0;
                    a *= 0.5;
                }
                return v;
            }

            float3 SafeNormalizeDir(float3 v)
            {
                float lenSq = dot(v, v);
                if (lenSq < 1e-8 || any(!isfinite(v)))
                    return float3(0.0, 1.0, 0.0);

                return v * rsqrt(lenSq);
            }

            // ================================================================
            //  PROJECTION HELPERS
            // ================================================================
            float2 DirectionToCubeUv(float3 dir, out float face)
            {
                float3 a = abs(dir);
                float2 uv;

                if (a.x >= a.y && a.x >= a.z)
                {
                    face = dir.x > 0.0 ? 0.0 : 1.0;
                    uv = dir.x > 0.0 ? float2(-dir.z, dir.y) / a.x
                                      : float2( dir.z, dir.y) / a.x;
                }
                else if (a.y >= a.x && a.y >= a.z)
                {
                    face = dir.y > 0.0 ? 2.0 : 3.0;
                    uv = dir.y > 0.0 ? float2(dir.x, -dir.z) / a.y
                                      : float2(dir.x,  dir.z) / a.y;
                }
                else
                {
                    face = dir.z > 0.0 ? 4.0 : 5.0;
                    uv = dir.z > 0.0 ? float2( dir.x, dir.y) / a.z
                                      : float2(-dir.x, dir.y) / a.z;
                }

                return uv * 0.5 + 0.5;
            }

            float2 DirectionToDiscPlane(float3 dir, float3 centerDir)
            {
                float3 upRef = abs(centerDir.y) > 0.99 ? float3(1, 0, 0) : float3(0, 1, 0);
                float3 right = normalize(cross(upRef, centerDir));
                float3 up    = cross(centerDir, right);
                float  z     = max(dot(dir, centerDir), 0.0001);
                return float2(dot(dir, right), dot(dir, up)) / z;
            }

            // ================================================================
            //  STAR LAYER
            // ================================================================
            half3 StarLayer(float2 uv, float density, float sizeMul, float seed)
            {
                float2 gridUv = uv * density;
                float2 cell   = floor(gridUv);
                float2 local  = frac(gridUv) - 0.5;
                float  rnd    = Hash21(cell + seed);
                float  rnd2   = Hash21(cell + seed + 127.0);

                float size = lerp(_StarSizeMin, _StarSizeMax, rnd2) * density * sizeMul * 0.25;
                float star = smoothstep(size, 0.0, length(local));
                star *= step(0.25, rnd);

                float twinkle = sin(_Time.y * _StarTwinkleSpeed * (rnd * 4.0 + 1.0)
                                    + rnd2 * 6.2831);
                twinkle = lerp(1.0, 0.5 + 0.5 * twinkle, _StarTwinkleAmount);
                star *= twinkle;

                half3 warm  = half3(1.0, 0.85, 0.6);
                half3 cool  = half3(0.7, 0.85, 1.0);
                half3 starColor = lerp(half3(1, 1, 1), lerp(warm, cool, rnd), _StarColorVar);
                starColor *= _StarTint.rgb;

                return starColor * star * (0.4 + rnd2 * 0.6);
            }

            // ================================================================
            //  STARS
            // ================================================================
            half3 ComputeStars(float3 dir)
            {
                float3 n = normalize(dir);

                float cubeFace;
                float2 cubeUv = DirectionToCubeUv(n, cubeFace);
                float2 starUv = cubeUv + cubeFace * 0.173;

                half3 stars = half3(0, 0, 0);
                stars += StarLayer(starUv + float2(0.31, 0.12), _StarDensity,        1.0,  13.1);
                stars += StarLayer(starUv + float2(0.47, 0.73), _StarDensity * 1.41, 0.85, 28.5);
                stars += StarLayer(starUv + float2(0.09, 0.44), _StarDensity * 1.88, 0.65, 61.9);
                stars *= _StarBrightness;

                float horizFade = smoothstep(0.0, max(_StarHorizonFade, 0.001), n.y);
                return stars * horizFade;
            }

            // ================================================================
            //  MOON
            // ================================================================
            half4 ComputeMoon(float3 dir)
            {
                float3 moonDir = normalize(float3(_MoonDirX, max(_MoonDirY, 0.001), _MoonDirZ));
                float3 view    = normalize(dir);

                // gnomonic disc projection
                float2 moonPlane  = DirectionToDiscPlane(view, moonDir);
                float2 moonDiscUv = moonPlane / max(_MoonSize, 0.00001) * 0.5 + 0.5;

                // texture rotation
                float rad = _MoonRotation * PI / 180.0;
                float cr  = cos(rad);
                float sr  = sin(rad);
                float2 centered = moonDiscUv - 0.5;
                float2 rotated  = float2(centered.x * cr - centered.y * sr,
                                          centered.x * sr + centered.y * cr);

                // texture UV with scale control
                float2 moonUv = rotated / max(_MoonTexScale, 0.0001) + 0.5;

                float moonUvMask = step(0.0, moonUv.x) * step(moonUv.x, 1.0)
                                 * step(0.0, moonUv.y) * step(moonUv.y, 1.0);

                float moonRadius   = length(centered * 2.0);
                float moonDiscMask = smoothstep(1.0, 1.0 - 2.0 / max(_MoonSharpness, 0.5), moonRadius);
                float moonCore     = pow(saturate(1.0 - moonRadius), 0.6);

                half4 tex = SAMPLE_TEXTURE2D_LOD(_MoonTex, sampler_MoonTex,
                                                 saturate(moonUv), 0);
                half3 moonTexture  = lerp(half3(1, 1, 1), tex.rgb, _MoonTexStrength);
                float moonTexAlpha = lerp(1.0, tex.a, _MoonTexStrength);

                half3 moonCoreColor   = _MoonColor.rgb * moonCore * _MoonBrightness;
                float moonTextureMask = moonUvMask * moonDiscMask * moonTexAlpha;
                half3 mColor = moonCoreColor * moonTexture * moonTextureMask;
                mColor += moonCoreColor * (1.0 - moonTextureMask);

                // halo
                float d       = dot(view, moonDir);
                float haloPow = _MoonHaloFall / max(_MoonHaloSize, 0.01);
                float halo    = pow(saturate(d), haloPow) * _MoonHaloInt;
                half3 haloCol = _MoonHaloColor.rgb * halo;

                float alpha = moonDiscMask * moonTexAlpha;
                return half4(mColor + haloCol, alpha);
            }

            // ================================================================
            //  AURORA  HELPERS
            // ================================================================
            half3 BlendThree(float t, half3 c1, half3 c2, half3 c3)
            {
                half3 a = lerp(c1, c2, saturate(t * 2.0));
                half3 b = lerp(c2, c3, saturate(t * 2.0 - 1.0));
                return lerp(a, b, step(0.5, t));
            }

            // ================================================================
            //  AURORA
            // ================================================================
            half3 ComputeAurora(float3 dir)
            {
                float3 n = normalize(dir);
                if (n.y <= 0.0)
                    return half3(0, 0, 0);

                float bandLo = _AuroraHeight - _AuroraBandWidth;
                float bandHi = _AuroraHeight + _AuroraBandWidth;

                float vertMask = smoothstep(bandLo, _AuroraHeight, n.y)
                               * smoothstep(bandHi, _AuroraHeight, n.y);

                if (vertMask < 0.001)
                    return half3(0, 0, 0);

                float time    = _Time.y * _AuroraSpeed;
                float azimuth = atan2(n.x, n.z);

                half3 result = half3(0, 0, 0);
                int   count  = clamp((int)_AuroraLayers, 1, 5);

                for (int i = 0; i < 5; i++)
                {
                    if (i >= count) break;

                    float phase = (float)i * 2.39996;
                    float h     = azimuth * _AuroraScale + phase;

                    // curtain waves
                    float wave  = sin(h * _AuroraWaviness + time * 1.3 + phase) * 0.28;
                    wave       += sin(h * _AuroraWaviness * 2.17 + time * 0.7
                                      + phase * 1.5) * 0.14;
                    wave       += sin(h * _AuroraWaviness * 4.31 + time * 2.1
                                      + phase * 0.8) * 0.07;

                    float sY = n.y + wave * _AuroraBandWidth;

                    float layerMask = smoothstep(bandLo, bandLo + _AuroraBandWidth * 0.7, sY)
                                    * smoothstep(bandHi, bandHi - _AuroraBandWidth * 0.7, sY);

                    // curtain density
                    float2 nUV    = float2(h * 0.5, n.y * 3.0);
                    float curtain = FBM(nUV + float2(time * 0.2, time * 0.05), 4);
                    curtain       = smoothstep(0.5 - _AuroraCoverage * 0.5, 0.55, curtain);

                    // vertical rays
                    float rFreq = h * 18.0 + sin(h * 3.0 + time) * 2.0;
                    float rays  = pow(0.5 + 0.5 * sin(rFreq + time * 2.5), 2.5);
                    rays        = lerp(1.0, 0.25 + 0.75 * rays, _AuroraRayStr);

                    // flicker
                    float flicker = 1.0 - _AuroraFlicker * 0.5
                        * (0.5 + 0.5 * sin(time * 5.7 + phase * 3.1 + h * 1.5));

                    // colour
                    float cp   = frac(curtain + time * _AuroraColorSpeed + phase * 0.15);
                    half3 col  = BlendThree(cp,
                                    _AuroraColor1.rgb,
                                    _AuroraColor2.rgb,
                                    _AuroraColor3.rgb);

                    float hInBand = saturate((sY - bandLo) / max(bandHi - bandLo, 0.001));
                    col = lerp(col, col * half3(0.7, 0.4, 1.4), hInBand * 0.35);

                    float intensity = layerMask * curtain * rays * flicker;
                    result += col * intensity / (float)count;
                }

                return result * _AuroraIntensity * vertMask;
            }

            // ================================================================
            //  VERTEX  &  FRAGMENT
            // ================================================================
            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS = posInputs.positionCS;

                float3 worldPos = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.viewDirWS = worldPos - _WorldSpaceCameraPos;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 dir = SafeNormalizeDir(IN.viewDirWS);

                // ---------- sky gradient ----------
                float height = saturate(dir.y);
                float t = pow(height, 1.0 / max(_SkyGradientExp, 0.01));
                half3 sky = lerp(_SkyHorizonColor.rgb, _SkyMidColor.rgb,
                                 smoothstep(0.0, 0.4, t));
                sky = lerp(sky, _SkyTopColor.rgb, smoothstep(0.4, 1.0, t));

                // below horizon
                sky = lerp(sky, _SkyHorizonColor.rgb * 0.1, saturate(-dir.y * 4.0));

                // horizon glow
                float glow = exp(-abs(dir.y) * _HorizonGlowFall) * _HorizonGlow;
                sky += _HorizonGlowColor.rgb * glow;

                // ---------- stars ----------
                half3 stars = ComputeStars(dir);

                // ---------- moon ----------
                half4 moon = ComputeMoon(dir);

                // ---------- aurora ----------
                half3 aurora = half3(0, 0, 0);
                #ifdef _AURORA_ON
                    aurora = ComputeAurora(dir);
                #endif

                // dim stars behind moon disc and bright aurora
                stars *= 1.0 - saturate(moon.w);
                stars *= 1.0 - saturate(dot(aurora, half3(0.33, 0.33, 0.33)) * 1.5);

                // ---------- compose ----------
                float3 color = (float3)sky + (float3)stars + (float3)moon.rgb + (float3)aurora;
                color *= max(_Exposure, 0.0);

                if (any(!isfinite(color)))
                    color = float3(0.0, 0.0, 0.0);

                color = max(color, 0.0);
                color = min(color, 64.0);

                return half4((half3)color, 1.0h);
            }

            ENDHLSL
        }
    }

    Fallback Off
}
