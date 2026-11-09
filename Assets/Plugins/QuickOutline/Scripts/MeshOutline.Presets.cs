using UnityEngine;

namespace Plugins.Outline
{
    public partial class MeshOutline
    {
        private void ResetAppearance()
        {
            _isPinging = _isFlashing = false;
            outlineMode = Mode.OutlineVisible;
            outlineColor = Color.white;
            outlineWidth = 5f;
            widthMode = WidthMode.ScreenSpacePixels;
            stencilReference = 1;
            enableSurfaceFill = false;
            fillColor = new Color(0.2f, 0.8f, 1f, 0.4f);
            fillSecondaryColor = Color.clear;
            fillPattern = PatternType.Stripes;
            fillTexture = null;
            fillTiling = new Vector2(10f, 10f);
            fillAngle = 45f;
            fillScrollSpeed = 1f;
            fresnelPower = 2.5f;
            fillSpace = CoordSpace.ObjectUV;
            fillOcclusion = FillOcclusion.Always;
            pulseEnabled = distanceFadeEnabled = jitterEnabled = false;
            pulseSpeed = 2.5f;
            pulseAmplitude = 0.35f;
            fadeMinDistance = 15f;
            fadeMaxDistance = 45f;
            jitterSpeed = 2f;
            jitterFrequency = 3f;
            jitterScale = 0.5f;
            jitterFramerate = 5f;
            twoSidedSurface = unscaledEffects = false;
            patternLineWidth = 0.12f;
            animationPhase = 0f;
            _needsPassRefresh = _needsUpdate = true;
        }

        public void ApplyPresetLootPickup()
        {
            ResetAppearance();
            outlineMode = Mode.OutlineVisible;
            outlineColor = new Color(0.2f, 0.85f, 1f, 1f);
            outlineWidth = 5f;
            widthMode = WidthMode.ScreenSpacePixels;
            enableSurfaceFill = false;
            pulseEnabled = false;
            jitterEnabled = false;
            RefreshRenderers();
            UpdateMaterialProperties();
        }

        public void ApplyPresetTacticalXRay()
        {
            ResetAppearance();
            outlineMode = Mode.OutlineAndSilhouette;
            outlineColor = new Color(1f, 0.4f, 0.1f, 1f);
            outlineWidth = 6f;
            widthMode = WidthMode.ScreenSpacePixels;
            enableSurfaceFill = true;
            fillColor = new Color(1f, 0.35f, 0.05f, 0.35f);
            fillSecondaryColor = new Color(0f, 0f, 0f, 0f);
            fillPattern = PatternType.Stripes;
            fillTiling = new Vector2(16f, 16f);
            fillAngle = 45f;
            fillScrollSpeed = 2f;
            fillSpace = CoordSpace.ObjectUV;
            fillOcclusion = FillOcclusion.WhenOccludedOnly;
            RefreshRenderers();
            UpdateMaterialProperties();
        }

        public void ApplyPresetHologram()
        {
            ResetAppearance();
            outlineMode = Mode.OutlineAll;
            outlineColor = new Color(0f, 1f, 0.8f, 1f);
            outlineWidth = 4f;
            widthMode = WidthMode.ScreenSpacePixels;
            enableSurfaceFill = true;
            fillColor = new Color(0f, 0.9f, 0.9f, 0.3f);
            fillSecondaryColor = new Color(0f, 0.1f, 0.2f, 0.1f);
            fillPattern = PatternType.Grid;
            fillTiling = new Vector2(25f, 25f);
            fillAngle = 0f;
            fillScrollSpeed = 1.5f;
            fillSpace = CoordSpace.ScreenSpace;
            fillOcclusion = FillOcclusion.Always;
            RefreshRenderers();
            UpdateMaterialProperties();
        }

        public void ApplyPresetSolidSilhouette()
        {
            ResetAppearance();
            outlineMode = Mode.SilhouetteOnly;
            outlineWidth = 0f;
            enableSurfaceFill = true;
            fillColor = new Color(1f, 0.9f, 0.2f, 0.6f);
            fillPattern = PatternType.Solid;
            fillOcclusion = FillOcclusion.Always;
            RefreshRenderers();
            UpdateMaterialProperties();
        }

        public void ApplyPresetSpiderVerse()
        {
            ResetAppearance();
            outlineMode = Mode.OutlineVisible;
            outlineColor = new Color(0.95f, 0.1f, 0.25f, 1f);
            outlineWidth = 7f;
            jitterEnabled = true;
            jitterSpeed = 8f;
            jitterFrequency = 5f;
            jitterScale = 0.45f;
            jitterFramerate = 12f;
            enableSurfaceFill = false;
            RefreshRenderers();
            UpdateMaterialProperties();
        }

        public void ApplyPresetHoloShield()
        {
            ResetAppearance();
            outlineMode = Mode.OutlineAndSilhouette;
            outlineColor = new Color(0.1f, 0.7f, 1f, 1f);
            outlineWidth = 4f;
            enableSurfaceFill = true;
            fillColor = new Color(0.2f, 0.85f, 1f, 0.8f);
            fillSecondaryColor = new Color(0f, 0f, 0f, 0f);
            fillPattern = PatternType.FresnelRim;
            fresnelPower = 2.2f;
            fillOcclusion = FillOcclusion.Always;
            RefreshRenderers();
            UpdateMaterialProperties();
        }

    }
}
