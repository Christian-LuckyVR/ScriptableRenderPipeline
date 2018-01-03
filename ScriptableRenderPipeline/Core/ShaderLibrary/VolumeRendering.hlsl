#ifndef UNITY_VOLUME_RENDERING_INCLUDED
#define UNITY_VOLUME_RENDERING_INCLUDED

#include "Filtering.hlsl"

// Reminder:
// Optical_Depth(x, y) = Integral{x, y}{Extinction(t) dt}
// Transmittance(x, y) = Exp(-Optical_Depth(x, y))
// Transmittance(x, z) = Transmittance(x, y) * Transmittance(y, z)
// Integral{a, b}{Transmittance(0, t) * L_s(t) dt} = Transmittance(0, a) * Integral{a, b}{Transmittance(0, t - a) * L_s(t) dt}.

float OpticalDepthHomogeneousMedium(float extinction, float intervalLength)
{
    return extinction * intervalLength;
}

float3 OpticalDepthHomogeneousMedium(float3 extinction, float intervalLength)
{
    return extinction * intervalLength;
}

float Transmittance(float opticalDepth)
{
    return exp(-opticalDepth);
}

float3 Transmittance(float3 opticalDepth)
{
    return exp(-opticalDepth);
}

float TransmittanceHomogeneousMedium(float extinction, float intervalLength)
{
    return Transmittance(OpticalDepthHomogeneousMedium(extinction, intervalLength));
}

float3 TransmittanceHomogeneousMedium(float3 extinction, float intervalLength)
{
    return Transmittance(OpticalDepthHomogeneousMedium(extinction, intervalLength));
}

// Integral{a, b}{Transmittance(0, t - a) dt}.
float TransmittanceIntegralHomogeneousMedium(float extinction, float intervalLength)
{
    return rcp(extinction) - rcp(extinction) * exp(-extinction * intervalLength);
}

// Integral{a, b}{Transmittance(0, t - a) dt}.
float3 TransmittanceIntegralHomogeneousMedium(float3 extinction, float intervalLength)
{
    return rcp(extinction) - rcp(extinction) * exp(-extinction * intervalLength);
}

float IsotropicPhaseFunction()
{
    return INV_FOUR_PI;
}

float HenyeyGreensteinPhasePartConstant(float asymmetry)
{
    float g = asymmetry;

    return INV_FOUR_PI * (1 - g * g);
}

float HenyeyGreensteinPhasePartVarying(float asymmetry, float LdotD)
{
    float g = asymmetry;

    return pow(abs(1 + g * g - 2 * g * LdotD), -1.5);
}

float HenyeyGreensteinPhaseFunction(float asymmetry, float LdotD)
{
    return HenyeyGreensteinPhasePartConstant(asymmetry) *
           HenyeyGreensteinPhasePartVarying(asymmetry, LdotD);
}

// Samples the interval of homogeneous participating medium using the closed-form tracking approach
// (proportionally to the transmittance).
// Returns the offset from the start of the interval and the weight = (transmittance / pdf).
// Ref: Production Volume Rendering, 3.6.1.
void ImportanceSampleHomogeneousMedium(float rndVal, float extinction, float intervalLength,
                                      out float offset, out float weight)
{
    // pdf    = extinction * exp(-extinction * t) / (1 - exp(-intervalLength * extinction))
    // weight = exp(-extinction * t) / pdf
    // weight = (1 - exp(-extinction * intervalLength)) / extinction;

    float x = 1 - exp(-extinction * intervalLength);

    weight = x * rcp(extinction);
    offset = -log(1 - rndVal * x) * rcp(extinction);
}

// Implements equiangular light sampling.
// Returns the distance from the origin of the ray, the squared (radial) distance from the light,
// and the reciprocal of the PDF.
// Ref: Importance Sampling of Area Lights in Participating Medium.
void ImportanceSamplePunctualLight(float rndVal, float3 lightPosition,
                                   float3 rayOrigin, float3 rayDirection,
                                   float tMin, float tMax,
                                   out float dist, out float rSq, out float rcpPdf,
                                   float minDistSq = FLT_EPS)
{
    float3 originToLight       = lightPosition - rayOrigin;
    float  originToLightProj   = dot(originToLight, rayDirection);
    float  originToLightDistSq = dot(originToLight, originToLight);
    float  rayToLightDistSq    = max(originToLightDistSq - originToLightProj * originToLightProj, minDistSq);

    float a    = tMin - originToLightProj;
    float b    = tMax - originToLightProj;
    float dSq  = rayToLightDistSq;
    float dRcp = rsqrt(dSq);
    float d    = dSq * dRcp;

    // TODO: optimize me. :-(
    float theta0 = FastATan(a * dRcp);
    float theta1 = FastATan(b * dRcp);
    float gamma  = theta1 - theta0;
    float theta  = lerp(theta0, theta1, rndVal);
    float t      = d * tan(theta);

    dist   = originToLightProj + t;
    rSq    = dSq + t * t;
    rcpPdf = gamma * rSq * dRcp;
}

// Absorption coefficient from Disney: http://blog.selfshadow.com/publications/s2015-shading-course/burley/s2015_pbs_disney_bsdf_notes.pdf
float3 TransmittanceColorAtDistanceToAbsorption(float3 transmittanceColor, float atDistance)
{
    return -log(transmittanceColor + FLT_EPS) / max(atDistance, FLT_EPS);
}

#define VOLUMETRIC_LIGHTING_ENABLED

