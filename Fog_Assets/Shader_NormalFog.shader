Shader "Unlit/Shader_NormalFog"
{
    Properties
    {
        _MainTex ("_FogNoiseTex", 2D) = "white" {} // Screen texture (input)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" 
        "RenderPipeline"="UniversalPipeline" }
        LOD 100


        Pass
        {
            ZTest Always
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
    

    
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION; 
                half2 uv : TEXCOORD0; 
                // カメラから各ピクセルへの方向のRay
                float4 interpolatedRay : TEXCOORD1; 
            };

            TEXTURE2D(_CameraDepthTexture);
            SAMPLER(sampler_CameraDepthTexture);
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            float4 _FogColor;

            sampler2D _NoiseTex;  
            float _NoiseSpeedX; 
            float _NoiseScale; 

            float _DistanceFogStart;
            float _DistanceFogEnd;


            float _HeightFogEnd;

            float _HeightFogIntensity;

             
            float4x4 _CameraFrustumCorners; 

            // カメラからピクセルへの方向ベクトルを計算します
            float4 getInterpolatedRay(half2 uv) 
            { 
                int index = 0;
                if (uv.x < 0.5 && uv.y < 0.5) {
                    index = 0;
                } else if (uv.x > 0.5 && uv.y < 0.5) {
                    index = 1;
                } else if (uv.x > 0.5 && uv.y > 0.5) {
                    index = 2;
                } else {
                    index = 3;
                }
                return _CameraFrustumCorners[index];
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = TransformObjectToHClip(v.vertex);
                o.uv = v.uv;
                o.interpolatedRay = getInterpolatedRay(v.uv);
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                // 深度テクスチャ内のピクセルのUV座標における非線形深度を取得します
                float depth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture,  sampler_CameraDepthTexture, i.uv); 

                // 線形深度に変換します。
                float linearDepth = LinearEyeDepth(depth, _ZBufferParams); 
             
                // 補間された光線の方向と深度を掛けて、カメラ空間内のピクセルの位置を計算します。
                float3 viewPos = linearDepth * i.interpolatedRay.xyz; 

                // カメラからピクセルまでの距離を計算します。
                float len = length(viewPos);

                // 距離フォグの強さを計算します
                float factor =saturate((_DistanceFogEnd - len) / ( _DistanceFogEnd- _DistanceFogStart)); 

                // ノイズの移動距離
                float2 dist = float2(_NoiseSpeedX,0) * _Time.y; 

                // ノイズテクスチャの偏差値
                float offset = (tex2D(_NoiseTex, i.uv + dist).r - 0.5) * _NoiseScale; 

                // 高さフォグの強さを計算します
                float heightFogFactor = saturate(viewPos.y /_HeightFogEnd/_HeightFogIntensity) ;

                // 距離フォグと高さフォグを結合しますス
                float combinedFogFactor = saturate(factor + heightFogFactor);
               
                // ノイズを加えてフォグの最終値を計算します
                combinedFogFactor = saturate(combinedFogFactor * (1 + offset));

                // 最終色を計算します
                float4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                float4 color = lerp(_FogColor, tex, combinedFogFactor);

                return color;
            }
        
            ENDHLSL
        }
    }
}