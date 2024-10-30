using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System;

// VolFogと大体同じに
public class NormalFogRenderFeature : ScriptableRendererFeature
{

    [SerializeField]
    private Shader shader;
    [SerializeField]
    private RenderPassEvent evt = RenderPassEvent.AfterRenderingTransparents;
    private Material matInstance;
    private NormalFogRenderPass pass;


    /// <inheritdoc/>
    public override void Create()
    {
        pass = new NormalFogRenderPass();
        pass.renderPassEvent = evt;
    }

    // Here you can inject one or multiple render passes in the renderer.
    // This method is called when setting up the renderer once per-camera.
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
public class NormalFogRenderPass : ScriptableRenderPass
{
    const string customPassTag = "Normal Fog Render Pass";
    private NormalFogVolumeComponent NormalFogVC;
    public Material mat { get; set; }
    private RTHandle tempRT;

    public override void Execute(ScriptableRenderContext ctx, ref RenderingData data)
    {
        VolumeStack stack = VolumeManager.instance.stack;
        NormalFogVC = stack.GetComponent<NormalFogVolumeComponent>();

        CommandBuffer command = CommandBufferPool.Get(customPassTag);

        Render(command,  data);

        ctx.ExecuteCommandBuffer(command);
        CommandBufferPool.Release(command);
    }

    // Cleanup any allocated resources that were created during the execution of this render pass.
    public void Render(CommandBuffer command,  RenderingData data)
    {
        if (NormalFogVC.IsActive)
        {
            NormalFogVC.Load(mat, GetCameraFrustumCorners(data));
            RenderTextureDescriptor opaqueDesc = data.cameraData.cameraTargetDescriptor;
            opaqueDesc.depthBufferBits = 0;
            if (tempRT == null)
            {
                tempRT = RTHandles.Alloc(opaqueDesc, name: "NormalFogTempRT");
            }
            RenderTargetIdentifier sourceRT = data.cameraData.renderer.cameraColorTargetHandle;

            // command.GetTemporaryRT(tempRT.name, opaqueDesc);
            command.Blit(sourceRT, tempRT, mat);
            command.Blit(tempRT, sourceRT);
        }
    }
    /// <summary>
    /// カメラの視錐台のコーナーをワールド空間で計算します
    /// </summary>
    /// <param name="data"></param>
    /// <returns>
    /// Row 0:カメラからnear clip の 左下隅までのベクトル
    /// Row 1:カメラからnear clip の 右下隅までのベクトル
    /// Row 2:カメラからnear clip の 右上隅までのベクトル
    /// Row 3:カメラからnear clip の 左上隅までのベクトル
    /// </returns>
    private Matrix4x4 GetCameraFrustumCorners(RenderingData data)
    {
        // カメラを取得します
        Camera camera = data.cameraData.camera;

        // 4x4 の単位行列
        Matrix4x4 camerafrustumCorners = Matrix4x4.identity;

        // カメラの視野角 (度数)
        float fov = camera.fieldOfView;

        // カメラの近クリップ面
        float near = camera.nearClipPlane;

        // カメラ画面の縦横比
        float aspect = camera.aspect;

        // 角度をラジアンに変換
        // 近クリップ面の高さの半分を計算
        float halfHeight = near * Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);

        // 近クリップ面の中心から右方向へのベクトル (カメラ空間の右側で、ワールド空間ではありません)
        Vector3 toRight = camera.transform.right * halfHeight * aspect;

        // 近クリップ面の中心から上方向へのベクトル (カメラ空間の上側で、ワールド空間ではありません)
        Vector3 toTop = camera.transform.up * halfHeight;

        // カメラから近クリップ面の左上へのベクトル
        Vector3 topLeft = camera.transform.forward * near + toTop - toRight;
        float scale = topLeft.magnitude / near;
        topLeft.Normalize();
        topLeft *= scale;

        // カメラから近クリップ面の右上へのベクトル
        Vector3 topRight = camera.transform.forward * near + toRight + toTop;
        topRight.Normalize();
        topRight *= scale;

        // カメラから近クリップ面の左下へのベクトル
        Vector3 bottomLeft = camera.transform.forward * near - toTop - toRight;
        bottomLeft.Normalize();
        bottomLeft *= scale;

        // カメラから近クリップ面の右下へのベクトル
        Vector3 bottomRight = camera.transform.forward * near + toRight - toTop;
        bottomRight.Normalize();
        bottomRight *= scale;

        camerafrustumCorners.SetRow(0, bottomLeft);
        camerafrustumCorners.SetRow(1, bottomRight);
        camerafrustumCorners.SetRow(2, topRight);
        camerafrustumCorners.SetRow(3, topLeft);

        return camerafrustumCorners;
    }
}


