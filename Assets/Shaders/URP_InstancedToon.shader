Shader "Custom/URP_InstancedToon"
{
    Properties
    {
        [MainColor] _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _HighlightColor ("Highlight Color", Color) = (1, 1, 1, 1)
        _ShadowColor ("Shadow Color", Color) = (0.2, 0.2, 0.2, 1)
        _Threshold ("Threshold", Float) = 1.0
        _Smoothing ("Smoothing", Float) = 0.98
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
            "UniversalMaterialType" = "Unlit"
            "IgnoreProjector" = "True"
        }

        // ------------------------------------------------------------------
        // ForwardLit: Cel-shade + main light shadow receive (hafif)
        // ------------------------------------------------------------------
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite On
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0

            #pragma vertex Vert
            #pragma fragment Frag

            // DrawMeshInstanced / MaterialPropertyBlock per-instance renk
            #pragma multi_compile_instancing

            // Main light hard shadow only — soft shadow varyanti yok (mobil)
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE

            // OpenGLES3 / Vulkan: gereksiz varyantları kirp
            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // Paylasilan (non-instanced) material sabitleri.
            // NOT: _BaseColor burada YOK — instancing buffer'da tutulur.
            CBUFFER_START(UnityPerMaterial)
                half4 _HighlightColor;
                half4 _ShadowColor;
                half _Threshold;
                half _Smoothing;
            CBUFFER_END

            // ----------------------------------------------------------------
            // GPU Instancing Buffer:
            // Unity, Graphics.DrawMeshInstanced + MaterialPropertyBlock ile
            // gonderilen _BaseColor degerlerini bu buffer'a yazar.
            // Her instance farkli renk alabilir; ek DrawCall olusmaz.
            // UNITY_DEFINE_INSTANCED_PROP: tip + property adi.
            // UNITY_ACCESS_INSTANCED_PROP: fragment'te instance renk okuma.
            // ----------------------------------------------------------------
            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
            UNITY_INSTANCING_BUFFER_END(Props)

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                float4 shadowCoord : TEXCOORD2;
                #endif
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;

                // Instance ID'yi vertex'te kur; fragment'e aktar
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = normalInputs.normalWS;

                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                output.shadowCoord = GetShadowCoord(positionInputs);
                #endif

                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // Fragment'te instance buffer erisimi icin zorunlu
                UNITY_SETUP_INSTANCE_ID(input);

                // Per-instance base color (MaterialPropertyBlock ile set edilen)
                half4 baseColor = UNITY_ACCESS_INSTANCED_PROP(Props, _BaseColor);

                half3 normalWS = normalize(input.normalWS);

                // Main light + gölge
                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                float4 shadowCoord = input.shadowCoord;
                #elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                #else
                float4 shadowCoord = float4(0, 0, 0, 0);
                #endif

                Light mainLight = GetMainLight(shadowCoord);
                half3 lightDirWS = mainLight.direction;

                // Cel ramp: NdotL + TCP2 tarzı edge cifti
                half NdotL = saturate(dot(normalWS, lightDirWS));

                // edge0/edge1: Threshold * (1 ± Smoothing)
                half edge0 = _Threshold * (1.0h - _Smoothing);
                half edge1 = _Threshold * (1.0h + _Smoothing);
                // smoothstep edge0 >= edge1 durumunda bozulmasin
                edge1 = max(edge1, edge0 + 1e-4h);

                half ramp = smoothstep(edge0, edge1, NdotL);

                // Direksiyonel shadow: lit bolgeyi karart
                ramp *= mainLight.shadowAttenuation;

                half3 shade = lerp(_ShadowColor.rgb, _HighlightColor.rgb, ramp);
                half3 finalRGB = shade * baseColor.rgb * mainLight.color;

                return half4(finalRGB, baseColor.a);
            }
            ENDHLSL
        }

        // ------------------------------------------------------------------
        // ShadowCaster: instance matrix ile golge map'e yaz (cast)
        // ------------------------------------------------------------------
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0

            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            // ShadowCaster URP sablonlari UnityPerMaterial bekler; tutarli tut
            CBUFFER_START(UnityPerMaterial)
                half4 _HighlightColor;
                half4 _ShadowColor;
                half _Threshold;
                half _Smoothing;
            CBUFFER_END

            // Shadow pass'te renk kullanilmaz; buffer bos birakilabilir.
            // Ancak material SRP Batcher / instancing layout tutarliligi icin
            // ayni Props buffer imzasi tutulur.
            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
            UNITY_INSTANCING_BUFFER_END(Props)

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float4 GetShadowPositionHClip(Attributes input)
            {
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                float3 lightDirectionWS = _LightDirection;
                #endif

                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));

                #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif

                return positionCS;
            }

            Varyings ShadowPassVertex(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.positionCS = GetShadowPositionHClip(input);
                return output;
            }

            half4 ShadowPassFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return 0;
            }
            ENDHLSL
        }

        // ------------------------------------------------------------------
        // DepthOnly: depth prepass / occlusion (opaque)
        // ------------------------------------------------------------------
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0

            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma multi_compile_instancing

            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _HighlightColor;
                half4 _ShadowColor;
                half _Threshold;
                half _Smoothing;
            CBUFFER_END

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
            UNITY_INSTANCING_BUFFER_END(Props)

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings DepthOnlyVertex(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 DepthOnlyFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
