Shader "Custom/Shader_Bloom"
{
    Properties
    { 
         _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags  
        {
            "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline"
        }
        LOD 100

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
        float _Threshold;
        float4 _BloomColor;
        float _Intensity;
        float _ThresholdKnee;
        float4 _BlurOffset;
               CBUFFER_END
       
        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);
        TEXTURE2D(_SourceTex);
        SAMPLER(sampler_SourceTex);
        struct appdata
        {
            float4 positionOS : POSITION;
            float2 texcoord : TEXCOORD0;
        };
              
        struct v2f
        {
            float2 uv : TEXCOORD0;
            float4 positionCS : SV_POSITION;
        };
        v2f vert(appdata v)
        {
            v2f o;
            VertexPositionInputs PositionInputs = GetVertexPositionInputs(v.positionOS.xyz);
            o.positionCS = PositionInputs.positionCS;
            o.uv = v.texcoord;
            return o;
        }
        half3 Sample(float2 uv)
        {
            return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).rgb;
        }
        // Blur algorithm during downsampling
        half3 SampleBox(float2 uv, float2 delta)
        {
            float4 o = 0;
            o = delta.xyxy * float4(-1.0, -1.0, 1.0, 1.0);
            half3 s = 0;
            s = Sample(uv + o.xy) + Sample(uv + o.zy) +
            Sample(uv + o.xw) + Sample(uv + o.zw);
            return s * 0.25f;
        }
        // Extract the light information
        half3 GetLightInfo(half3 color)
        {
            // Get the maximum rgb
            half brightness = Max3(color.r, color.g, color.b);
 
            half softness = clamp(brightness - _Threshold + _ThresholdKnee, 0.0, 2.0 * _ThresholdKnee);
            softness = (softness * softness) / (4.0 * _ThresholdKnee + 1e-4);
            half multiplier = max(brightness - _Threshold, softness) / max(brightness, 1e-4);
            // Get threshold  after multiplication
            color *= multiplier;
            color = max(color, 0);
            return color;
        }
         ENDHLSL
        // PASS0：Get threshold
        pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment GetTexLightInfo
            half4 GetTexLightInfo(v2f i) : SV_Target
            {
                half3 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                return half4(GetLightInfo(tex), 1);
            }
            ENDHLSL
        }

        // Pass1：Blur, in the script with the effect to do downsampling
        pass
        { 
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment BoxBlurfrag
            half4 BoxBlurfrag(v2f i) : SV_Target
            {
                half4 col = half4(SampleBox(i.uv, _BlurOffset).rgb, 1);
                return col;
            }
            ENDHLSL
        }

        // Pass2：Blur and overlay, in the script with the effect to do upsampling
        pass
        {  
            // blend one one
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment AddBlurfrag
            half4 AddBlurfrag(v2f i) : SV_Target
            {
                half4 col = half4(SampleBox(i.uv, _BlurOffset).rgb, 1);
                return col;
            }
            ENDHLSL
        }

        // // Pass3：Merge the two pictures
        pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment Mergefrag
            half4 Mergefrag(v2f i) : SV_Target
            {
                half4 source = SAMPLE_TEXTURE2D(_SourceTex, sampler_SourceTex, i.uv);
                half4 blur = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * _BloomColor * _Intensity;
                half4 final = source + blur;
                return final;
            }
            ENDHLSL
        }

        Pass
        {
            // Pass4 :  debug
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment FragmentProgram

            half4 FragmentProgram(v2f i) : SV_Target
            {
                half4 blue = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                return blue * _Intensity * _BloomColor;
            }
            ENDHLSL
            }

    }

}