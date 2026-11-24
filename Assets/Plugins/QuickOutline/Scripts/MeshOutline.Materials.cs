using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Plugins.Outline
{
    public partial class MeshOutline
    {
        private static class ShaderIds
        {
            public static readonly int AnimationPhase = Shader.PropertyToID("_AnimationPhase");
            public static readonly int CameraEnabled = Shader.PropertyToID("_CameraEnabled");
            public static readonly int CoordSpace = Shader.PropertyToID("_CoordSpace");
            public static readonly int Cull = Shader.PropertyToID("_Cull");
            public static readonly int DistanceFadeEnabled = Shader.PropertyToID("_DistanceFadeEnabled");
            public static readonly int FadeMaxDistance = Shader.PropertyToID("_FadeMaxDistance");
            public static readonly int FadeMinDistance = Shader.PropertyToID("_FadeMinDistance");
            public static readonly int FillColor = Shader.PropertyToID("_FillColor");
            public static readonly int FillSecondaryColor = Shader.PropertyToID("_FillSecondaryColor");
            public static readonly int FresnelPower = Shader.PropertyToID("_FresnelPower");
            public static readonly int JitterEnabled = Shader.PropertyToID("_JitterEnabled");
            public static readonly int JitterFramerate = Shader.PropertyToID("_JitterFramerate");
            public static readonly int JitterFrequency = Shader.PropertyToID("_JitterFrequency");
            public static readonly int JitterScale = Shader.PropertyToID("_JitterScale");
            public static readonly int JitterSpeed = Shader.PropertyToID("_JitterSpeed");
            public static readonly int OutlineColor = Shader.PropertyToID("_OutlineColor");
            public static readonly int OutlineWidth = Shader.PropertyToID("_OutlineWidth");
            public static readonly int PatternAngle = Shader.PropertyToID("_PatternAngle");
            public static readonly int PatternLineWidth = Shader.PropertyToID("_PatternLineWidth");
            public static readonly int PatternTex = Shader.PropertyToID("_PatternTex");
            public static readonly int PatternTiling = Shader.PropertyToID("_PatternTiling");
            public static readonly int PatternType = Shader.PropertyToID("_PatternType");
            public static readonly int PulseAmplitude = Shader.PropertyToID("_PulseAmplitude");
            public static readonly int PulseEnabled = Shader.PropertyToID("_PulseEnabled");
            public static readonly int PulseSpeed = Shader.PropertyToID("_PulseSpeed");
            public static readonly int ScrollSpeed = Shader.PropertyToID("_ScrollSpeed");
            public static readonly int StencilRef = Shader.PropertyToID("_StencilRef");
            public static readonly int WidthMode = Shader.PropertyToID("_WidthMode");
            public static readonly int ZTest = Shader.PropertyToID("_ZTest");
        }
        private static Material CreateMaterial(string shaderName, string resource)
        {
            var shader = Shader.Find(shaderName) ?? Resources.Load<Shader>(resource);
            if (!shader) throw new InvalidOperationException("Missing outline shader: " + shaderName);
            return new Material(shader) { name = shaderName + " (Instance)", hideFlags = HideFlags.DontSave };
        }

        private void EnsureMaterials()
        {
            if (!_maskMaterial) _maskMaterial = CreateMaterial("Custom/Outline/Mask", "Shaders/OutlineMask");
            if (outlineMode != Mode.SilhouetteOnly && !_fillMaterial) _fillMaterial = CreateMaterial("Custom/Outline/Fill", "Shaders/OutlineFill");
            if (HasSurface && !_silhouetteMaterial) _silhouetteMaterial = CreateMaterial("Custom/Outline/Silhouette", "Shaders/OutlineSilhouette");
            if (!_needsPassRefresh && _effectMaterials != null) return;
            var materials = new List<Material> { _maskMaterial };
            if (outlineMode != Mode.SilhouetteOnly) materials.Add(_fillMaterial);
            if (HasSurface) materials.Add(_silhouetteMaterial);
            var next = materials.ToArray();
            if (!SameMaterials(_effectMaterials, next)) { _effectMaterials = next; _needsPassApply = true; }
            _needsPassRefresh = false;
        }

        public void UpdateMaterialProperties()
        {
            if (!Application.IsPlaying(gameObject)) { _needsUpdate = true; return; }
            stencilReference = 1; // Normalize legacy serialized references before any draw.
            EnsureMaterials();
            _needsUpdate = false;
            ApplyAppearance();
            ApplyXRayToBindings(); // Mode and occlusion edits can change without a material swap.

            if (_silhouetteMaterial)
            {
                _silhouetteMaterial.SetFloat(ShaderIds.Cull, twoSidedSurface ? (float)CullMode.Off : (float)CullMode.Back);
                _silhouetteMaterial.SetFloat(ShaderIds.PatternLineWidth, patternLineWidth);
                _silhouetteMaterial.SetFloat(ShaderIds.AnimationPhase, animationPhase);
            }
            if (_fillMaterial) _fillMaterial.SetFloat(ShaderIds.AnimationPhase, animationPhase);

            // 1. Mask
            if (_maskMaterial)
            {
                _maskMaterial.SetFloat(ShaderIds.StencilRef, stencilReference);
                _maskMaterial.SetFloat(ShaderIds.ZTest, (float)CompareFunction.Always);
            }

            // 2. Fill (Exterior Outline)
            if (_fillMaterial)
            {
                _fillMaterial.SetFloat(ShaderIds.WidthMode, (float)widthMode);
                _fillMaterial.SetFloat(ShaderIds.StencilRef, stencilReference);

                // Pulse
                _fillMaterial.SetFloat(ShaderIds.PulseEnabled, pulseEnabled ? 1f : 0f);
                _fillMaterial.SetFloat(ShaderIds.PulseSpeed, pulseSpeed);
                _fillMaterial.SetFloat(ShaderIds.PulseAmplitude, pulseAmplitude);

                // Distance Fade
                _fillMaterial.SetFloat(ShaderIds.DistanceFadeEnabled, distanceFadeEnabled ? 1f : 0f);
                _fillMaterial.SetFloat(ShaderIds.FadeMinDistance, fadeMinDistance);
                _fillMaterial.SetFloat(ShaderIds.FadeMaxDistance, fadeMaxDistance);

                // Jitter Wobble
                _fillMaterial.SetFloat(ShaderIds.JitterEnabled, jitterEnabled ? 1f : 0f);
                _fillMaterial.SetFloat(ShaderIds.JitterSpeed, jitterSpeed);
                _fillMaterial.SetFloat(ShaderIds.JitterFrequency, jitterFrequency);
                _fillMaterial.SetFloat(ShaderIds.JitterScale, jitterScale);
                _fillMaterial.SetFloat(ShaderIds.JitterFramerate, jitterFramerate);

                switch (outlineMode)
                {
                    case Mode.OutlineAll:
                        _fillMaterial.SetFloat(ShaderIds.ZTest, (float)CompareFunction.Always);
                        break;
                    case Mode.OutlineVisible:
                        _fillMaterial.SetFloat(ShaderIds.ZTest, (float)CompareFunction.LessEqual);
                        break;
                    case Mode.OutlineHidden:
                        _fillMaterial.SetFloat(ShaderIds.ZTest, (float)CompareFunction.Greater);
                        break;
                    case Mode.OutlineAndSilhouette:
                        _fillMaterial.SetFloat(ShaderIds.ZTest, (float)CompareFunction.Always);
                        break;
                    case Mode.SilhouetteOnly:
                        _fillMaterial.SetFloat(ShaderIds.ZTest, (float)CompareFunction.Greater);
                        break;
                }
            }

            // 3. Surface Fill / Silhouette
            if (_silhouetteMaterial)
            {
                _silhouetteMaterial.SetColor(ShaderIds.FillColor, fillColor);
                _silhouetteMaterial.SetColor(ShaderIds.FillSecondaryColor, fillSecondaryColor);
                _silhouetteMaterial.SetFloat(ShaderIds.PatternType, (float)fillPattern);
                _silhouetteMaterial.SetTexture(ShaderIds.PatternTex, fillTexture ? fillTexture : Texture2D.whiteTexture);
                _silhouetteMaterial.SetVector(ShaderIds.PatternTiling, fillTiling);
                _silhouetteMaterial.SetFloat(ShaderIds.PatternAngle, fillAngle);
                _silhouetteMaterial.SetFloat(ShaderIds.ScrollSpeed, fillScrollSpeed);
                _silhouetteMaterial.SetFloat(ShaderIds.FresnelPower, fresnelPower);
                _silhouetteMaterial.SetFloat(ShaderIds.CoordSpace, (float)fillSpace);
                _silhouetteMaterial.SetFloat(ShaderIds.StencilRef, stencilReference);

                // Distance Fade
                _silhouetteMaterial.SetFloat(ShaderIds.DistanceFadeEnabled, distanceFadeEnabled ? 1f : 0f);
                _silhouetteMaterial.SetFloat(ShaderIds.FadeMinDistance, fadeMinDistance);
                _silhouetteMaterial.SetFloat(ShaderIds.FadeMaxDistance, fadeMaxDistance);

                switch (fillOcclusion)
                {
                    case FillOcclusion.Always:
                        _silhouetteMaterial.SetFloat(ShaderIds.ZTest, (float)CompareFunction.Always);
                        break;
                    case FillOcclusion.WhenOccludedOnly:
                        _silhouetteMaterial.SetFloat(ShaderIds.ZTest, (float)CompareFunction.Greater);
                        break;
                    case FillOcclusion.WhenVisibleOnly:
                        _silhouetteMaterial.SetFloat(ShaderIds.ZTest, (float)CompareFunction.LessEqual);
                        break;
                }
            }
        }

    }
}
