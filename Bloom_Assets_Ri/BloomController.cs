using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;



public class BloomController :  ScriptableRendererFeature
{
   [System.Serializable]
    public class BloomSettings
    {
        public RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
         // Set the post-processing Shader
        public Shader shader;
    }
    public BloomSettings settings = new BloomSettings();
    BloomRenderPass bloomScriptablePass;
    public override void Create()
    {   
        // this bloom effect's display name in URP-HighFidelity-Renderer's Add Renderer Feature
        this.name = "Bloom Effect by Ri"; 
        // Initializes  Pass
        bloomScriptablePass =
            new BloomRenderPass(RenderPassEvent.BeforeRenderingPostProcessing, settings.shader); 
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (settings.shader == null)
        {
            Debug.LogError("Bloom shader not assigned in BloomController settings.");
            return;
        }
        renderer.EnqueuePass(bloomScriptablePass);
    }
}
public class BloomRenderPass : ScriptableRenderPass
{
    // Set render Tags
    static readonly string renderTag = "Bloom"; 
    // Set to save image information
    static readonly int tempTargetId = Shader.PropertyToID("_Store a temporary map of the threshold"); 
    
    // Transfer the information to volume
    BloomTest_01 bloomVC;
    // Post-processing using materials
    Material bloomMaterial; 
    
    // Set the current render target
    RenderTargetIdentifier cameraColorTexture; 

    Level[] pyramid;
    const int k_MaxPyramidSize = 16;


    struct Level
    {
        internal int down;
        internal int up;
    }

    static class ShaderIDs
    {
        internal static readonly int BlurOffset = Shader.PropertyToID("_BlurOffset");
        internal static readonly int Threshold = Shader.PropertyToID("_Threshold");
        internal static readonly int ThresholdKnee = Shader.PropertyToID("_ThresholdKnee");
        internal static readonly int Intensity = Shader.PropertyToID("_Intensity");
        internal static readonly int BloomColor = Shader.PropertyToID("_BloomColor");
    }

    public BloomRenderPass(RenderPassEvent evt, Shader bloomShader)
    {
        // Set the location of the render event
        renderPassEvent = evt;
        // Enter Shader information
        var shader = bloomShader;
        
      
        if (shader == null) 
        {
            if(Shader.Find("Custom/Shader_Bloom")==null)
            {
                Debug.LogError("BloomRenderPass has no Shader");
                return;
            }
            else
                shader = Shader.Find("Custom/Shader_Bloom");
        }

        // If new material exists
        bloomMaterial = CoreUtils.CreateEngineMaterial(shader);

        pyramid = new Level[k_MaxPyramidSize];

        for (int i = 0; i < k_MaxPyramidSize; i++)
        {
            pyramid[i] = new Level
            {
                down = Shader.PropertyToID("_BlurMipDown" + i),
                up = Shader.PropertyToID("_BlurMipUp" + i)
            };
        }
        
    }

    public void Setup(in RenderTargetIdentifier currentTarget)
    {
        this.cameraColorTexture = currentTarget;
    }

    // The post-processing logic and rendering core functions are basically equivalent 
    // to the OnRenderImage function of the built-in pipeline
    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
      
        if (bloomMaterial == null)
        {
            Debug.LogError("Failed to created material");
            return;
        }

      
        // Determine whether to enable post-processing
        if (!renderingData.cameraData.postProcessEnabled)
        {
            return;
        }
        // Render Settings
        // Pass in the volume
        var stack = VolumeManager.instance.stack; 
        bloomVC = stack.GetComponent< BloomTest_01>(); 
        if (bloomVC == null)
        {
            Debug.LogError("Failed to get Volume ");
            return;
        }

        // Set the render label
        var cmd = CommandBufferPool.Get(renderTag); 
       

