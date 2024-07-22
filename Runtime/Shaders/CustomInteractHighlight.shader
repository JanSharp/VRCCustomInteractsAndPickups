﻿Shader "Unlit/CustomInteractHighlight"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
        }

        Pass
        {
            ZWrite Off
            ZTest Off
            // Blend One One // Additive
            Blend SrcAlpha OneMinusSrcAlpha // Traditional transparency

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            float4 _Color;

            struct MeshData
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float3 uv : TEXCOORD0;
            };

            struct Interpolator
            {
                float4 vertex : SV_POSITION; // clip space
                float3 normal : TEXCOORD0;
                float3 viewDir : TEXCOORD1;
                float3 uv : TEXCOORD2;
            };

            Interpolator vert (MeshData v)
            {
                Interpolator o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.viewDir = _WorldSpaceCameraPos - mul(unity_ObjectToWorld, v.vertex);
                o.uv = v.uv;
                return o;
            }

            float4 frag (Interpolator i) : SV_Target
            {
                // float3 forward = -mul((float3x3)unity_CameraToWorld, float3(0,0,1));
                float3 forward = normalize(i.viewDir);
                // return float4(saturate((1 - dot(i.normal, forward)).xxx), 1) * _Color;
                float fresnel = 1 - dot(i.normal, forward);
                float base = (_SinTime.w + 1) / 2;
                return float4(_Color.rgb, _Color.w * (0.2 * base + 0.8 * fresnel));
            }
            ENDCG
        }
    }
}
