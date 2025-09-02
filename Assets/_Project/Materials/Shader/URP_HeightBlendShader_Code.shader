// URP用のシェーダー。ハイトブレンド + 道路描画
Shader "Custom/URP/HeightBlendShader_WithRoad"
{
    Properties
    {
        [Header(Texture Settings)]
        _Texture1 ("Texture 1 (Sand)", 2D) = "white" {}
        _Texture2 ("Texture 2 (Grass)", 2D) = "white" {}
        _Texture3 ("Texture 3 (Rock)", 2D) = "white" {}
        _Texture4 ("Texture 4 (Snow)", 2D) = "white" {}
        _TextureTiling("Texture Tiling", Float) = 0.1

        [Header(Height Settings)]
        _Height1 ("Height 1 (Sand/Grass)", Float) = 5
        _Height2 ("Height 2 (Grass/Rock)", Float) = 15
        _Height3 ("Height 3 (Rock/Snow)", Float) = 25

        [Header(Blend Settings)]
        _BlendAmount ("Blend Smoothness", Range(0.01, 10)) = 1.0

        [Header(Road Settings)]
        _RoadTexture ("Road Texture", 2D) = "gray" {}
        _RoadSplatMap ("Road Splat Map", 2D) = "black" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_Texture1);   SAMPLER(sampler_Texture1);
            TEXTURE2D(_Texture2);   SAMPLER(sampler_Texture2);
            TEXTURE2D(_Texture3);   SAMPLER(sampler_Texture3);
            TEXTURE2D(_Texture4);   SAMPLER(sampler_Texture4);
            TEXTURE2D(_RoadTexture);  SAMPLER(sampler_RoadTexture);
            TEXTURE2D(_RoadSplatMap); SAMPLER(sampler_RoadSplatMap);

            float _Height1, _Height2, _Height3, _BlendAmount, _TextureTiling;
            
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionHCS : SV_POSITION; float3 positionWS : TEXCOORD0; float2 uv : TEXCOORD1; };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionHCS = TransformWorldToHClip(OUT.positionWS);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 terrainUV = IN.positionWS.xz * _TextureTiling;
                half4 tex1 = SAMPLE_TEXTURE2D(_Texture1, sampler_Texture1, terrainUV);
                half4 tex2 = SAMPLE_TEXTURE2D(_Texture2, sampler_Texture2, terrainUV);
                half4 tex3 = SAMPLE_TEXTURE2D(_Texture3, sampler_Texture3, terrainUV);
                half4 tex4 = SAMPLE_TEXTURE2D(_Texture4, sampler_Texture4, terrainUV);

                float height = IN.positionWS.y;
                float blend1 = smoothstep(_Height1 - _BlendAmount, _Height1 + _BlendAmount, height);
                float blend2 = smoothstep(_Height2 - _BlendAmount, _Height2 + _BlendAmount, height);
                float blend3 = smoothstep(_Height3 - _BlendAmount, _Height3 + _BlendAmount, height);
                
                half4 terrainColor = lerp(tex1, tex2, blend1);
                terrainColor = lerp(terrainColor, tex3, blend2);
                terrainColor = lerp(terrainColor, tex4, blend3);

                half roadAmount = SAMPLE_TEXTURE2D(_RoadSplatMap, sampler_RoadSplatMap, IN.uv).r;
                half4 roadColor = SAMPLE_TEXTURE2D(_RoadTexture, sampler_RoadTexture, terrainUV);
                half4 finalColor = lerp(terrainColor, roadColor, roadAmount);

                return finalColor;
            }
            ENDHLSL
        }
    }
}