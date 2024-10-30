using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;


// Reset the render pipeline assembly
namespace UnityEngine.Rendering.Universal
{
    //Add to the Volume component menu
    [Serializable, VolumeComponentMenu("RiShinchiku/VolumetricFogTest")]

    // VolumeComponentに示すパラメーター
    public class VolFogVolumeComponent : VolumeComponent
    {

        // ボリュメトリックフォグの色
        [Tooltip("ボリュメトリックフォグの色")]
        public ColorParameter baseColor = new ColorParameter(new Color(1, 1, 1, 1));

        // フォグのパラメーター
        // フォグ本体のノイズのテクスチャ
        [Tooltip("フォグ本体のノイズのテクスチャ")]
        public Texture3DParameter densityNoise = new Texture3DParameter(null);

        // ノイズの形、この数は小さくなると、フォグが大きくなります
        [Tooltip("ノイズの形、この数は小さくなると、フォグが大きくなります")]
        public FloatParameter noiseScale = new ClampedFloatParameter(0.002f, 0, 0.1f, true);
        private Vector3Parameter densityNoiseScale = new Vector3Parameter(new Vector3(0.002f, 0.002f, 0.002f));

        // ノイズテクスチャの偏差値
        [Tooltip("ノイズテクスチャの偏差値")]
        public Vector3Parameter densityNoiseOffset = new Vector3Parameter(new Vector3(0.1f, 0.1f, 0.1f));

        // フォグの濃度、この数は大きくなると、フォグの色が黒くなります
        [Tooltip(" フォグの濃度、この数は大きくなると、フォグの色が黒くなります")]
        public ClampedFloatParameter densityScale = new ClampedFloatParameter(2f, 0, 20, true);

        // フォグの縁を侵食される程度のパラメータ
        // フォグの縁に侵食するノイズのテクスチャ
        [Tooltip("フォグの縁に侵食するノイズのテクスチャ")]
        public Texture3DParameter densityErodeNoise = new Texture3DParameter(null);

        // フォグの縁に侵食するノイズの形、この数は大きくなると、フォグを侵食される程度が深くなります
        [Tooltip("フォグの縁に侵食するノイズの形、この数は大きくなると、フォグを侵食される程度が深くなります")]
        public FloatParameter erodeNoiseScale = new ClampedFloatParameter(0.002f, 0, 0.1f, true);
        private Vector3Parameter densityErodeNoiseScale = new Vector3Parameter(new Vector3(0.05f, 0.05f, 0.05f));

        // 侵食するノイズのテクスチャの偏差値
        [Tooltip("侵食するノイズのテクスチャの偏差値")]
        public Vector3Parameter densityErodeNoiseOffset = new Vector3Parameter(new Vector3(0.1f, 0.1f, 0.1f));

        // 侵食するノイズの大きさ
        [Tooltip("侵食するノイズの大きさ")]
        public ClampedFloatParameter densityErode = new ClampedFloatParameter(1f, 0, 10, true);


        // フォグの位置
        [Tooltip("フォグの位置")]
        public Vector3Parameter fogPosition = new Vector3Parameter(new Vector3(0f, 0f, 0f));

        // フォグの範囲
        //                    +--------+ 
        //                   /        /|
        //                  /        / |
        //                 +--------+  <--A max position
        //                 |        |  |
        //B min position --> +      |  +
        //                 |        | /
        //                 |        |/
        //                 +--------+

        // A点
        [Tooltip("フォグの範囲の最大点")]
        public Vector3Parameter boundsMax = new Vector3Parameter(new Vector3(40f, 40f, 40f));

        // B点
        [Tooltip("フォグの範囲の最小点")]
        public Vector3Parameter boundsMin = new Vector3Parameter(new Vector3(-40f, -40f, -40f));

        // ボックスの縁から内側への減衰の程度(XZ)
        [Tooltip("ボックスの縁から内側への減衰の程度(XZ)")]
        public ClampedFloatParameter edgeSoftnessX = new ClampedFloatParameter(20f, 0.5f, 100f, true);

