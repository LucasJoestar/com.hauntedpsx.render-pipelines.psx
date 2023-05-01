using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace HauntedPSX.RenderPipelines.PSX.Runtime
{
    /// <summary>
    /// Renders a color grading LUT texture.
    /// </summary>
    public class ColorGradingLutRenderer
    {
        readonly Material m_LutGenerationMaterial;
        readonly GraphicsFormat m_LutFormat;

        ProfilingSampler m_ProfilingSampler = null;
        static readonly int s_LutID = Shader.PropertyToID("_ColorGradingLUT");
        int m_LutSize = 32;

        public ColorGradingLutRenderer()
        {
            m_ProfilingSampler = new ProfilingSampler(nameof(ColorGradingLutRenderer));

            Material Load(Shader shader)
            {
                if (shader == null)
                {
                    Debug.LogError($"Missing shader. {GetType().DeclaringType.Name} render pass will not execute. Check for missing reference in the renderer resources.");
                    return null;
                }

                return CoreUtils.CreateEngineMaterial(shader);
            }

            m_LutGenerationMaterial = Load(Shader.Find("Hidden/HauntedPS1/ColorGradingLUTBuilder"));
            m_LutFormat = GraphicsFormat.R8G8B8A8_UNorm;
        }

        public void RenderLUT(ScriptableRenderContext context)
        {
            var cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, m_ProfilingSampler))
            {
                // Fetch all color grading settings
                var stack = VolumeManager.instance.stack;
                var colorAdjustments = stack.GetComponent<ColorAdjustmentsVolume>();
                var curves = stack.GetComponent<ColorCurvesVolume>();

                // Prepare texture & material
                int lutHeight = m_LutSize;
                int lutWidth = lutHeight * lutHeight;
                var format = m_LutFormat;
                var material = m_LutGenerationMaterial;
                var desc = new RenderTextureDescriptor(lutWidth, lutHeight, format, 0);
                desc.vrUsage = VRTextureUsage.None;
                cmd.GetTemporaryRT(s_LutID, desc, FilterMode.Bilinear);

                // Prepare data
                var hueSatCon = new Vector4(colorAdjustments.hueShift.value / 360f, colorAdjustments.saturation.value / 100f + 1f, colorAdjustments.contrast.value / 100f + 1f, 0f);

                var lutParameters = new Vector4(lutHeight, 0.5f / lutWidth, 0.5f / lutHeight,
                    lutHeight / (lutHeight - 1f));

                // Fill in constants
                material.SetVector(ShaderConstants._Lut_Params, lutParameters);
                material.SetVector(ShaderConstants._ColorFilter, colorAdjustments.colorFilter.value.linear);
                material.SetVector(ShaderConstants._HueSatCon, hueSatCon);

                // YRGB curves
                material.SetTexture(ShaderConstants._CurveMaster, curves.master.value.GetTexture());
                material.SetTexture(ShaderConstants._CurveRed, curves.red.value.GetTexture());
                material.SetTexture(ShaderConstants._CurveGreen, curves.green.value.GetTexture());
                material.SetTexture(ShaderConstants._CurveBlue, curves.blue.value.GetTexture());

                // Secondary curves
                material.SetTexture(ShaderConstants._CurveHueVsHue, curves.hueVsHue.value.GetTexture());
                material.SetTexture(ShaderConstants._CurveHueVsSat, curves.hueVsSat.value.GetTexture());
                material.SetTexture(ShaderConstants._CurveLumVsSat, curves.lumVsSat.value.GetTexture());
                material.SetTexture(ShaderConstants._CurveSatVsSat, curves.satVsSat.value.GetTexture());

                material.SetMatrix(ShaderConstants._FullscreenProjMat, GL.GetGPUProjectionMatrix(Matrix4x4.identity, true));

                // Render the lut
                cmd.Blit(null, s_LutID, material);
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public void PushGlobalParameters(CommandBuffer cmd)
        {
            int lutHeight = m_LutSize;
            int lutWidth = lutHeight * lutHeight;
            var lutParameters = new Vector4(1.0f / lutWidth, 1.0f / lutHeight, lutHeight - 1, 0);

            var colorAdjustments = VolumeManager.instance.stack.GetComponent<ColorAdjustmentsVolume>();
            float intensity = colorAdjustments.intensity.value;
            cmd.SetGlobalFloat(ShaderConstants._Intensity, intensity);
            cmd.SetGlobalVector(ShaderConstants._ColorGradingLUTParams, lutParameters);
            cmd.SetGlobalTexture(s_LutID, s_LutID);
        }

        public void OnFinishRendering(CommandBuffer cmd)
        {
            cmd.ReleaseTemporaryRT(s_LutID);
        }

        public void Cleanup()
        {
            CoreUtils.Destroy(m_LutGenerationMaterial);
        }

        static class ShaderConstants
        {
            public static readonly int _ColorGradingLUTParams = Shader.PropertyToID("_ColorGradingLUTParams");
            public static readonly int _Lut_Params = Shader.PropertyToID("_Lut_Params");
            public static readonly int _FullscreenProjMat = Shader.PropertyToID("_FullscreenProjMat");

            // Color Adjustments
            public static readonly int _ColorFilter = Shader.PropertyToID("_ColorFilter");
            public static readonly int _HueSatCon = Shader.PropertyToID("_HueSatCon");
            public static readonly int _Intensity = Shader.PropertyToID("_Intensity");

            // Color Curves
            public static readonly int _CurveMaster = Shader.PropertyToID("_CurveMaster");
            public static readonly int _CurveRed = Shader.PropertyToID("_CurveRed");
            public static readonly int _CurveGreen = Shader.PropertyToID("_CurveGreen");
            public static readonly int _CurveBlue = Shader.PropertyToID("_CurveBlue");
            public static readonly int _CurveHueVsHue = Shader.PropertyToID("_CurveHueVsHue");
            public static readonly int _CurveHueVsSat = Shader.PropertyToID("_CurveHueVsSat");
            public static readonly int _CurveLumVsSat = Shader.PropertyToID("_CurveLumVsSat");
            public static readonly int _CurveSatVsSat = Shader.PropertyToID("_CurveSatVsSat");
        }
    }
}