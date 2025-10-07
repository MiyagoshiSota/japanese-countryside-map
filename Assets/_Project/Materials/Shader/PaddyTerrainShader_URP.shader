// URP用の棚田シェーダー
Shader "Custom/URP/PaddyTerrainShader_URP"
{
    Properties
    {
        [Header(Texture Settings)]
        _MaskTex("Visualization Mask (R)", 2D) = "white" {}
        _GrassTex("Grass Texture (Albedo)", 2D) = "white" {}
        _BorderTex("Border Texture (Albedo)", 2D) = "white" {}
        _PaddyTex("Paddy Texture (Albedo)", 2D) = "white" {}

        [Header(Normal Map Settings)]
        _GrassNormal("Grass Normal Map", 2D) = "bump" {}
        _BorderNormal("Border Normal Map", 2D) = "bump" {}
        _PaddyNormal("Paddy Normal Map", 2D) = "bump" {}
        _NormalStrength("Normal Map Strength", Range(0.0, 2.0)) = 1.0

        [Header(Triplanar Settings)]
        _TextureScale("Texture Scale", Float) = 50.0
        _TriplanarBlendSharpness("Triplanar Blend Sharpness", Range(1.0, 10.0)) = 5.0

        [Header(PBR Settings)]
        _Smoothness("Smoothness", Range(0.0, 1.0)) = 0.0
        _Metallic("Metallic", Range(0.0, 1.0)) = 0.0
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue" = "Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_MaskTex);         SAMPLER(sampler_MaskTex);
            TEXTURE2D(_GrassTex);        SAMPLER(sampler_GrassTex);
            TEXTURE2D(_BorderTex);       SAMPLER(sampler_BorderTex);
            TEXTURE2D(_PaddyTex);        SAMPLER(sampler_PaddyTex);
            TEXTURE2D(_GrassNormal);     SAMPLER(sampler_GrassNormal);
            TEXTURE2D(_BorderNormal);    SAMPLER(sampler_BorderNormal);
            TEXTURE2D(_PaddyNormal);     SAMPLER(sampler_PaddyNormal);

            CBUFFER_START(UnityPerMaterial)
                float4 _MaskTex_ST;
                float4 _GrassTex_ST;
                float _NormalStrength;
                float _TextureScale;
                float _TriplanarBlendSharpness;
                half _Smoothness;
                half _Metallic;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float4 tangentOS    : TANGENT;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float3 positionWS   : TEXCOORD1;
                float3 normalWS     : TEXCOORD2;
                float3 tangentWS    : TEXCOORD3;
                float3 bitangentWS  : TEXCOORD4;
            };

            half4 TriplanarSample(TEXTURE2D_PARAM(tex, smp), float3 positionWS, float3 normalWS, float scale, float blendSharpness)
            {
                float2 uvX = positionWS.zy / scale;
                float2 uvY = positionWS.xz / scale;
                float2 uvZ = positionWS.xy / scale;
                half4 colX = SAMPLE_TEXTURE2D(tex, smp, uvX);
                half4 colY = SAMPLE_TEXTURE2D(tex, smp, uvY);
                half4 colZ = SAMPLE_TEXTURE2D(tex, smp, uvZ);
                float3 weights = abs(normalWS);
                weights = pow(weights, blendSharpness);
                weights /= (weights.x + weights.y + weights.z);
                return colX * weights.x + colY * weights.y + colZ * weights.z;
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionCS = TransformWorldToHClip(OUT.positionWS);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.tangentWS = TransformObjectToWorldDir(IN.tangentOS.xyz);
                OUT.bitangentWS = cross(OUT.normalWS, OUT.tangentWS) * IN.tangentOS.w;
                OUT.uv = TRANSFORM_TEX(IN.uv, _MaskTex);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half maskValue = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, IN.uv).r;
                half4 grassColor = TriplanarSample(TEXTURE2D_ARGS(_GrassTex, sampler_GrassTex), IN.positionWS, IN.normalWS, _TextureScale, _TriplanarBlendSharpness);
                half4 borderColor = TriplanarSample(TEXTURE2D_ARGS(_BorderTex, sampler_BorderTex), IN.positionWS, IN.normalWS, _TextureScale, _TriplanarBlendSharpness);
                half4 paddyColor = TriplanarSample(TEXTURE2D_ARGS(_PaddyTex, sampler_PaddyTex), IN.positionWS, IN.normalWS, _TextureScale, _TriplanarBlendSharpness);
                half3 grassNormalTS = UnpackNormalScale(TriplanarSample(TEXTURE2D_ARGS(_GrassNormal, sampler_GrassNormal), IN.positionWS, IN.normalWS, _TextureScale, _TriplanarBlendSharpness), _NormalStrength);
                half3 borderNormalTS = UnpackNormalScale(TriplanarSample(TEXTURE2D_ARGS(_BorderNormal, sampler_BorderNormal), IN.positionWS, IN.normalWS, _TextureScale, _TriplanarBlendSharpness), _NormalStrength);
                half3 paddyNormalTS = UnpackNormalScale(TriplanarSample(TEXTURE2D_ARGS(_PaddyNormal, sampler_PaddyNormal), IN.positionWS, IN.normalWS, _TextureScale, _TriplanarBlendSharpness), _NormalStrength);

                float borderBlend = smoothstep(0.1, 0.6, maskValue);
                half4 grassAndBorderColor = lerp(grassColor, borderColor, borderBlend);
                half3 grassAndBorderNormal = lerp(grassNormalTS, borderNormalTS, borderBlend);
                float paddyBlend = smoothstep(0.6, 0.9, maskValue);
                half4 finalAlbedo = lerp(grassAndBorderColor, paddyColor, paddyBlend);
                half3 finalNormalTS = lerp(grassAndBorderNormal, paddyNormalTS, paddyBlend);

                // ★ 修正: 構造体をゼロで初期化
                InputData inputData = (InputData)0;
                inputData.positionWS = IN.positionWS;
                float3x3 TBN = float3x3(IN.tangentWS, IN.bitangentWS, IN.normalWS);
                inputData.normalWS = TransformTangentToWorld(finalNormalTS, TBN);
                inputData.viewDirectionWS = GetWorldSpaceViewDir(IN.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                
                // ★ 修正: 構造体をゼロで初期化
                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = finalAlbedo.rgb;
                surfaceData.metallic = _Metallic;
                surfaceData.smoothness = _Smoothness;
                surfaceData.specular = half3(0, 0, 0); 
                surfaceData.emission = 0;
                surfaceData.occlusion = 1;
                surfaceData.alpha = 1;

                // return UniversalFragmentPBR(inputData, surfaceData);
                return half4(1, 0, 0, 1); // R, G, B, A (赤)
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}