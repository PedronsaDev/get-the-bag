Shader "Custom/Outline/Silhouette" {
  Properties {
    [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest("ZTest", Float) = 8
    _StencilRef("Stencil Reference", Int) = 1
    [HideInInspector] _CameraEnabled("Camera Enabled", Float) = 1

    [Header(Surface Fill Appearance)]
    [ColorUsage(true, true)] _FillColor("Fill Color (HDR)", Color) = (0.2, 0.8, 1.0, 0.5)
    [ColorUsage(true, true)] _FillSecondaryColor("Secondary Color (HDR)", Color) = (0, 0, 0, 0)

    [Header(Pattern Settings)]
    [Enum(Solid,0,Stripes,1,Grid,2,Texture,3,FresnelRim,4)] _PatternType("Pattern Type", Float) = 1
    _PatternTex("Pattern Texture", 2D) = "white" {}
    _PatternTiling("Pattern Tiling", Vector) = (10, 10, 0, 0)
    _PatternLineWidth("Pattern Line Width", Range(0.01, 0.49)) = 0.12
    _AnimationPhase("Animation Phase", Float) = 0
    [Enum(UnityEngine.Rendering.CullMode)] _Cull("Surface Culling", Float) = 2
    _PatternAngle("Pattern Angle", Range(0, 360)) = 45
    _ScrollSpeed("Scroll Speed", Float) = 1.0
    _FresnelPower("Fresnel Power (Rim Mode)", Range(0.1, 10)) = 2.5

    [Header(Coordinate Space)]
    [Enum(ObjectUV,0,ScreenSpace,1,WorldSpace,2)] _CoordSpace("Coordinate Space", Float) = 0

    [Header(Distance Fading)]
    [Toggle] _DistanceFadeEnabled("Distance Fade Enabled", Float) = 0
    _FadeMinDistance("Fade Min Distance", Float) = 15
    _FadeMaxDistance("Fade Max Distance", Float) = 40
  }

  SubShader {
    Tags {
      "RenderPipeline" = "UniversalPipeline"
      "Queue" = "Transparent+120"
      "RenderType" = "Transparent"
    }

    Pass {
      Name "OutlineSilhouette"
      Cull [_Cull]
      ZTest [_ZTest]
      ZWrite Off
      Blend SrcAlpha OneMinusSrcAlpha
      ColorMask RGB

      Stencil {
        Ref [_StencilRef]
        ReadMask 1
        WriteMask 1
        Comp Equal
      }

      HLSLPROGRAM
      #pragma target 3.5
      #pragma vertex vert
      #pragma fragment frag
      #pragma multi_compile_instancing
      #pragma multi_compile _ LOD_FADE_CROSSFADE

      #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
      #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"

      struct Attributes {
        float4 positionOS : POSITION;
        float3 normalOS   : NORMAL;
        float2 uv         : TEXCOORD0;
        UNITY_VERTEX_INPUT_INSTANCE_ID
      };

      struct Varyings {
        float4 positionCS : SV_POSITION;
        float2 uv         : TEXCOORD0;
        float3 positionWS : TEXCOORD1;
        float3 normalWS   : TEXCOORD2;
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
        float _PatternLineWidth;
        float _AnimationPhase;
        float _Cull;
        float _ScrollSpeed;
        float _FresnelPower;
        float _CoordSpace;
        float _ZTest;
        float _StencilRef;
        float _CameraEnabled;

        float _DistanceFadeEnabled;
        float _FadeMinDistance;
        float _FadeMaxDistance;
      CBUFFER_END

      Varyings vert(Attributes input) {
        Varyings output = (Varyings)0;
        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_TRANSFER_INSTANCE_ID(input, output);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

        output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
        output.positionCS = TransformWorldToHClip(output.positionWS);
        output.normalWS = TransformObjectToWorldNormal(input.normalOS);
        output.uv = input.uv;
        return output;
      }

      half4 frag(Varyings input) : SV_Target {
        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        clip(_CameraEnabled - 0.5);
        #if defined(LOD_FADE_CROSSFADE)
          LODFadeCrossFade(input.positionCS);
        #endif

        // Distance Fade calculation
        float distToCam = length(input.positionWS - _WorldSpaceCameraPos);
        float distanceAlpha = 1.0;
        if (_DistanceFadeEnabled > 0.5) {
          distanceAlpha = saturate((_FadeMaxDistance - distToCam) / max(0.001, _FadeMaxDistance - _FadeMinDistance));
        }

        float patternAlpha = 1.0;

        if (_PatternType > 3.5) {
          // Fresnel Inner Rim Glow
          float3 viewDirWS = normalize(_WorldSpaceCameraPos - input.positionWS);
          float NdotV = saturate(dot(normalize(input.normalWS), viewDirWS));
          patternAlpha = pow(1.0 - NdotV, _FresnelPower);
        } else {
          float2 baseUV;
          if (_CoordSpace > 1.5) {
            // World Space
            baseUV = input.positionWS.xz + input.positionWS.y * 0.5;
          } else if (_CoordSpace > 0.5) {
            // Screen Space
            baseUV = (input.positionCS.xy / _ScaledScreenParams.xy);
            baseUV.x *= (_ScaledScreenParams.x / _ScaledScreenParams.y);
          } else {
            // Object UV
            baseUV = input.uv;
          }

          // Rotate UV by pattern angle
          float rad = radians(_PatternAngle);
          float cosA = cos(rad);
          float sinA = sin(rad);
          float2x2 rot = float2x2(cosA, -sinA, sinA, cosA);

          float2 scrollOffset = float2(_ScrollSpeed * (_Time.y + _AnimationPhase), 0.0);
          float2 patternUV = mul(rot, baseUV * _PatternTiling.xy) + scrollOffset;

          if (_PatternType > 2.5) {
            // Texture
            patternAlpha = SAMPLE_TEXTURE2D(_PatternTex, sampler_PatternTex, patternUV).r;
          } else if (_PatternType > 1.5) {
            // Grid
            float2 grid = abs(frac(patternUV + 0.5) - 0.5);
            float distanceToLine = min(grid.x, grid.y);
            float aa = max(fwidth(distanceToLine), 0.0001);
            patternAlpha = 1.0 - smoothstep(_PatternLineWidth - aa, _PatternLineWidth + aa, distanceToLine);
          } else if (_PatternType > 0.5) {
            // Stripes
            float stripe = abs(frac(patternUV.x + 0.5) - 0.5);
            float aa = max(fwidth(patternUV.x), 0.0001);
            patternAlpha = 1.0 - smoothstep(_PatternLineWidth - aa, _PatternLineWidth + aa, stripe);
          }
        }

        half4 color = lerp(_FillSecondaryColor, _FillColor, patternAlpha);
        color.a *= distanceAlpha;
        return color;
      }
      ENDHLSL
    }
  }

  FallBack Off
}
