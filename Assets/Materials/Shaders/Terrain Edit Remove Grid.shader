Shader "Custom/TerrainEditRemoveGrid"{
  Properties{
      _MainTex("Tint Texture", 2D) = "white" {}
      _WireFrameColor("Wireframe Color", Color) = (1, 1, 1, 1)
      _Glossiness("Glossiness", Range(0,1)) = 0.5
      _Metallic("Metallic", Range(0,1)) = 0.0
      _WireframeWidth("Wire Width", Float) = 0.1
      _MaxMeshRadius("Maximum Mesh Radius", Float) = 1.0
  }
    SubShader{
      Tags {
        "RenderType" = "Transparent"
        "Queue" = "Transparent"
      }
      LOD 200
      Pass {
        ZTest Always
      }

      CGPROGRAM
        // Physically based Standard lighting model, and enable shadows on all light types
        #pragma surface surf Standard fullforwardshadows alpha
        // Use shader model 3.0 target, to get nicer looking lighting
        #pragma target 3.0

        struct Input {
          float4 position: POSITION;
          float4 vertexColor: COLOR;
          float2 uv_MainTex;
        };

        float4 _MainTex;
        float4 _WireFrameColor;
        float _Glossiness;
        float _Metallic;
        float _WireframeWidth;
        float _MaxMeshRadius;

        // Get the fade % based on the distance from this to the center
        float distanceFade(float vertexPosition) {
          float positionInWorldSpace = mul(unity_ObjectToWorld, vertexPosition);
          float centerOfModelInWorldSpace = mul(unity_ObjectToWorld, float3(0, 0, 0));
          return 1 - distance(positionInWorldSpace, centerOfModelInWorldSpace)/_MaxMeshRadius;
        }

        void surf(Input IN, inout SurfaceOutputStandard o) {
          // Albedo comes from a texture tinted by color
          o.Albedo = IN.vertexColor.rgb;
          // Metallic and smoothness come from slider variables
          o.Metallic = _Metallic;
          o.Smoothness = _Glossiness;
          if (IN.uv_MainTex.x <= _WireframeWidth 
            || IN.uv_MainTex.y <= _WireframeWidth
            || IN.uv_MainTex.x >= 1 - _WireframeWidth
            || IN.uv_MainTex.y >= 1 - _WireframeWidth
          ) {
            o.Albedo = _WireFrameColor;
            o.Alpha = 1;
          } else {
            o.Alpha = distanceFade(IN.position);
          }
        }
    ENDCG
  }
  FallBack "Diffuse"
}