        // Set the rendering function
        Render(cmd, ref renderingData); 
        context.ExecuteCommandBuffer(cmd); 
        CommandBufferPool.Release(cmd); 
    }

    void Render(CommandBuffer cmd, ref RenderingData renderingData)
    {
        // Get camera properties
        ref var cameraData = ref renderingData.cameraData; 
        // Get  render image
        var source =cameraData.renderer.cameraColorTargetHandle; 
        // Render the resulting image
        int buffer0 = tempTargetId; 

        // Obtain the scaled pixel width and height of the camera, dividing them by the downsampling value, 
        // and cast the results to integers.
        int tw = (int)(cameraData.camera.scaledPixelWidth / bloomVC.DownSampling.value);
        int th = (int)(cameraData.camera.scaledPixelHeight / bloomVC.DownSampling.value);

        // Calculates the offset for the bloom effect, using the bloom range divided by the screen width and height.
        Vector4 BlurOffset = new Vector4(bloomVC.BloomRange.value / (float)Screen.width,
        bloomVC.BloomRange.value / (float)Screen.height, 0, 0);
        // Set various parameters of bloomMaterial
        bloomMaterial.SetVector(ShaderIDs.BlurOffset, BlurOffset);
        bloomMaterial.SetFloat(ShaderIDs.Threshold, bloomVC.Threshold.value);
        bloomMaterial.SetFloat(ShaderIDs.ThresholdKnee, bloomVC.SoftThreshold.value);
        bloomMaterial.SetFloat(ShaderIDs.Intensity, bloomVC.Intensity.value);
        bloomMaterial.SetColor(ShaderIDs.BloomColor, bloomVC.BloomColor.value);

        // Get a threshold 
        // For the pass0 in shader
        // Use GetTemporaryRT() to get a temporary render texture with the same dimensions as the screen
        cmd.GetTemporaryRT(
            buffer0, 
            cameraData.camera.scaledPixelWidth, cameraData.camera.scaledPixelHeight, 0,
            FilterMode.Trilinear, RenderTextureFormat.Default);
        // Copy the contents of the source render target into emporary render texture(buffer0)
        cmd.Blit(source, buffer0); 
        // Do pass0 of shader
        cmd.Blit(buffer0, source, bloomMaterial, 0); 

        // Do Downsampling pass
        // For the pass1 in shader
        // Copy the source render target to a new render target for downsampling pass
        RenderTargetIdentifier lastDown = source; 

        // Enter the downsampling loop, reduce the image size by half each time to blur it,
        // and use  Mathf.Max() to avoid the image size being less than 1
        for (int i = 0; i < bloomVC.Iterations.value; i++)
        {
            int mipDown = pyramid[i].down;
            int mipUp = pyramid[i].up;
            // Allocates a temporary render texture mipDown for the downsampling pass 
            // and applies bilinear filtering for smoother results.
            cmd.GetTemporaryRT(mipDown, tw, th, 0, FilterMode.Bilinear);
            cmd.GetTemporaryRT(mipUp, tw, th, 0, FilterMode.Bilinear);
            // Do pass1 of shader
            cmd.Blit(lastDown, mipDown, bloomMaterial, 1);
            // After each iteration, lastDown is updated to point to the new created downsampled texture (mipDown). 
            // This ensures the next iteration blits from this texture, making it progressively more blurred.
            lastDown = mipDown;
            tw = Mathf.Max(tw / 2, 1);
            th = Mathf.Max(th / 2, 1);
        }

      
        // Up sampling pass
        // For the pass2 in shader
        // Get the final downsampled texture which will be upsampled back to higher resolutions.
        int lastUp = pyramid[bloomVC.Iterations.value - 1].down;

        // Enter the upsampling loop, iterates downsampled texture up to the highest resolution.
        for (int i = bloomVC.Iterations.value - 2; i >= 0; i--)
        {
            int mipUp =pyramid[i].up;
            // Do pass2 of shader
            cmd.Blit(lastUp, mipUp, bloomMaterial, 2);
            lastUp = mipUp;
        }
        // If you want to see the effect of upsampling and downsampling, set debug to true to do pass4 of shader
        if (bloomVC.Debug.value)
        {
            cmd.Blit(lastUp, source, bloomMaterial, 4);
        }
        // Merge
        else
        {
            // Set the calculated buffer0 to the source render target 
            cmd.SetGlobalTexture("_SourceTex", buffer0);
            // Do pass3 of shader
            cmd.Blit(lastUp, source, bloomMaterial, 3);
        }

        // Cleanup
        for (int i = 0; i < bloomVC.Iterations.value; i++)
        {
            if (pyramid[i].down != lastUp)
                cmd.ReleaseTemporaryRT(pyramid[i].down);
            if (pyramid[i].up != lastUp)
                cmd.ReleaseTemporaryRT(pyramid[i].up);
        }
    }
}