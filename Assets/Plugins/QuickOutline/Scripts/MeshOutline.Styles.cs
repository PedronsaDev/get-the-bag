using UnityEngine;

namespace Plugins.Outline
{
    public partial class MeshOutline
    {
        public void ApplyAssignedStyle() { if (style) ApplyStyle(style); }

        public void ApplyStyle(MeshOutlineStyle source)
        {
            if (!source) return;
            outlineMode = source.outlineMode;
            outlineColor = source.outlineColor;
            outlineWidth = source.outlineWidth;
            widthMode = source.widthMode;
            enableSurfaceFill = source.enableSurfaceFill;
            fillColor = source.fillColor;
            fillSecondaryColor = source.fillSecondaryColor;
            fillPattern = source.fillPattern;
            fillTexture = source.fillTexture;
            fillTiling = source.fillTiling;
            fillAngle = source.fillAngle;
            fillScrollSpeed = source.fillScrollSpeed;
            fresnelPower = source.fresnelPower;
            fillSpace = source.fillSpace;
            fillOcclusion = source.fillOcclusion;
            pulseEnabled = source.pulseEnabled;
            pulseSpeed = source.pulseSpeed;
            pulseAmplitude = source.pulseAmplitude;
            distanceFadeEnabled = source.distanceFadeEnabled;
            fadeMinDistance = source.fadeMinDistance;
            fadeMaxDistance = source.fadeMaxDistance;
            jitterEnabled = source.jitterEnabled;
            jitterSpeed = source.jitterSpeed;
            jitterFrequency = source.jitterFrequency;
            jitterScale = source.jitterScale;
            jitterFramerate = source.jitterFramerate;
            unscaledEffects = source.unscaledEffects;
            twoSidedSurface = source.twoSidedSurface;
            patternLineWidth = source.patternLineWidth;
            animationPhase = source.animationPhase;
            OnValidate();
            if (Application.IsPlaying(gameObject) && isActiveAndEnabled) UpdateMaterialProperties();
        }

        public void CaptureStyle(MeshOutlineStyle destination)
        {
            if (!destination) return;
            destination.outlineMode = outlineMode;
            destination.outlineColor = outlineColor;
            destination.outlineWidth = outlineWidth;
            destination.widthMode = widthMode;
            destination.enableSurfaceFill = enableSurfaceFill;
            destination.fillColor = fillColor;
            destination.fillSecondaryColor = fillSecondaryColor;
            destination.fillPattern = fillPattern;
            destination.fillTexture = fillTexture;
            destination.fillTiling = fillTiling;
            destination.fillAngle = fillAngle;
            destination.fillScrollSpeed = fillScrollSpeed;
            destination.fresnelPower = fresnelPower;
            destination.fillSpace = fillSpace;
            destination.fillOcclusion = fillOcclusion;
            destination.pulseEnabled = pulseEnabled;
            destination.pulseSpeed = pulseSpeed;
            destination.pulseAmplitude = pulseAmplitude;
            destination.distanceFadeEnabled = distanceFadeEnabled;
            destination.fadeMinDistance = fadeMinDistance;
            destination.fadeMaxDistance = fadeMaxDistance;
            destination.jitterEnabled = jitterEnabled;
            destination.jitterSpeed = jitterSpeed;
            destination.jitterFrequency = jitterFrequency;
            destination.jitterScale = jitterScale;
            destination.jitterFramerate = jitterFramerate;
            destination.unscaledEffects = unscaledEffects;
            destination.twoSidedSurface = twoSidedSurface;
            destination.patternLineWidth = patternLineWidth;
            destination.animationPhase = animationPhase;
        }
    }
}
