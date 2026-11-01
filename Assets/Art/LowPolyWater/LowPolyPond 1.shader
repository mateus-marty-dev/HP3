Shader "Hotzenplotz/Low Poly Pond"
{
    Properties
    {
        _BaseColor ("Water Color", Color) = (0.12,0.48,0.42,0.65)
        _WaveHeight ("Wave Height (world units)", Range(0,0.5)) = 0.08
        _WaveLength ("Wave Length (world units)", Float) = 3
        _WaveSpeed ("Wave Speed", Range(0,3)) = 0.45
        _FacetVariation ("Facet Color Variation", Range(0,0.3)) = 0.08
        _Shine ("Sun Highlight", Range(0,1)) = 0.35
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Name "Water"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _WaveHeight, _WaveLength, _WaveSpeed, _FacetVariation, _Shine;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float4 color : COLOR; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float fog : TEXCOORD1;
                float facet : TEXCOORD2;
            };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 p = TransformObjectToWorld(input.positionOS.xyz);
                float k = 6.283185 / max(_WaveLength, 0.1);
                float t = _Time.y * _WaveSpeed;
                p.y += _WaveHeight * (sin((p.x + p.z * 0.37) * k + t)
                    + 0.45 * sin((p.z - p.x * 0.28) * k * 1.31 - t * 0.73)) / 1.45;
                output.positionWS = p;
                output.positionCS = TransformWorldToHClip(p);
                output.fog = ComputeFogFactor(output.positionCS.z);
                output.facet = input.color.r;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float3 normal = normalize(cross(ddy(input.positionWS), ddx(input.positionWS)));
                normal *= normal.y < 0 ? -1 : 1;
                Light sun = GetMainLight();
                float3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float diffuse = saturate(dot(normal, sun.direction));
                float3 ambient = max(SampleSH(normal), float3(0.18,0.18,0.18));
                float variation = 1 + (input.facet * 2 - 1) * _FacetVariation;
                float3 color = _BaseColor.rgb * variation * (ambient + sun.color * diffuse * 0.65);
                float3 halfway = SafeNormalize(sun.direction + view);
                color += sun.color * pow(saturate(dot(normal, halfway)), 64) * _Shine;
                float fresnel = pow(1 - saturate(abs(dot(normal, view))), 4);
                color += float3(0.10,0.17,0.18) * fresnel;
                color = MixFog(color, input.fog);
                return half4(color, saturate(_BaseColor.a + fresnel * (1 - _BaseColor.a) * 0.65));
            }
            ENDHLSL
        }
    }
}
