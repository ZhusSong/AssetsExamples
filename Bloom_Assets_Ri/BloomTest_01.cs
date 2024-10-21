using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;


// Reset the render pipeline assembly
namespace UnityEngine.Rendering.Universal
{
    //Add to the Volume component menu
    [Serializable, VolumeComponentMenu("RiShinchiku/BloomTest")]
    public class BloomTest_01 : VolumeComponent
    {
        public FloatParameter Threshold = new ClampedFloatParameter(1.0f, 0.0f, 1.0f);
        public FloatParameter SoftThreshold = new ClampedFloatParameter(0.0f, 0.0f, 1.0f);
        public FloatParameter Intensity= new ClampedFloatParameter(1.0f, 1.0f, 10.0f);
        public ColorParameter BloomColor = new ColorParameter(Color.white);
        public FloatParameter BloomRange = new ClampedFloatParameter(0f, 0f, 15f);
        public IntParameter Iterations = new ClampedIntParameter(1, 1, 8);
        public FloatParameter DownSampling = new ClampedFloatParameter(1f, 1f, 10f);
        public BoolParameter Debug = new BoolParameter(false);
    }
}