
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

        _ShallowColor ("Shallow Water Color", Color) = (0.52,0.71,0.64,1)
        _DeepColor ("Deep Water Color", Color) = (0.16,0.37,0.41,1)
        _DepthDistance ("Depth Blend Distance", Range(0.1,10)) = 3
        _ShallowAlpha ("Shallow Water Opacity", Range(0,1)) = 0.25
        _DeepAlpha ("Deep Water Opacity", Range(0,1)) = 0.8
        _DepthInfluence ("Depth Color Influence", Range(0,1)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "RenderType"="Transparent"
            "Queue"="Transparent"
        }

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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _ShallowColor;
                float4 _DeepColor;

                float _WaveHeight;
                float _WaveLength;
                float _WaveSpeed;
                float _FacetVariation;
                float _Shine;

                float _DepthDistance;
                float _ShallowAlpha;
                float _DeepAlpha;
                float _DepthInfluence;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
            };

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

                p.y += _WaveHeight *
                    (
                        sin((p.x + p.z * 0.37) * k + t)
                        + 0.45 * sin(
                            (p.z - p.x * 0.28) * k * 1.31
                            - t * 0.73
                        )
                    ) / 1.45;

                output.positionWS = p;
                output.positionCS = TransformWorldToHClip(p);
                output.fog = ComputeFogFactor(output.positionCS.z);
                output.facet = input.color.r;

                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // Surface normal
                float3 normal = normalize(
                    cross(ddy(input.positionWS), ddx(input.positionWS))
                );

                normal *= normal.y < 0 ? -1 : 1;

                // Main light
                Light sun = GetMainLight();

                float3 view = GetWorldSpaceNormalizeViewDir(
                    input.positionWS
                );

                float diffuse = saturate(
                    dot(normal, sun.direction)
                );

                float3 ambient = max(
                    SampleSH(normal),
                    float3(0.18, 0.18, 0.18)
                );

                float variation =
                    1 + (input.facet * 2 - 1) * _FacetVariation;

                // Camera depth sampling
                float2 screenUV =
                    input.positionCS.xy / _ScaledScreenParams.xy;

                float rawDepth = SampleSceneDepth(screenUV);

                float sceneEyeDepth = LinearEyeDepth(
                    rawDepth, _ZBufferParams
                );

                float waterEyeDepth =
                    -TransformWorldToView(input.positionWS).z;

                float waterDepth = max(
                    0,
                    sceneEyeDepth - waterEyeDepth
                );

                float depthFactor = saturate(
                    waterDepth / max(_DepthDistance, 0.01)
                );

                depthFactor =
                    depthFactor * depthFactor *
                    (3.0 - 2.0 * depthFactor);

                // Depth-dependent colors
                float3 depthColor = lerp(
                    _ShallowColor.rgb,
                    _DeepColor.rgb,
                    depthFactor
                );

                float3 baseColor = lerp(
                    _BaseColor.rgb,
                    depthColor,
                    _DepthInfluence
                );

                float3 color =
                    baseColor * variation *
                    (ambient + sun.color * diffuse * 0.65);

                // Sun highlight
                float3 halfway = SafeNormalize(
                    sun.direction + view
                );

                color += sun.color *
                    pow(saturate(dot(normal, halfway)), 64) *
                    _Shine;

                // Fresnel
                float fresnel = pow(
                    1 - saturate(abs(dot(normal, view))),
                    4
                );

                color += float3(0.10, 0.17, 0.18) * fresnel;

                // Depth-dependent transparency
                float depthAlpha = lerp(
                    _ShallowAlpha,
                    _DeepAlpha,
                    depthFactor
                );

                float alpha = lerp(
                    _BaseColor.a,
                    depthAlpha,
                    _DepthInfluence
                );

                alpha = saturate(
                    alpha + fresnel * (1 - alpha) * 0.65
                );

                color = MixFog(color, input.fog);

                return half4(color, alpha);
            }

            ENDHLSL
        }
    }
}
