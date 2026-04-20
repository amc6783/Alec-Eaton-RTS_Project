Shader "Hidden/RTS/SelectionOutlineComposite"
{
    Properties
    {
        _MainTex("Source", 2D) = "white" {}
        _OutlineColor("OutlineColor", Color) = (0,1,0,1)
        _OutlineThickness("OutlineThickness", Float) = 2
        _DebugFillSelected("DebugFillSelected", Float) = 0
        _DebugForceFullScreen("DebugForceFullScreen", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline"="UniversalPipeline" }
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            Name "SelectionOutlineComposite"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_SelectionMaskTexGlobal);
            SAMPLER(sampler_SelectionMaskTexGlobal);

            half4 _OutlineColor;
            float _OutlineThickness;
            float _DebugFillSelected;
            float _DebugForceFullScreen;

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half SampleMask(float2 uv)
            {
                return SAMPLE_TEXTURE2D(_SelectionMaskTexGlobal, sampler_SelectionMaskTexGlobal, uv).r;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 sceneColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                if (_DebugForceFullScreen > 0.5f)
                {
                    return lerp(sceneColor, _OutlineColor, _OutlineColor.a);
                }
                half center = SampleMask(input.uv);

                float2 pixelStep = _OutlineThickness / _ScreenParams.xy;

                half neighbor = 0;
                neighbor = max(neighbor, SampleMask(input.uv + float2(pixelStep.x, 0)));
                neighbor = max(neighbor, SampleMask(input.uv + float2(-pixelStep.x, 0)));
                neighbor = max(neighbor, SampleMask(input.uv + float2(0, pixelStep.y)));
                neighbor = max(neighbor, SampleMask(input.uv + float2(0, -pixelStep.y)));
                neighbor = max(neighbor, SampleMask(input.uv + float2(pixelStep.x, pixelStep.y)));
                neighbor = max(neighbor, SampleMask(input.uv + float2(pixelStep.x, -pixelStep.y)));
                neighbor = max(neighbor, SampleMask(input.uv + float2(-pixelStep.x, pixelStep.y)));
                neighbor = max(neighbor, SampleMask(input.uv + float2(-pixelStep.x, -pixelStep.y)));

                half outlineMask = step(0.001h, neighbor) * (1.0h - step(0.001h, center));
                half fillMask = step(0.001h, center);
                half useMask = lerp(outlineMask, fillMask, step(0.5h, _DebugFillSelected));
                return lerp(sceneColor, _OutlineColor, useMask * _OutlineColor.a);
            }
            ENDHLSL
        }
    }
}
