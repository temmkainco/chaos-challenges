Shader "UI/CRTMonitor"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        
        [Header(CRT Curvature)]
        _Curvature ("Screen Curvature", Range(0.0, 1.0)) = 0.3
        _CurvatureX ("Curvature X", Range(0.0, 1.0)) = 0.3
        _CurvatureY ("Curvature Y", Range(0.0, 1.0)) = 0.3
        
        [Header(CRT Effects)]
        _ScanlineIntensity ("Scanline Intensity", Range(0.0, 1.0)) = 0.5
        _ScanlineCount ("Scanline Count", Range(100, 1000)) = 400
        _VignetteIntensity ("Vignette Intensity", Range(0.0, 2.0)) = 0.8
        _Brightness ("Brightness", Range(0.5, 2.0)) = 1.2
        _Contrast ("Contrast", Range(0.5, 3.0)) = 1.3
        
        [Header(Phosphor Effect)]
        _PhosphorGlow ("Phosphor Glow", Range(0.0, 1.0)) = 0.3
        _GlowRadius ("Glow Radius", Range(0.001, 0.01)) = 0.003
        
        [Header(Screen Distortion)]
        _NoiseIntensity ("Static Noise", Range(0.0, 0.5)) = 0.05
        _Flicker ("Flicker Intensity", Range(0.0, 0.2)) = 0.02
        
        [Header(Color Aberration)]
        _ChromaticAberration ("Chromatic Aberration", Range(0.0, 0.01)) = 0.002
        
        [Header(UI Settings)]
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "CRT_Effect"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord  : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                float2 screenPos : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;
            
            // CRT Properties
            float _Curvature;
            float _CurvatureX;
            float _CurvatureY;
            float _ScanlineIntensity;
            float _ScanlineCount;
            float _VignetteIntensity;
            float _Brightness;
            float _Contrast;
            float _PhosphorGlow;
            float _GlowRadius;
            float _NoiseIntensity;
            float _Flicker;
            float _ChromaticAberration;

            // Random function for noise
            float random(float2 co)
            {
                return frac(sin(dot(co.xy, float2(12.9898, 78.233))) * 43758.5453);
            }

            // CRT Curvature function
            float2 CRTCurvature(float2 uv)
            {
                // Center the coordinates
                uv = uv * 2.0 - 1.0;
                
                // Apply curvature offset
                float2 offset = abs(uv.yx) / float2(_CurvatureX, _CurvatureY);
                uv = uv + uv * offset * offset * _Curvature;
                
                // Return to 0-1 range
                uv = uv * 0.5 + 0.5;
                return uv;
            }

            // Vignette effect
            float Vignette(float2 uv)
            {
                uv *= 1.0 - uv.yx;
                float vig = uv.x * uv.y * 15.0;
                return pow(vig, _VignetteIntensity * 0.25);
            }

            // Scanlines effect
            float Scanlines(float2 uv)
            {
                float scanline = sin(uv.y * _ScanlineCount) * 0.5 + 0.5;
                return lerp(1.0, scanline, _ScanlineIntensity);
            }

            // Phosphor glow effect
            float3 PhosphorGlow(sampler2D tex, float2 uv)
            {
                float3 col = tex2D(tex, uv).rgb;
                
                // Sample surrounding pixels for glow
                float3 glow = float3(0, 0, 0);
                for(int x = -2; x <= 2; x++)
                {
                    for(int y = -2; y <= 2; y++)
                    {
                        float2 offset = float2(x, y) * _GlowRadius;
                        glow += tex2D(tex, uv + offset).rgb;
                    }
                }
                glow /= 25.0; // Average of 5x5 samples
                
                return lerp(col, glow, _PhosphorGlow);
            }

            // Chromatic aberration
            float3 ChromaticAberration(sampler2D tex, float2 uv)
            {
                float3 col;
                col.r = tex2D(tex, uv + float2(_ChromaticAberration, 0)).r;
                col.g = tex2D(tex, uv).g;
                col.b = tex2D(tex, uv - float2(_ChromaticAberration, 0)).b;
                return col;
            }

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.screenPos = ComputeScreenPos(OUT.vertex);
                OUT.color = v.color * _Color;
                
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                // Apply CRT curvature
                float2 curvedUV = CRTCurvature(IN.texcoord);
                
                // Check if we're outside the curved screen bounds
                if (curvedUV.x < 0.0 || curvedUV.x > 1.0 || curvedUV.y < 0.0 || curvedUV.y > 1.0)
                {
                    return float4(0, 0, 0, 0); // Outside screen bounds
                }
                
                // Sample texture with chromatic aberration
                float3 col = ChromaticAberration(_MainTex, curvedUV);
                
                // Add phosphor glow
                col = PhosphorGlow(_MainTex, curvedUV);
                
                // Apply scanlines
                float scanlines = Scanlines(curvedUV);
                col *= scanlines;
                
                // Apply vignette
                float vignette = Vignette(curvedUV);
                col *= vignette;
                
                // Add static noise
                float noise = random(curvedUV + frac(_Time.y)) * _NoiseIntensity;
                col += noise;
                
                // Add flicker effect
                float flicker = 1.0 + sin(_Time.y * 60.0) * _Flicker;
                col *= flicker;
                
                // Apply brightness and contrast
                col = pow(col * _Brightness, _Contrast);
                
                // Create final color
                float4 finalColor = float4(col, 1.0) * IN.color;
                finalColor += _TextureSampleAdd;
                
                // Apply screen edge fade
                float2 edgeFade = smoothstep(0.0, 0.05, curvedUV) * smoothstep(1.0, 0.95, curvedUV);
                finalColor.a *= edgeFade.x * edgeFade.y;

                
                #ifdef UNITY_UI_CLIP_RECT
                finalColor.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip (finalColor.a - 0.001);
                #endif

                return finalColor;
            }
            ENDCG
        }
    }
}