#ifdef PRESET_ULTRA
    // E.g. for 1080p: (1920/4)x(1080/4)x(256) = 33,177,600 voxels
    #define VBUFFER_TILE_SIZE   4
    #define VBUFFER_SLICE_COUNT 256
#else
    // E.g. for 1080p: (1920/8)x(1080/8)x(128) =  4,147,200 voxels
    #define VBUFFER_TILE_SIZE   8
    #define VBUFFER_SLICE_COUNT 128
#endif // PRESET_ULTRA

#include "Random.hlsl"

// Samples the linearly interpolated V-Buffer.
// If (clampToEdge == false), out-of-bounds loads return 0.
float4 SampleVBuffer(TEXTURE3D_ARGS(VBufferLighting, trilinearSampler), bool clampToEdge,
                     float2 positionNDC, float linearDepth,
                     float2 VBufferScale,
                     float4 VBufferDepthEncodingParams)
{
    int   k = VBUFFER_SLICE_COUNT;
    float z = linearDepth;
    float d = EncodeLogarithmicDepth(z, VBufferDepthEncodingParams);

    // Account for the visible area of the V-Buffer.
    float2 uv = positionNDC * VBufferScale;

    // Unity doesn't support samplers clamping to border, so we have to do it ourselves.
    // TODO: add the proper sampler support.
    bool isInBounds = Min3(uv.x, uv.y, d) > 0 && Max3(uv.x, uv.y, d) < 1;

    [branch] if (clampToEdge || isInBounds)
    {
        // The distance between slices is log-encoded.
        float s0 = floor(d * k - 0.5);
        float s1 = ceil(d * k - 0.5);
        float d0 = saturate(s0 * rcp(k) + (0.5 * rcp(k)));
        float d1 = saturate(s1 * rcp(k) + (0.5 * rcp(k)));
        float z0 = DecodeLogarithmicDepth(d0, VBufferDepthEncodingParams);
        float z1 = DecodeLogarithmicDepth(d1, VBufferDepthEncodingParams);

        // Compute the linear Z-interpolation weight.
        float a = saturate((z - z0) / (z1 - z0));
        float w = d0 + a * rcp(k);

        // Adjust the texture coordinate and take a single HW trlinear sample.
        return SAMPLE_TEXTURE3D_LOD(VBufferLighting, trilinearSampler, float3(uv, w), 0);
    }
    else
    {
        return 0;
    }
}

// Returns linearly interpolated {volumetric radiance, opacity}. The sampler clamps to edge.
float4 SampleInScatteredRadianceAndTransmittance(TEXTURE3D_ARGS(VBufferLighting, trilinearSampler),
                                                 float2 positionNDC, float linearDepth,
                                                 float4 VBufferResolutionAndScale, float4 VBufferDepthEncodingParams)
{
#ifdef RECONSTRUCTION_FILTER_TRILINEAR
    float4 L = SampleVBuffer(TEXTURE3D_PARAM(VBufferLighting, trilinearSampler), true,
                             positionNDC, linearDepth, VBufferScale, VBufferDepthEncodingParams);
#else
    // Perform biquadratic reconstruction in XY, linear in Z, using 4x trilinear taps.
    int   k = VBUFFER_SLICE_COUNT;
    float z = linearDepth;
    float d = EncodeLogarithmicDepth(z, VBufferDepthEncodingParams);

     // The distance between slices is log-encoded.
    float s0 = floor(d * k - 0.5);
    float s1 = ceil(d * k - 0.5);
    float d0 = saturate(s0 * rcp(k) + (0.5 * rcp(k)));
    float d1 = saturate(s1 * rcp(k) + (0.5 * rcp(k)));
    float z0 = DecodeLogarithmicDepth(d0, VBufferDepthEncodingParams);
    float z1 = DecodeLogarithmicDepth(d1, VBufferDepthEncodingParams);

    // Compute the linear Z-interpolation weight.
    float a = saturate((z - z0) / (z1 - z0));
    float w = d0 + a * rcp(k);

    // Account for the visible area of the V-Buffer.
    float2 xy = positionNDC * (VBufferResolutionAndScale.xy * VBufferResolutionAndScale.zw); // TODO: precompute
    float2 ic = floor(xy);
    float2 fc = frac(xy);

    float2 weights[2], offsets[2];
    BiquadraticFilter(1 - fc, weights, offsets); // Reflect the filter around 0.5

    float2 rcpRes = rcp(VBufferResolutionAndScale.xy); // TODO: precompute

    float4 L = (weights[0].x * weights[0].y) * SAMPLE_TEXTURE3D_LOD(VBufferLighting, trilinearSampler, float3((ic + float2(offsets[0].x, offsets[0].y)) * rcpRes, w), 0)  // Top left
             + (weights[1].x * weights[0].y) * SAMPLE_TEXTURE3D_LOD(VBufferLighting, trilinearSampler, float3((ic + float2(offsets[1].x, offsets[0].y)) * rcpRes, w), 0)  // Top right
             + (weights[0].x * weights[1].y) * SAMPLE_TEXTURE3D_LOD(VBufferLighting, trilinearSampler, float3((ic + float2(offsets[0].x, offsets[1].y)) * rcpRes, w), 0)  // Bottom left
             + (weights[1].x * weights[1].y) * SAMPLE_TEXTURE3D_LOD(VBufferLighting, trilinearSampler, float3((ic + float2(offsets[1].x, offsets[1].y)) * rcpRes, w), 0); // Bottom right
#endif

    // TODO: add some animated noise to the reconstructed radiance.
    return float4(L.rgb, 1 - Transmittance(L.a));
}

#endif // UNITY_VOLUME_RENDERING_INCLUDED
