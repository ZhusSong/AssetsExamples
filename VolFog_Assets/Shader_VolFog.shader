Shader "Unlit/Shader_VolFog"
{
    Properties
    {
        _MainTex("Main Texture", 2D) = "white"{}
    }
    SubShader{
        Tags
        {
            "RenderPipeline" = "UniversalRenderPipeline"
        }
        pass
        {
            Cull Off
            ZTest Always
            ZWrite Off
            
            HLSLPROGRAM

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/SpaceTransforms.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
                
            #pragma vertex Vertex
            #pragma fragment Pixel

            Texture2D _MainTex;
            SamplerState sampler_MainTex;
            half4 _BaseColor;


            // フォグの位置と範囲
            float3 _FogPosition;
            float3 _BoundsMax;
            float3 _BoundsMin;

            // フォグのパラメーター
            sampler3D _DensityNoiseTex;
            float _DensityScale;
            float3 _DensityNoise_Scale;
            float3 _DensityNoise_Offset;
            
            // 減衰の程度
            float2 _EdgeSoftnessThreshold;

            // 侵食ノイズのパラメーター
            sampler3D _DensityErodeTex;
            float3 _DensityErodeNoise_Scale;
            float3 _DensityErodeNoise_Offset;
            float _DensityErode;

            // 光のパラメーター
            float _Absorption;
            float _LightAbsorption;
            float _LightPower;

            // 移動のスピード
            float _MoveSpeed;

            struct vertexInput
            {
                float4 vertex: POSITION;
                float2 uv: TEXCOORD0;
            };

            struct vertexOutput{
                float4 pos: SV_POSITION;
                float2 uv: TEXCOORD0;
            };

          
            //　RayMarching演算
            //  boundsMin: ボックスの範囲
            //  boundsMax:ボックスの範囲
            //  rayOrigin:Rayの始点
            //  rayDir:Rayの方向
            float2 rayBoxDst(float3 boundsMin, float3 boundsMax, float3 rayOrigin, float3 rayDir)
            {
                /*  boundsMinとboundsMaxをボックスを設定して
                    rayOriginからrayDirにrayを発して， rayとボックスの縁の距離を計算する
                    参考：
                    about ray box algorithm 
                    https://jcgt.org/published/0007/03/04/ 　*/

                float3 t0 = (boundsMin - rayOrigin) / rayDir;
                float3 t1 = (boundsMax - rayOrigin) / rayDir;
                float3 tmin = min(t0, t1);
                float3 tmax = max(t0, t1);

                float dstA = max(max(tmin.x, tmin.y), tmin.z);
                float dstB = min(tmax.x, min(tmax.y, tmax.z));

                float dstToBox = max(0, dstA);
                float dstInsideBox = max(0, dstB - dstToBox);
                return float2(dstToBox, dstInsideBox);
            }

            // サンプル演算
            // position:フォグボックス内の点のworld position
            float sampleDensity(float3 position)
            {
                // カメラとの距離
                float distanceFromCamera = distance(position, _WorldSpaceCameraPos.xyz);

                // テクスチャの移動
                float3 uvw = position* _DensityNoise_Scale + _DensityNoise_Offset*_Time*_MoveSpeed;
                float density = tex3D(_DensityNoiseTex, uvw).r;

                 // フォグに侵食します
                float3 erodeUVW = position * _DensityErodeNoise_Scale + _DensityErodeNoise_Offset;
                float erode = tex3D(_DensityErodeTex, erodeUVW).r * _DensityErode;
                    
                // ボックスの縁から内側への減衰
                float edgeToX = min(_EdgeSoftnessThreshold.x, min(position.x - _BoundsMin.x, _BoundsMax.x - position.x));
                float edgeToZ = min(_EdgeSoftnessThreshold.x, min(position.z - _BoundsMin.z, _BoundsMax.z - position.z));
                float edgeToY = min(_EdgeSoftnessThreshold.y, min(position.y - _BoundsMin.y, _BoundsMax.y - position.y));
                float softness = edgeToX/ _EdgeSoftnessThreshold.x * edgeToZ / _EdgeSoftnessThreshold.x * edgeToY / _EdgeSoftnessThreshold.y;
                
                // すべての効果を計算します
                // max(0, density-erode):フォグ本体から侵食される部分を減算します
                density = max(0, density-erode)  * _DensityScale * softness * softness;
                return density;
            }
                
            // 光のシミュレーション
            // position:サンプルの始点
            // stepCount:計算の回数
            float LightPathDensity(float3 position, int stepCount)
            {
                 /* sample density from given point to light 
                    within target step count */
                // mainライトの方向
                float3 dirToLight = _MainLightPosition.xyz;
                    
                // ボックスの内側までの距離を計算する
                float dstInsideBox = rayBoxDst(_BoundsMin,_BoundsMax, position, 1/dirToLight).y;
                    
                // サンプル演算
                float stepSize = dstInsideBox / stepCount;
                float totalDensity = 0;
                float3 stepVec = dirToLight * stepSize;
                for(int i = 0; i < stepCount; i ++)
                {
                    position += stepVec;
                    totalDensity += max(0, sampleDensity(position) * stepSize);
                }
                return totalDensity;
            }

            // HCS座標を世界座標に転換します
            float3 GetWorldPosition(float3 positionHCS)
            {
                // get world space position 
                float2 UV = positionHCS.xy / _ScaledScreenParams.xy;
                #if UNITY_REVERSED_Z
                    real depth = SampleSceneDepth(UV);
                #else
                    real depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, SampleSceneDepth(UV));
                #endif
                return ComputeWorldSpacePosition(UV, depth, UNITY_MATRIX_I_VP);
            }

            vertexOutput Vertex(vertexInput v)
            {
                vertexOutput o;
                o.pos = TransformObjectToHClip(v.vertex.xyz);
                o.uv = v.uv;
                return o;
            }

            // 最終色の計算
            half4 Pixel(vertexOutput IN): SV_TARGET
            {
                // MainTexをサンプルします
                half4 albedo = _MainTex.Sample(sampler_MainTex, IN.uv);

                 // 世界座標を得ます
                float3 worldPosition = GetWorldPosition(IN.pos);
                float3 rayPosition = _WorldSpaceCameraPos.xyz;
                float3 worldViewVector = worldPosition - rayPosition;
                float3 rayDir = normalize(worldViewVector);

                // RayMarching演算
                float2 rayBoxInfo = rayBoxDst(_BoundsMin, _BoundsMax, rayPosition, rayDir);
                float dstToBox = rayBoxInfo.x;
                float dstInsideBox = rayBoxInfo.y;
                float dstToOpaque = length(worldViewVector);
                float dstLimit = min(dstToOpaque - dstToBox, dstInsideBox);

                // フォグのサンプル
                // comment picture's stepCount is 7
                //  <-stepSize->
                // |______0______|______1______|________2______|______3______|_______4_______|______5______|_______6____|  
              
                // サンプルの回数
                int stepCount = 16;     
                // 毎回サンプルの長さ            
                float stepSize = dstInsideBox / stepCount;     
                // stepのベクトル             
                float3 stepVec = rayDir * stepSize;    
                // サンプルの始点                 
                float3 currentPoint = rayPosition + dstToBox * rayDir;   
                
                // フォグの総濃度
                float totalDensity = 0;

                // 現在のサンプル点
                float dstTravelled = 0;

                // サンプル点の光の強さ
                float lightIntensity = 0;

                // サンプル演算
                for(int i = 0; i < stepCount; i ++)
                {
                    if(dstTravelled < dstLimit)
                    {
                        float Dx = sampleDensity(currentPoint) * stepSize;
                        totalDensity += Dx;

                        float lightPathDensity = LightPathDensity(currentPoint, 1);

                        lightIntensity += exp(-(lightPathDensity * _LightAbsorption + totalDensity * _Absorption)) * Dx; 
                        lightIntensity = saturate(lightIntensity); 

                        currentPoint += stepVec;
                        dstTravelled += stepSize;
                        continue;
                    }
                        break;
                }
                    
                // フォグの色=mainライトの色＊サンプル点の光の強さ＊フォグを設定した色＊フォグで反射する光の強さ
                float3 fogColor = _MainLightColor.xyz * lightIntensity * _BaseColor.xyz * _LightPower;

                // 光吸収の量を加えて最終色の計算
                return half4(albedo * exp(-totalDensity * _Absorption) + fogColor, 1);
            }
        ENDHLSL
        }
    }
}