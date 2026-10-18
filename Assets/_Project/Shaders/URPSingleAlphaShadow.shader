Shader "Custom/URPSingleAlphaShadow"
{
    Properties
    {
        [MainTexture] MainTex ("Sprite Texture", 2D) = "white" {}
        TintColor ("Tint Color", Color) = (1, 1, 1, 1)
        StencilRefValue ("Stencil Reference", Int) = 145
        AlphaClipThreshold ("Alpha Clip Threshold", Range(0, 1)) = 0.05
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent-1"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Cull Off
            Lighting Off
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            Stencil
            {
                Ref [StencilRefValue]
                Comp NotEqual
                Pass Replace
            }

            HLSLPROGRAM
            #pragma vertex VertexProgram
            #pragma fragment FragmentProgram

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct VertexInput
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct VertexOutput
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            sampler2D MainTex;
            float4 TintColor;
            float AlphaClipThreshold;

            VertexOutput VertexProgram(VertexInput inputData)
            {
                VertexOutput outputData;
                outputData.positionCS = TransformObjectToHClip(inputData.positionOS.xyz);
                outputData.uv = inputData.uv;
                outputData.color = inputData.color;
                
                return outputData;
            }

            half4 FragmentProgram(VertexOutput inputData) : SV_Target
            {
                half4 texColor = tex2D(MainTex, inputData.uv);
                half4 finalColor = texColor * inputData.color * TintColor;
                
                if (finalColor.a <= AlphaClipThreshold)
                {
                    discard;
                }
                
                return finalColor;
            }
            ENDHLSL
        }
    }
}