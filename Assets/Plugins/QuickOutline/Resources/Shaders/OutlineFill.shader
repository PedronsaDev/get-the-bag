Shader "Custom/Outline/Fill" {
  Properties {
    [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest("ZTest", Float) = 8
    _StencilRef("Stencil Reference", Int) = 32

    [Header(Outline Appearance)]
    [ColorUsage(true, true)] _OutlineColor("Outline Color (HDR)", Color) = (1, 1, 1, 1)
    _OutlineWidth("Outline Width", Range(0, 50)) = 5
    [Enum(ScreenSpacePixels,0,WorldSpaceMeters,1)] _WidthMode("Width Mode", Float) = 0
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
        Ref [_StencilRef]
        Comp NotEqual
      }

      HLSLPROGRAM
      #pragma target 3.5
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
        float4 positionCS   : SV_POSITION;
        half4 color         : COLOR;
        UNITY_VERTEX_INPUT_INSTANCE_ID
        UNITY_VERTEX_OUTPUT_STEREO
      };

      CBUFFER_START(UnityPerMaterial)
        half4 _OutlineColor;
        float _OutlineWidth;
        float _ZTest;
        float _StencilRef;
        float _WidthMode;
      CBUFFER_END

      Varyings vert(Attributes input) {
        Varyings output = (Varyings)0;
        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_TRANSFER_INSTANCE_ID(input, output);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

        float3 normalOS = any(input.smoothNormal) ? input.smoothNormal : input.normalOS;
        float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);

        // Extract pure scale-free world rotation from UNITY_MATRIX_M
        float3 axisX = normalize(UNITY_MATRIX_M[0].xyz);
        float3 axisY = normalize(UNITY_MATRIX_M[1].xyz);
        float3 axisZ = normalize(UNITY_MATRIX_M[2].xyz);
        float3x3 rotMatrixWS = float3x3(axisX, axisY, axisZ);

        float3 normalWS = normalize(mul(rotMatrixWS, normalOS));

        float4 positionCS;
        if (_WidthMode > 0.5) {
          // World space meters mode
          float3 extrudedWS = positionWS + normalWS * (_OutlineWidth * 0.01);
          positionCS = TransformWorldToHClip(extrudedWS);
        } else {
          // Screen space pixels mode (scale-invariant)
          positionCS = TransformWorldToHClip(positionWS);
          float3 normalVS = mul((float3x3)UNITY_MATRIX_V, normalWS);

          // Project view normal to clip space
          float2 normalCS = mul((float2x2)UNITY_MATRIX_P, normalVS.xy);
          float len = length(normalCS);
          float2 screenDir = (len > 0.0001) ? (normalCS / len) : float2(0.0, 0.0);

          // Aspect ratio correction (square pixel extrusion)
          float aspect = _ScreenParams.y / _ScreenParams.x;
          screenDir.x *= aspect;

          // Extrude in clip space pre-multiplied by w to cancel perspective division
          positionCS.xy += screenDir * (positionCS.w * _OutlineWidth * 0.001);
        }

        output.positionCS = positionCS;
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

  FallBack Off
}
