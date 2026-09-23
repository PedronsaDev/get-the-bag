Shader "Custom/Outline/Silhouette" {
  Properties {
    [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest("ZTest", Float) = 8
    _StencilRef("Stencil Reference", Int) = 32

    [Header(Surface Fill Appearance)]
    [ColorUsage(true, true)] _FillColor("Fill Color (HDR)", Color) = (0.2, 0.8, 1.0, 0.5)
    [ColorUsage(true, true)] _FillSecondaryColor("Secondary Color (HDR)", Color) = (0, 0, 0, 0)

    [Header(Pattern Settings)]
    [Enum(Solid,0,Stripes,1,Grid,2,Texture,3)] _PatternType("Pattern Type", Float) = 1
    _PatternTex("Pattern Texture", 2D) = "white" {}
    _PatternTiling("Pattern Tiling", Vector) = (10, 10, 0, 0)
    _PatternAngle("Pattern Angle", Range(0, 360)) = 45
    _ScrollSpeed("Scroll Speed", Float) = 1.0

    [Header(Coordinate Space)]
    [Enum(ObjectUV,0,ScreenSpace,1,WorldSpace,2)] _CoordSpace("Coordinate Space", Float) = 0
  }

  SubShader {
    Tags {
      "RenderPipeline" = "UniversalPipeline"
      "Queue" = "Transparent+120"
      "RenderType" = "Transparent"
    }

    Pass {
      Name "OutlineSilhouette"
      Cull Off
      ZTest [_ZTest]
      ZWrite Off
      Blend SrcAlpha OneMinusSrcAlpha
      ColorMask RGB

      Stencil {
        Ref [_StencilRef]
        Comp Equal
      }

      HLSLPROGRAM
      #pragma target 3.5
      #pragma vertex vert
      #pragma fragment frag
      #pragma multi_compile_instancing

      #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

      struct Attributes {
        float4 positionOS : POSITION;
        float2 uv         : TEXCOORD0;
        UNITY_VERTEX_INPUT_INSTANCE_ID
      };

      struct Varyings {
        float4 positionCS : SV_POSITION;
        float2 uv         : TEXCOORD0;
        float3 positionWS : TEXCOORD1;
        UNITY_VERTEX_INPUT_INSTANCE_ID
        UNITY_VERTEX_OUTPUT_STEREO
      };

      TEXTURE2D(_PatternTex);
      SAMPLER(sampler_PatternTex);

      CBUFFER_START(UnityPerMaterial)
        half4 _FillColor;
        half4 _FillSecondaryColor;
        float4 _PatternTiling;
        float _PatternType;
        float _PatternAngle;
        float _ScrollSpeed;
        float _CoordSpace;
        float _ZTest;
        float _StencilRef;
      CBUFFER_END

      Varyings vert(Attributes input) {
        Varyings output = (Varyings)0;
        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_TRANSFER_INSTANCE_ID(input, output);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

        output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
        output.positionCS = TransformWorldToHClip(output.positionWS);
        output.uv = input.uv;
        return output;
      }

      half4 frag(Varyings input) : SV_Target {
        UNITY_SETUP_INSTANCE_ID(input);

        float2 baseUV;
        if (_CoordSpace > 1.5) {
          // World Space
          baseUV = input.positionWS.xz + input.positionWS.y * 0.5;
        } else if (_CoordSpace > 0.5) {
          // Screen Space
          baseUV = (input.positionCS.xy / _ScreenParams.xy);
          baseUV.x *= (_ScreenParams.x / _ScreenParams.y);
        } else {
          // Object UV
          baseUV = input.uv;
        }

        // Rotate UV by pattern angle
        float rad = radians(_PatternAngle);
        float cosA = cos(rad);
        float sinA = sin(rad);
        float2x2 rot = float2x2(cosA, -sinA, sinA, cosA);

        float2 scrollOffset = float2(_ScrollSpeed * _Time.y, 0.0);
        float2 patternUV = mul(rot, baseUV * _PatternTiling.xy) + scrollOffset;

        float patternAlpha = 1.0;
        if (_PatternType > 2.5) {
          // Texture
          patternAlpha = SAMPLE_TEXTURE2D(_PatternTex, sampler_PatternTex, patternUV).r;
        } else if (_PatternType > 1.5) {
          // Grid
          float2 grid = abs(frac(patternUV) - 0.5);
          patternAlpha = 1.0 - smoothstep(0.04, 0.08, min(grid.x, grid.y));
        } else if (_PatternType > 0.5) {
          // Stripes
          float stripe = frac(patternUV.x);
          patternAlpha = smoothstep(0.42, 0.58, stripe);
        }

        half4 color = lerp(_FillSecondaryColor, _FillColor, patternAlpha);
        color.a *= _FillColor.a;
        return color;
      }
      ENDHLSL
    }
  }

  FallBack Off
}
