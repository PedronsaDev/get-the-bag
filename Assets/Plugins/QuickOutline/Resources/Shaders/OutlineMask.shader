Shader "Custom/Outline/Mask" {
  Properties {
    [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest("ZTest", Float) = 8
    _StencilRef("Stencil Reference", Int) = 32
  }

  SubShader {
    Tags {
      "RenderPipeline" = "UniversalPipeline"
      "Queue" = "Transparent+100"
      "RenderType" = "Transparent"
    }

    Pass {
      Name "OutlineMask"
      Cull Off
      ZTest [_ZTest]
      ZWrite Off
      ColorMask 0

      Stencil {
        Ref [_StencilRef]
        Pass Replace
      }

      HLSLPROGRAM
      #pragma target 3.5
      #pragma vertex vert
      #pragma fragment frag
      #pragma multi_compile_instancing

      #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

      struct Attributes {
        float4 positionOS : POSITION;
        UNITY_VERTEX_INPUT_INSTANCE_ID
      };

      struct Varyings {
        float4 positionCS : SV_POSITION;
        UNITY_VERTEX_INPUT_INSTANCE_ID
        UNITY_VERTEX_OUTPUT_STEREO
      };

      CBUFFER_START(UnityPerMaterial)
        float _ZTest;
        float _StencilRef;
      CBUFFER_END

      Varyings vert(Attributes input) {
        Varyings output = (Varyings)0;
        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_TRANSFER_INSTANCE_ID(input, output);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

        output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
        return output;
      }

      half4 frag(Varyings input) : SV_Target {
        UNITY_SETUP_INSTANCE_ID(input);
        return 0;
      }
      ENDHLSL
    }
  }

  FallBack Off
}
