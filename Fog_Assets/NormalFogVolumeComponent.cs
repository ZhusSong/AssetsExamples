using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

// VolFogと大体同じに
// Reset the render pipeline assembly
namespace UnityEngine.Rendering.Universal
{
    //Add to the Volume component menu
    [Serializable, VolumeComponentMenu("RiShinchiku/NormalFogTest")]
    public class NormalFogVolumeComponent : VolumeComponent
    {
        // フォグの色
        [Tooltip("フォグの色")]
        public ColorParameter fogColor = new ColorParameter(Color.white);

        // フォグが掛かり始める距離
        [Tooltip("フォグが掛かり始める距離")]
        public FloatParameter distanceFogStart = new ClampedFloatParameter(1.0f, 0.0f, 1000.0f);

        // フォグが完全に掛かる距離
        [Tooltip("フォグが完全に掛かる距離")]
        public FloatParameter distanceFogEnd = new ClampedFloatParameter(1.0f, 0.0f, 1000.0f);

        // 高さフォグの始点は０に設定し、このパラメーターは高さフォグの終点です。普通に言えば、高さフォグの高さということです。
        [Tooltip("高さフォグの始点")]
        public FloatParameter heightFogEnd = new ClampedFloatParameter(1.0f, 1.0f, 100.0f);

        // 高さフォグの濃度       
        [Tooltip("高さフォグの濃度 ")]
        public FloatParameter heightFogIntensity = new ClampedFloatParameter(0.0f, 0.0f, 10000.0f);

        // ノイズのテクスチャ
        [Tooltip("ノイズのテクスチャ")]
        public TextureParameter noiseTex = new TextureParameter(null);

        // ノイズの移動の方向とスピード
        [Tooltip("ノイズの移動の方向とスピード")]
        public FloatParameter noiseSpeedX = new ClampedFloatParameter(0.0f, -10.0f, 10.0f);

        // ノイズの大小
        [Tooltip("ノイズの大小")]
        public FloatParameter noiseScale = new ClampedFloatParameter(1.0f, 0.0f, 10.0f);


        public bool IsActive{
            get{return true;}
            }

        /// <summary>
        /// shaderへのパラメーターの設定
        /// </summary>
        /// <param name="material"></param>
        /// <param name="data"></param>
        /// <param name="cameraFrustumCorners">各行に near clip の四隅が格納された行列
        ///                                    Row 0:near clip の 左下隅
        ///                                    Row 1:near clip の 右下隅 
        ///                                    Row 2:near clip の 右上隅 
        ///                                    Row 3:near clip の 左上隅 </param>
        public void Load(Material material, Matrix4x4 cameraFrustumCorners)
        {
            material.SetColor("_FogColor", fogColor.value);
            material.SetMatrix("_CameraFrustumCorners", cameraFrustumCorners);

            material.SetFloat("_DistanceFogStart", distanceFogStart.value);
            material.SetFloat("_DistanceFogEnd", distanceFogEnd.value);

            material.SetFloat("_HeightFogEnd", heightFogEnd.value);
            material.SetFloat("_HeightFogIntensity", heightFogIntensity.value);

            material.SetTexture("_NoiseTex", noiseTex.value);
            material.SetFloat("_NoiseSpeedX", noiseSpeedX.value);
            material.SetFloat("_NoiseScale", noiseScale.value);

        }
    }
}


