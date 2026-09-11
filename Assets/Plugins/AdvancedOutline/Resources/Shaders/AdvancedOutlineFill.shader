// AdvancedOutlineFill.shader
// Extrudes vertices along smooth normals and renders color where stencil != 1.
// Supports custom screen-space dashed animation patterns.
Shader "Custom/Advanced Outline Fill" {
  Properties {
    [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest("ZTest", Float) = 0
    _OutlineColor("Outline Color", Color) = (1, 1, 1, 1)
    _OutlineWidth("Outline Width", Range(0, 10)) = 2
    
    [Header(Dashed Pattern)]
    _DashScale("Dash Scale", Float) = 0
    _DashSpeed("Dash Speed", Float) = 0
  }

  SubShader {
    Tags {
      "Queue" = "Transparent+110"
      "RenderType" = "Transparent"
      "DisableBatching" = "True"
    }

    Pass {
      Name "Fill"
      Cull Off
      ZTest [_ZTest]
      ZWrite Off
      Blend SrcAlpha OneMinusSrcAlpha
      ColorMask RGB

      Stencil {
        Ref 1
        Comp NotEqual
      }

      CGPROGRAM
      #include "UnityCG.cginc"

      #pragma vertex vert
      #pragma fragment frag

      struct appdata {
        float4 vertex : POSITION;
        float3 normal : NORMAL;
        float3 smoothNormal : TEXCOORD3;
        UNITY_VERTEX_INPUT_INSTANCE_ID
      };

      struct v2f {
        float4 position : SV_POSITION;
        fixed4 color : COLOR;
        float4 screenPos : TEXCOORD4;
        UNITY_VERTEX_OUTPUT_STEREO
      };

      uniform fixed4 _OutlineColor;
      uniform float _OutlineWidth;
      uniform float _DashScale;
      uniform float _DashSpeed;

      v2f vert(appdata input) {
        v2f output;

        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

        float3 normal = any(input.smoothNormal) ? input.smoothNormal : input.normal;
        float3 viewPosition = UnityObjectToViewPos(input.vertex);
        float3 viewNormal = normalize(mul((float3x3)UNITY_MATRIX_IT_MV, normal));

        output.position = UnityViewToClipPos(viewPosition + viewNormal * -viewPosition.z * _OutlineWidth / 1000.0);
        output.color = _OutlineColor;
        
        // Calculate screenspace position for the dashed effect
        output.screenPos = ComputeScreenPos(output.position);

        return output;
      }

      fixed4 frag(v2f input) : SV_Target {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

        // Calculate dashed/crawling pattern if _DashScale is non-zero
        if (_DashScale > 0.001) {
          float2 screenPos = input.screenPos.xy / input.screenPos.w;
          
          // Multiply by screen resolution to keep dash length consistent across aspect ratios
          float2 pixelPos = screenPos * _ScreenParams.xy;
          
          // scrolling diagonal dashes pattern
          float pattern = sin((pixelPos.x + pixelPos.y) * _DashScale - _Time.y * _DashSpeed);
          if (pattern < 0.0) {
            discard;
          }
        }

        return input.color;
      }
      ENDCG
    }
  }
}
