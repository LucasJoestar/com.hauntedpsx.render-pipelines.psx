Shader "Hidden/HauntedPS1/ColorGradingLUTBuilder"
{
    HLSLINCLUDE
    #include "Packages/com.hauntedpsx.render-pipelines.psx/Runtime/ShaderLibrary/ShaderVariables.hlsl"
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/SpaceTransforms.hlsl"

    float4 _Lut_Params;         // x: lut_height, y: 0.5 / lut_width, z: 0.5 / lut_height, w: lut_height / lut_height - 1
    half4 _ColorFilter;         // xyz: color, w: unused
    float4 _HueSatCon;          // x: hue shift, y: saturation, z: contrast, w: unused

    TEXTURE2D(_CurveMaster);
    TEXTURE2D(_CurveRed);
    TEXTURE2D(_CurveGreen);
    TEXTURE2D(_CurveBlue);

    TEXTURE2D(_CurveHueVsHue);
    TEXTURE2D(_CurveHueVsSat);
    TEXTURE2D(_CurveSatVsSat);
    TEXTURE2D(_CurveLumVsSat);

    SAMPLER(sampler_LinearClamp);

    struct Attributes
    {
        float4 positionOS : POSITION;
        float2 uv : TEXCOORD0;
    };

    struct Varyings
    {
        float4 positionCS : SV_POSITION;
        float2 uv : TEXCOORD0;
    };

    Varyings Vertex(Attributes input)
    {
        Varyings output;
        output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
        output.uv = input.uv;

        return output;
    }

    half GetLuminance(half3 linearRgb)
    {
        return dot(linearRgb, float3(0.2126729, 0.7151522, 0.0721750));
    }

    half EvaluateCurve(TEXTURE2D(curve), float t)
    {
        half x = SAMPLE_TEXTURE2D(curve, sampler_LinearClamp, float2(t, 0.0)).x;
        return saturate(x);
    }

    half4 Frag(Varyings input) : SV_Target
    {
        float3 colorLinear = GetLutStripValue(input.uv, _Lut_Params);

        // Do contrast in log after white balance
        float3 colorLog = LinearToLogC(colorLinear);
        colorLog = (colorLog - ACEScc_MIDGRAY) * _HueSatCon.z + ACEScc_MIDGRAY;
        colorLinear = LogCToLinear(colorLog);

        // Color filter is just an unclipped multiplier
        colorLinear *= _ColorFilter.xyz;

        // Do NOT feed negative values to the following color ops
        colorLinear = max(0.0, colorLinear);

        // HSV operations
        float satMult;
        float3 hsv = RgbToHsv(colorLinear);
        {
            // Hue Vs Sat
            satMult = EvaluateCurve(_CurveHueVsSat, hsv.x) * 2.0;

            // Sat Vs Sat
            satMult *= EvaluateCurve(_CurveSatVsSat, hsv.y) * 2.0;

            // Lum Vs Sat
            satMult *= EvaluateCurve(_CurveLumVsSat, Luminance(colorLinear)) * 2.0;

            // Hue Shift & Hue Vs Hue
            float hue = hsv.x + _HueSatCon.x;
            float offset = EvaluateCurve(_CurveHueVsHue, hue) - 0.5;
            hue += offset;
            hsv.x = RotateHue(hue, 0.0, 1.0);
        }
        colorLinear = HsvToRgb(hsv);

        // Global saturation
        float luma = GetLuminance(colorLinear);
        colorLinear = luma.xxx + (_HueSatCon.yyy * satMult) * (colorLinear - luma.xxx);

        // YRGB curves
        {
            const float kHalfPixel = (1.0 / 128.0) / 2.0;
            float3 c = colorLinear;

            // Y (master)
            c += kHalfPixel.xxx;
            float mr = EvaluateCurve(_CurveMaster, c.r);
            float mg = EvaluateCurve(_CurveMaster, c.g);
            float mb = EvaluateCurve(_CurveMaster, c.b);
            c = float3(mr, mg, mb);

            // RGB
            c += kHalfPixel.xxx;
            float r = EvaluateCurve(_CurveRed, c.r);
            float g = EvaluateCurve(_CurveGreen, c.g);
            float b = EvaluateCurve(_CurveBlue, c.b);
            colorLinear = float3(r, g, b);
        }

        return half4(saturate(colorLinear), 1.0);
    }

    ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline"}
        LOD 100
        ZTest Always ZWrite Off Cull Off

        Pass
        {
            Name "ColorGradingLUTBuilder"

            HLSLPROGRAM
                #pragma vertex Vertex
                #pragma fragment Frag
            ENDHLSL
        }
    }
}