using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace HauntedPSX.RenderPipelines.PSX.Runtime
{
    [Serializable, VolumeComponentMenu("HauntedPS1/Color Adjustments")]
    public sealed class ColorAdjustmentsVolume : VolumeComponent
    {
        [Tooltip("Adjusts the intensity of the color.")]
        public FloatParameter intensity = new FloatParameter(1f);

        [Tooltip("Expands or shrinks the overall range of tonal values.")]
        public ClampedFloatParameter contrast = new ClampedFloatParameter(0f, -100f, 100f);

        [Tooltip("Tint the render by multiplying a color.")]
        public ColorParameter colorFilter = new ColorParameter(Color.white, true, false, true);

        [Tooltip("Shift the hue of all colors.")]
        public ClampedFloatParameter hueShift = new ClampedFloatParameter(0f, -180f, 180f);

        [Tooltip("Pushes the intensity of all colors.")]
        public ClampedFloatParameter saturation = new ClampedFloatParameter(0f, -100f, 100f);

        //static TonemapperVolume s_Default = null;
        //public static TonemapperVolume @default
        //{
        //    get
        //    {
        //        if (s_Default == null)
        //        {
        //            s_Default = ScriptableObject.CreateInstance<TonemapperVolume>();
        //            s_Default.hideFlags = HideFlags.HideAndDontSave;
        //        }
        //        return s_Default;
        //    }
        //}
    }
}