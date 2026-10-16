Shader "Avian/SafeZoneOverlay"
{
    Properties
    {
        _Color ("Outside Tint", Color) = (0.4392157, 0.4392157, 0.83137256, 0.39215687)
        _SafeZoneCenter ("Safe Zone Center", Vector) = (0, 0, 0, 0)
        _SafeZoneRadius ("Safe Zone Radius", Float) = 500
        _EdgeSoftness ("Edge Softness", Float) = 0.25
    }
    SubShader
    {
        Tags { "Queue" = "Transparent+80" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off
            ZWrite Off
            ZTest Always
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float4 _SafeZoneCenter;
                float _SafeZoneRadius;
                float _EdgeSoftness;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float distanceFromCenter = distance(input.positionWS.xz, _SafeZoneCenter.xz);
                float outside = smoothstep(_SafeZoneRadius, _SafeZoneRadius + max(0.001, _EdgeSoftness), distanceFromCenter);
                return half4(_Color.rgb, _Color.a * outside);
            }
            ENDHLSL
        }
    }
}
