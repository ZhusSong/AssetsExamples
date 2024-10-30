using System.Collections;
using System.Collections.Generic;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine;

public class VolumtericFogController : ScriptableRendererFeature
{
    // Start is called before the first frame update


    // shaderの指定
    [SerializeField]
    private Shader shader;

    // pipelineの設定
    [SerializeField]
    private RenderPassEvent evt = RenderPassEvent.AfterRenderingPostProcessing;
    private Material matInstance;
    private VolFogRenderPass pass;


    // VolFogRenderPass VolFogScriptablePass;
    public override void Create()
    {
        pass = new VolFogRenderPass();
        pass.renderPassEvent = evt;
    }

    // URPのAddRenderPasses()をオーバーライドして、ブルームのレンダーパスをレンダリングパイプラインに追加します
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (shader == null)
            return;
        if (matInstance == null)
        {
            matInstance = CoreUtils.CreateEngineMaterial(shader);
        }
        pass.mat = matInstance; // Setup the render pass
        renderer.EnqueuePass(pass);
    }
}


public class VolFogRenderPass : ScriptableRenderPass
{
    // レンダーパスのTag
    const string customPassTag = "Vol Fog Render Pass";
    private VolFogVolumeComponent volFogVC;
    public Material mat { get; set; }
    private RenderTargetIdentifier sourceRT;
    private RTHandle tempRT;


    public override void Execute(ScriptableRenderContext ctx, ref RenderingData data)
    {

        VolumeStack stack = VolumeManager.instance.stack;
        volFogVC = stack.GetComponent<VolFogVolumeComponent>();
        CommandBuffer command = CommandBufferPool.Get(customPassTag);
        Render(command,  data);
        ctx.ExecuteCommandBuffer(command);
        CommandBufferPool.Release(command);
    }
    public void Render(CommandBuffer command, RenderingData data)
    {
        if (volFogVC.IsActive)
        {
            volFogVC.Load(mat);
            RenderTextureDescriptor opaqueDesc = data.cameraData.cameraTargetDescriptor;
            opaqueDesc.depthBufferBits = 0;
            // Ensure tempRT is only allocated once
            if (tempRT == null)
            {
                tempRT = RTHandles.Alloc(opaqueDesc, name: "VolumetricFogTempRT");
            }
            RenderTargetIdentifier sourceRT = data.cameraData.renderer.cameraColorTargetHandle;

            command.Blit(sourceRT, tempRT, mat);
            command.Blit(tempRT, sourceRT);
        }
    }
}