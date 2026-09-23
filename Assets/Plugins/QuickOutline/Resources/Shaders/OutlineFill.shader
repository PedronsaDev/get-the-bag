Shader "Custom/Outline/Fill" {
  Properties {
    [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest("ZTest", Float) = 8
    _StencilRef("Stencil Reference", Int) = 32

    [Header(Outline Appearance)]
    [ColorUsage(true, true)] _OutlineColor("Outline Color (HDR)", Color) = (1, 1, 1, 1)
    _OutlineWidth("Outline Width", Range(0, 50)) = 5
    [Enum(ScreenSpacePixels,0,WorldSpaceMeters,1)] _WidthMode("Width Mode", Float) = 0

    [Header(Animation and Pulse)]
    [Toggle] _PulseEnabled("Pulse Enabled", Float) = 0
    _PulseSpeed("Pulse Speed", Float) = 2.5
    _PulseAmplitude("Pulse Amplitude", Range(0, 1)) = 0.35

    [Header(Distance Fading)]
    [Toggle] _DistanceFadeEnabled("Distance Fade Enabled", Float) = 0
    _FadeMinDistance("Fade Min Distance", Float) = 15
    _FadeMaxDistance("Fade Max Distance", Float) = 40

    [Header(Stylized Jitter Wobble)]
    [Toggle] _JitterEnabled("Jitter Enabled", Float) = 0
    _JitterSpeed("Jitter Speed", Float) = 6.0
    _JitterFrequency("Jitter Frequency", Float) = 4.0
    _JitterScale("Jitter Scale", Range(0, 1)) = 0.35
    _JitterFramerate("Jitter Framerate (0 for smooth)", Float) = 12
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

        float _PulseEnabled;
        float _PulseSpeed;
        float _PulseAmplitude;

        float _DistanceFadeEnabled;
        float _FadeMinDistance;
        float _FadeMaxDistance;

        float _JitterEnabled;
        float _JitterSpeed;
        float _JitterFrequency;
        float _JitterScale;
        float _JitterFramerate;
      CBUFFER_END

      // Fast procedural hash for vertex wobble (zero texture sampling cost)
      float Hash31(float3 p) {
        p = frac(p * 0.3183099 + 0.1);
        p *= 17.0;
        return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
      }

      float Noise3D(float3 p) {
        float3 i = floor(p);
        float3 f = frac(p);
        f = f * f * (3.0 - 2.0 * f);
        return lerp(
          lerp(lerp(Hash31(i + float3(0,0,0)), Hash31(i + float3(1,0,0)), f.x),
               lerp(Hash31(i + float3(0,1,0)), Hash31(i + float3(1,1,0)), f.x), f.y),
          lerp(lerp(Hash31(i + float3(0,0,1)), Hash31(i + float3(1,0,1)), f.x),
               lerp(Hash31(i + float3(0,1,1)), Hash31(i + float3(1,1,1)), f.x), f.y), f.z);
      }

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

        // 1. Dynamic Pulse
        float pulseFactor = 1.0;
        if (_PulseEnabled > 0.5) {
          pulseFactor = 1.0 + sin(_Time.y * _PulseSpeed) * _PulseAmplitude;
        }

        // 2. Stylized Jitter / Spider-Verse Wobble
        float jitterFactor = 1.0;
        if (_JitterEnabled > 0.5) {
          float jitterTime = _Time.y;
          if (_JitterFramerate > 0.5) {
            jitterTime = floor(_Time.y * _JitterFramerate) / _JitterFramerate;
          }
          float n = Noise3D(positionWS * _JitterFrequency + float3(0.0, jitterTime * _JitterSpeed, 0.0));
          jitterFactor = 1.0 + (n - 0.5) * 2.0 * _JitterScale;
        }

        // 3. Distance Fading
        float distToCam = length(positionWS - _WorldSpaceCameraPos);
        float distanceAlpha = 1.0;
        float distanceWidthFactor = 1.0;
        if (_DistanceFadeEnabled > 0.5) {
          distanceAlpha = saturate((_FadeMaxDistance - distToCam) / max(0.001, _FadeMaxDistance - _FadeMinDistance));
          distanceWidthFactor = lerp(0.3, 1.0, distanceAlpha);
        }

        float currentWidth = _OutlineWidth * pulseFactor * jitterFactor * distanceWidthFactor;

        float4 positionCS;
        if (_WidthMode > 0.5) {
          // World space meters mode
          float3 extrudedWS = positionWS + normalWS * (currentWidth * 0.01);
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
          positionCS.xy += screenDir * (positionCS.w * currentWidth * 0.001);
        }

        output.positionCS = positionCS;
        output.color = _OutlineColor;
        output.color.a *= distanceAlpha;

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
