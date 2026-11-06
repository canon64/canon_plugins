Shader "Canon/MainGameKaleidoscope/ScreenComposite"
{
    Properties
    {
        _MainTex ("Original screen", 2D) = "black" {}
        _SourceTex ("Subcamera subject", 2D) = "black" {}
        _ForegroundTex ("Main camera characters", 2D) = "black" {}
        _ForegroundRightTex ("Right eye characters", 2D) = "black" {}
        _AfterimageTex ("Afterimage overlay", 2D) = "black" {}
        _BaseColor ("Background", Color) = (0.02,0.01,0.07,1)
        _Strength ("Strength", Range(0,1)) = 0
        _Segments ("Segments", Range(2,32)) = 8
        _Rotation ("Rotation", Float) = 0
        _Zoom ("Zoom", Float) = 1
        _CenterX ("Center X", Range(0,1)) = .5
        _CenterY ("Center Y", Range(0,1)) = .5
        _SourceZoom ("Source Zoom", Float) = 1
        _SourceCenterX ("Source Center X", Range(0,1)) = .5
        _SourceCenterY ("Source Center Y", Range(0,1)) = .5
        _SourceRotation ("Source Rotation", Float) = 0
        _ScreenAspect ("Screen Aspect", Float) = 1
        _Hue ("Hue", Float) = 0
        _Saturation ("Saturation", Float) = 1
        _Brightness ("Brightness", Float) = 1
        _Swirl ("Swirl", Float) = 0
        _Wave ("Wave", Float) = 0
        _WaveSpeed ("Wave Speed", Float) = 1
        _Repeat ("Radial Repeat", Float) = 1
        _Chromatic ("Chromatic", Float) = 0
        _VignetteStrength ("Vignette Strength", Range(0,1)) = .85
        _VignetteInner ("Vignette Inner Radius", Float) = .25
        _VignetteOuter ("Vignette Outer Radius", Float) = .75
        _VignetteCenterX ("Vignette Center X", Range(0,1)) = .5
        _VignetteCenterY ("Vignette Center Y", Range(0,1)) = .5
        _VignetteColor ("Vignette Color", Color) = (0,0,0,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert_img
            #pragma fragment frag
            #pragma multi_compile_instancing
            // Custom names keep XR variants from being stripped by this standalone
            // bundle project's non-VR player settings. The material selects the mode.
            #pragma multi_compile __ CANON_STEREO_DOUBLE_WIDE CANON_STEREO_INSTANCED
            #if defined(CANON_STEREO_DOUBLE_WIDE)
                #define UNITY_SINGLE_PASS_STEREO 1
            #elif defined(CANON_STEREO_INSTANCED)
                #define STEREO_INSTANCING_ON 1
            #endif
            #include "UnityCG.cginc"

            UNITY_DECLARE_SCREENSPACE_TEXTURE(_MainTex);
            float4 _MainTex_ST;
            sampler2D _SourceTex, _ForegroundTex, _ForegroundRightTex, _AfterimageTex;
            float _StereoForeground;
            float4 _PatternEyeOffset;
            float4 _BaseColor;
            float _Strength, _Segments, _Rotation, _Zoom, _CenterX, _CenterY;
            float _SourceZoom, _SourceCenterX, _SourceCenterY, _SourceRotation, _ScreenAspect;
            float _Hue, _Saturation, _Brightness, _Swirl, _Wave, _WaveSpeed, _Repeat, _Chromatic;
            float _VignetteStrength;
            float _VignetteInner, _VignetteOuter, _VignetteCenterX, _VignetteCenterY;
            float4 _VignetteColor;

            float3 HueShift(float3 color, float shift)
            {
                float a = shift * 6.2831853;
                float c = cos(a), s = sin(a);
                float3 axis = float3(.5773503,.5773503,.5773503);
                return color*c + cross(axis,color)*s + axis*dot(axis,color)*(1-c);
            }

            float2 MirrorUv(float2 uv)
            {
                return 1 - abs(frac(uv * .5) * 2 - 1);
            }

            float4 SampleRepeatedSource(float2 uv)
            {
                float4 result = tex2D(_SourceTex, MirrorUv(uv));
                if (result.a > .25) return result;

                // Extend the captured subject into transparent areas instead of
                // repeating the solid background color as a circular band.
                float2 center = float2(.5, .5);
                for (int index = 1; index <= 12; ++index)
                {
                    float4 candidate = tex2D(_SourceTex, MirrorUv(lerp(uv, center, index / 12.0)));
                    if (candidate.a > result.a) result = candidate;
                    if (result.a > .25) return result;
                }

                float4 candidate = tex2D(_SourceTex, float2(.25, .5));
                if (candidate.a > result.a) result = candidate;
                candidate = tex2D(_SourceTex, float2(.75, .5));
                if (candidate.a > result.a) result = candidate;
                candidate = tex2D(_SourceTex, float2(.5, .25));
                if (candidate.a > result.a) result = candidate;
                candidate = tex2D(_SourceTex, float2(.5, .75));
                if (candidate.a > result.a) result = candidate;
                return result;
            }

            float4 frag(v2f_img i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                // Pattern coordinates span 0..1 independently in each eye. Only
                // the original scene is a packed/array stereo screen texture.
                float2 screenUv = i.uv;
                float2 eyeOffset = _PatternEyeOffset.xy;
                #if defined(UNITY_SINGLE_PASS_STEREO) || defined(UNITY_STEREO_INSTANCING_ENABLED) || defined(UNITY_STEREO_MULTIVIEW_ENABLED)
                if (_StereoForeground > .5 && unity_StereoEyeIndex == 1)
                    eyeOffset = _PatternEyeOffset.zw;
                #endif
                // Center the pattern on the headset's forward ray, not the
                // midpoint of its asymmetric per-eye render texture.
                float2 patternUv = screenUv - eyeOffset;
                float2 sceneUv = UnityStereoScreenSpaceUVAdjust(screenUv, _MainTex_ST);
                float3 original = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_MainTex, sceneUv).rgb;
                float aspect = max(.01, _ScreenAspect);
                float2 aspectScale = float2(aspect, 1);
                float2 p = (patternUv - float2(_CenterX, _CenterY)) * aspectScale / max(.25, _Zoom);
                float radius = length(p);
                float angle = atan2(p.y,p.x) + _Rotation + radius * _Swirl;
                angle += sin(radius * 20 - _Time.y * _WaveSpeed * 6.2831853) * _Wave;
                float sector = 6.2831853 / max(2, _Segments);
                angle = abs(frac(angle / sector) * sector - sector * .5);
                float repeatCount = max(1, _Repeat);
                float radialPhase = frac(radius * repeatCount * .5) * 2;
                float r = (1 - abs(radialPhase - 1)) / repeatCount;
                float2 sourceOffset = float2(cos(angle),sin(angle)) * r
                    + (float2(_CenterX, _CenterY) - float2(_SourceCenterX, _SourceCenterY)) * aspectScale;
                float sourceCos = cos(_SourceRotation), sourceSin = sin(_SourceRotation);
                float2 sourceUv = .5 + float2(sourceOffset.x * sourceCos - sourceOffset.y * sourceSin,
                    sourceOffset.x * sourceSin + sourceOffset.y * sourceCos) / (aspectScale * max(.25, _SourceZoom));
                float inside = step(0, sourceUv.x) * step(sourceUv.x, 1) * step(0, sourceUv.y) * step(sourceUv.y, 1);
                float2 chromatic = float2(_Chromatic, 0);
                float repeated = step(1.001, _Repeat);
                float4 sampleCenter = repeated > 0
                    ? SampleRepeatedSource(sourceUv) : tex2D(_SourceTex, saturate(sourceUv));
                float4 sampleRed = tex2D(_SourceTex, repeated > 0
                    ? MirrorUv(sourceUv + chromatic) : saturate(sourceUv + chromatic));
                float4 sampleBlue = tex2D(_SourceTex, repeated > 0
                    ? MirrorUv(sourceUv - chromatic) : saturate(sourceUv - chromatic));
                float3 color;
                color.r = repeated > 0
                    ? (sampleRed.a > .25 ? sampleRed.r : sampleCenter.r) : sampleRed.r;
                color.g = sampleCenter.g;
                color.b = repeated > 0
                    ? (sampleBlue.a > .25 ? sampleBlue.b : sampleCenter.b) : sampleBlue.b;
                color = HueShift(color, _Hue);
                float gray = dot(color, float3(.2126,.7152,.0722));
                color = lerp(gray.xxx, color, _Saturation) * _Brightness;
                float3 background = lerp(_BaseColor.rgb, color,
                    saturate(sampleCenter.a * lerp(inside, 1, repeated)));
                float4 foreground = tex2D(_ForegroundTex, screenUv);
                #if defined(UNITY_SINGLE_PASS_STEREO) || defined(UNITY_STEREO_INSTANCING_ENABLED) || defined(UNITY_STEREO_MULTIVIEW_ENABLED)
                if (_StereoForeground > .5 && unity_StereoEyeIndex == 1)
                    foreground = tex2D(_ForegroundRightTex, screenUv);
                #endif
                float3 composite = lerp(background, original, saturate(foreground.a));
                // Measure in normalized screen space so the vignette expands with
                // the display width instead of remaining a fixed pixel-space circle.
                float vignetteRadius = length((patternUv - float2(_VignetteCenterX, _VignetteCenterY)) * 2);
                float vignetteMask = saturate(_VignetteStrength)
                    * smoothstep(_VignetteInner, max(_VignetteInner + .001, _VignetteOuter), vignetteRadius);
                composite = lerp(composite, _VignetteColor.rgb, vignetteMask);
                float3 fadedScene = lerp(original, composite, saturate(_Strength));
                float4 afterimage = tex2D(_AfterimageTex, screenUv);
                return float4(lerp(fadedScene, afterimage.rgb, saturate(afterimage.a)), 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
