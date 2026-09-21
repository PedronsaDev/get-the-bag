Shader "Custom/Outline Fill" {
    Properties {
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest("ZTest", Float) = 8
        _OutlineColor("Outline Color", Color) = (1, 1, 1, 1)
        _OutlineWidth("Outline Width", Range(0, 10)) = 2
    }

    SubShader {
        Tags {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent+110"
            "RenderType" = "Transparent"
        }

        Pass {
            Name "OutlineFill"
            Cull Off
            ZTest [_ZTest]
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha
            ColorMask RGB

            Stencil {
                Ref 32
                ReadMask 32
                Comp NotEqual
            }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float3 smoothNormal : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings {
                float4 positionCS : SV_POSITION;
                half4 color       : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _OutlineColor;
                float _OutlineWidth;
                float _ZTest;
            CBUFFER_END

            Varyings vert(Attributes input) {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                // Extract pure unit rotation axes to eliminate non-uniform scale distortion
                float3 axisX = normalize(mul(GetObjectToWorldMatrix(), float4(1.0, 0.0, 0.0, 0.0)).xyz);
                float3 axisY = normalize(mul(GetObjectToWorldMatrix(), float4(0.0, 1.0, 0.0, 0.0)).xyz);
                float3 axisZ = normalize(mul(GetObjectToWorldMatrix(), float4(0.0, 0.0, 1.0, 0.0)).xyz);

                float3 normalOS = any(input.smoothNormal) ? input.smoothNormal : input.normalOS;
                float3 normalWS = normalize(normalOS.x * axisX + normalOS.y * axisY + normalOS.z * axisZ);
                float3 normalVS = TransformWorldToViewNormal(normalWS, true);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 positionVS = TransformWorldToView(positionWS);

                // Extrude in 3D view space: -positionVS.z is the depth in front of the camera
                float depth = max(-positionVS.z, 0.01);
                float3 extrudedVS = positionVS + normalVS * (depth * (_OutlineWidth / 1000.0));

                output.positionCS = TransformWViewToHClip(extrudedVS);
                output.color = _OutlineColor;

                return output;
            }

            half4 frag(Varyings input) : SV_Target {
                UNITY_SETUP_INSTANCE_ID(input);
                return input.color;
            }
            ENDHLSL
        }
    }
}
