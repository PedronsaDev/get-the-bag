Shader "Custom/Outline/Fill" {
  Properties {
    [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest("ZTest", Float) = 8
    _StencilRef("Stencil Reference", Int) = 1
    [HideInInspector] _CameraEnabled("Camera Enabled", Float) = 1

    [Header(Outline Appearance)]
    [ColorUsage(true, true)] _OutlineColor("Outline Color (HDR)", Color) = (1, 1, 1, 1)
    _OutlineWidth("Outline Width", Range(0, 50)) = 5
    [Enum(ScreenSpacePixels,0,WorldSpaceCentimeters,1,WorldSpaceMeters,2)] _WidthMode("Width Mode", Float) = 0
    [HideInInspector] _AnimationPhase("Animation Phase", Float) = 0

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
        ReadMask 1
        WriteMask 1
        Comp NotEqual
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
        float4 positionOS   : POSITION;
        float3 normalOS     : NORMAL;
        // xyz: smoothed direction, encoded as an object-space normal for this renderer's
        // scale ratio. w: miter length. Zero xyz means "use the native normal".
        float4 extrusion    : TEXCOORD3;
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
        float _CameraEnabled;
        float _WidthMode;
        float _AnimationPhase;

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

        bool hasSmoothedDirection = any(input.extrusion.xyz);
        float3 normalOS = hasSmoothedDirection ? input.extrusion.xyz : input.normalOS;
        float miterLength = hasSmoothedDirection ? input.extrusion.w : 1.0;
        float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);

        float3 normalWS = TransformObjectToWorldNormal(normalOS);

        // 1. Dynamic Pulse
        float pulseFactor = 1.0;
        if (_PulseEnabled > 0.5) {
          pulseFactor = 1.0 + sin((_Time.y + _AnimationPhase) * _PulseSpeed) * _PulseAmplitude;
        }

        // 2. Stylized Jitter / Spider-Verse Wobble
        float jitterFactor = 1.0;
        if (_JitterEnabled > 0.5) {
          float jitterTime = _Time.y + _AnimationPhase;
          if (_JitterFramerate > 0.5) {
            jitterTime = floor(jitterTime * _JitterFramerate) / _JitterFramerate;
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
          // World space modes: value 1 is centimeters, value 2 is meters.
          float metersPerUnit = _WidthMode > 1.5 ? 1.0 : 0.01;
          float3 extrudedWS = positionWS + normalWS * (currentWidth * metersPerUnit * miterLength);
          positionCS = TransformWorldToHClip(extrudedWS);
        } else {
          // Screen space pixels mode (scale-invariant)
          positionCS = TransformWorldToHClip(positionWS);
          float3 normalVS = mul((float3x3)UNITY_MATRIX_V, normalWS);

          // The miter seen on screen is the 3D miter projected onto the view plane. It
          // lengthens hard corners so both silhouette edges reach the full width;
          // smooth surfaces stay at exactly 1.
          float screenMiter = max(1.0, length(normalVS.xy) * miterLength);

          float2 projectedNormal = mul((float2x2)UNITY_MATRIX_P, normalVS.xy);
          float2 pixelDirection = projectedNormal * _ScaledScreenParams.xy;
          float magnitude = length(pixelDirection);
          pixelDirection = magnitude > 0.0001 ? pixelDirection / magnitude : float2(0.0, 0.0);
          positionCS.xy += pixelDirection * (2.0 * currentWidth * screenMiter / _ScaledScreenParams.xy) * positionCS.w;
        }

        output.positionCS = positionCS;
        output.color = _OutlineColor;
        output.color.a *= distanceAlpha;

        return output;
      }

      half4 frag(Varyings input) : SV_Target {
        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        clip(_CameraEnabled - 0.5);
        #if defined(LOD_FADE_CROSSFADE)
          LODFadeCrossFade(input.positionCS);
        #endif
        return input.color;
      }
      ENDHLSL
    }
  }

  FallBack Off
}
