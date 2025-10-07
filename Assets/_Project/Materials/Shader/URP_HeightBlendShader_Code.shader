// URP用のシェーダー。ハイトブレンド + 川と道と棚田の描画（境テクスチャ対応）
Shader "Custom/URP/HeightBlendShader_WithRiverAndRoadAndPaddy"
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
        _BlendAmount ("Blend Smoothness", Range(0.01, 10)) = 1.0

        [Header(Paddy Field Settings)]
        _PaddyMask ("Paddy Mask (R=Flat, G=Border)", 2D) = "black" {}
        _PaddyTexture ("Paddy Texture (Flat)", 2D) = "gray" {}
        // ★★★ 変更点1: 境用のテクスチャプロパティを追加 ★★★
        _PaddyBorderTexture ("Paddy Texture (Border)", 2D) = "white" {}

        [Header(River Settings)]
        _RiverTexture ("River Texture", 2D) = "gray" {}
        _RiverMask ("River Mask", 2D) = "black" {}
        _RiverDepth ("River Depth", Range(0.0, 50.0)) = 2.0

        [Header(Road Settings)]
        _RoadTexture ("Road Texture", 2D) = "gray" {}
        _RoadMask ("Road Mask", 2D) = "black" {}
        _RoadElevation ("Road Elevation", Range(0.0, 50.0)) = 0.5
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

            TEXTURE2D(_Texture1);       SAMPLER(sampler_Texture1);
            TEXTURE2D(_Texture2);       SAMPLER(sampler_Texture2);
            TEXTURE2D(_Texture3);       SAMPLER(sampler_Texture3);
            TEXTURE2D(_Texture4);       SAMPLER(sampler_Texture4);
            TEXTURE2D(_PaddyMask);      SAMPLER(sampler_PaddyMask);
            TEXTURE2D(_PaddyTexture);   SAMPLER(sampler_PaddyTexture);
            // ★★★ 変更点2: 境用のテクスチャ変数を追加 ★★★
            TEXTURE2D(_PaddyBorderTexture); SAMPLER(sampler_PaddyBorderTexture);
            TEXTURE2D(_RiverTexture);   SAMPLER(sampler_RiverTexture);
            TEXTURE2D(_RiverMask);      SAMPLER(sampler_RiverMask);
            TEXTURE2D(_RoadTexture);    SAMPLER(sampler_RoadTexture);
            TEXTURE2D(_RoadMask);       SAMPLER(sampler_RoadMask);

            CBUFFER_START(UnityPerMaterial)
                float _Height1, _Height2, _Height3, _BlendAmount, _TextureTiling;
                float _RiverDepth;
                float _RoadElevation;
            CBUFFER_END
            
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionHCS : SV_POSITION; float3 positionWS : TEXCOORD0; float2 uv : TEXCOORD1; };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                float riverValue = SAMPLE_TEXTURE2D_LOD(_RiverMask, sampler_RiverMask, IN.uv, 0).r;
                float riverDisplacement = riverValue * _RiverDepth;
                IN.positionOS.y -= riverDisplacement;
                float roadValue = SAMPLE_TEXTURE2D_LOD(_RoadMask, sampler_RoadMask, IN.uv, 0).r;
                float roadDisplacement = roadValue * _RoadElevation;
                IN.positionOS.y = lerp(IN.positionOS.y + roadDisplacement, IN.positionOS.y, riverValue);
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
                
                // ★★★ 変更点3: 棚田の色合成ロジックを更新 ★★★
                // マスクのRチャンネルを「平地」の量、Gチャンネルを「境」の量として読み取る
                half4 paddyMaskColor = SAMPLE_TEXTURE2D(_PaddyMask, sampler_PaddyMask, IN.uv);
                half flatAmount = paddyMaskColor.r;
                half borderAmount = paddyMaskColor.g;

                // 各テクスチャをサンプリング
                half4 flatPaddyColor = SAMPLE_TEXTURE2D(_PaddyTexture, sampler_PaddyTexture, terrainUV);
                half4 borderPaddyColor = SAMPLE_TEXTURE2D(_PaddyBorderTexture, sampler_PaddyBorderTexture, terrainUV);

                // まず平地のテクスチャをベースの地形色に合成
                half4 colorWithPaddy = lerp(terrainColor, flatPaddyColor, flatAmount);
                // 次に境のテクスチャを合成
                colorWithPaddy = lerp(colorWithPaddy, borderPaddyColor, borderAmount);
                
                // 川の色を合成
                half riverAmount = SAMPLE_TEXTURE2D(_RiverMask, sampler_RiverMask, IN.uv).r;
                half4 riverColor = SAMPLE_TEXTURE2D(_RiverTexture, sampler_RiverTexture, terrainUV);
                half4 finalColor = lerp(colorWithPaddy, riverColor, riverAmount);

                // 道の色をさらに合成
                half roadAmount = SAMPLE_TEXTURE2D(_RoadMask, sampler_RoadMask, IN.uv).r;
                half4 roadColor = SAMPLE_TEXTURE2D(_RoadTexture, sampler_RoadTexture, terrainUV);
                finalColor = lerp(finalColor, roadColor, saturate(roadAmount - riverAmount));

                return finalColor;
            }
            ENDHLSL
        }
    }
}