        // ボックスの縁から内側への減衰の程度(Y)
        [Tooltip(" ボックスの縁から内側への減衰の程度(Y)")]
        public ClampedFloatParameter edgeSoftnessY = new ClampedFloatParameter(20f, 0.5f, 100f, true);


        // モル減衰係数、この数は大きくなると、フォグの色が黒くなります
        [Tooltip("モル減衰係数、この数は大きくなると、フォグの色が黒くなります")]
        public MinFloatParameter absorption = new MinFloatParameter(1, 0);

        // 光の追加モル減衰係数、光のエフェクトをよりスムーズになります
        [Tooltip(" 光の追加モル減衰係数、光のエフェクトをよりスムーズになります")]
        public MinFloatParameter lightAbsorption = new MinFloatParameter(1, 0);

        // フォグで反射する光の強さ(普通に言えば、フォグの自発光程度)
        [Tooltip("フォグで反射する光の強さ(普通に言えば、フォグの自発光程度)")]
        public MinFloatParameter lightPower = new MinFloatParameter(1, 0);

        // フォグの移動スピード
        [Tooltip(" フォグの移動スピード")]
        public FloatParameter moveSpeed = new ClampedFloatParameter(0.15f, -0.5f, 0.5f);

        public bool IsActive{
            get{return true;}
            }

        // shaderへのパラメーターの設定  
        public void Load(Material material)
        {
            material.SetColor("_BaseColor", baseColor.value);
            if (densityNoise.value != null)
            {
                material.SetTexture("_DensityNoiseTex", densityNoise.value);
            }
            else
            {
                // Debug.Log("DensityNoiseTex is null!");
                try
                {
                    densityNoise.value = Resources.Load<Texture3D>("Textures/RiShinchiku/worley_noise_02");
                    material.SetTexture("_DensityNoiseTex", densityNoise.value);
                }
                catch
                {
                    Debug.LogError("DensityNoiseTex is not  setted");
                    return;

                }
            }

            if (densityErodeNoise.value != null)
            {
                material.SetTexture("_DensityErodeTex", densityErodeNoise.value);
            }
            else
            {
                // Debug.Log("DensityErodeTex is null!");
                try
                {
                    densityErodeNoise.value = Resources.Load<Texture3D>("Textures/RiShinchiku/fbmNoise_02");
                    material.SetTexture("_DensityErodeTex", densityErodeNoise.value);
                }
                catch
                {
                    Debug.LogError("DensityNoiseTex is not  setted");
                    return;

                }
            }
            densityNoiseScale.value = new Vector3(noiseScale.value, noiseScale.value, noiseScale.value);
            material.SetVector("_DensityNoise_Scale", densityNoiseScale.value);
            material.SetVector("_DensityNoise_Offset", densityNoiseOffset.value);
            material.SetFloat("_DensityScale", densityScale.value);

            densityErodeNoiseScale.value = new Vector3(erodeNoiseScale.value, erodeNoiseScale.value, erodeNoiseScale.value);
            material.SetVector("_DensityErodeNoise_Scale", densityErodeNoiseScale.value);
            material.SetVector("_DensityErodeNoise_Offset", densityErodeNoiseOffset.value);
            material.SetFloat("_DensityErode", densityErode.value);

            material.SetFloat("_Absorption", absorption.value);
            material.SetFloat("_LightAbsorption", lightAbsorption.value);
            material.SetFloat("_LightPower", lightPower.value);

            material.SetVector("_FogPosition", fogPosition.value);
            material.SetVector("_BoundsMax", boundsMax.value + fogPosition.value);
            material.SetVector("_BoundsMin", boundsMin.value + fogPosition.value);

            material.SetVector("_EdgeSoftnessThreshold", new Vector4(edgeSoftnessX.value, edgeSoftnessY.value, 0, 0));

            material.SetFloat("_MoveSpeed", moveSpeed.value);
        }
    }
}