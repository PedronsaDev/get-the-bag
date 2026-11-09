using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Project.Logging;

namespace Plugins.Outline
{
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public partial class MeshOutline : MonoBehaviour
    {
        public enum Mode { OutlineAll, OutlineVisible, OutlineHidden, OutlineAndSilhouette, SilhouetteOnly }
        // Value 1 retains the existing serialized centimeter behavior.
        public enum WidthMode { ScreenSpacePixels = 0, WorldSpaceCentimeters = 1, WorldSpaceMeters = 2 }
        public enum PatternType { Solid, Stripes, Grid, Texture, FresnelRim }
        public enum CoordSpace { ObjectUV, ScreenSpace, WorldSpace }
        public enum FillOcclusion { Always, WhenOccludedOnly, WhenVisibleOnly }

        #region Serialized Properties - Outline

        [SerializeField]
        private Mode outlineMode = Mode.OutlineVisible;

        [SerializeField, ColorUsage(true, true)]
        private Color outlineColor = Color.white;

        [SerializeField, Range(0f, 50f)]
        private float outlineWidth = 5f;

        [SerializeField]
        private WidthMode widthMode = WidthMode.ScreenSpacePixels;

        [SerializeField, HideInInspector]
        private int stencilReference = 1;

        #endregion

        #region Serialized Properties - Surface Fill

        [SerializeField]
        private bool enableSurfaceFill = false;

        [SerializeField, ColorUsage(true, true)]
        private Color fillColor = new Color(0.2f, 0.8f, 1f, 0.4f);

        [SerializeField, ColorUsage(true, true)]
        private Color fillSecondaryColor = new Color(0f, 0f, 0f, 0f);

        [SerializeField]
        private PatternType fillPattern = PatternType.Stripes;

        [SerializeField]
        private Texture2D fillTexture;

        [SerializeField]
        private Vector2 fillTiling = new Vector2(10f, 10f);

        [SerializeField, Range(0f, 360f)]
        private float fillAngle = 45f;

        [SerializeField]
        private float fillScrollSpeed = 1f;

        [SerializeField, Range(0.1f, 10f)]
        private float fresnelPower = 2.5f;

        [SerializeField]
        private CoordSpace fillSpace = CoordSpace.ObjectUV;

        [SerializeField]
        private FillOcclusion fillOcclusion = FillOcclusion.Always;

        #endregion

        #region Serialized Properties - Pulse & Distance Fading

        [SerializeField]
        private bool pulseEnabled = false;

        [SerializeField]
        private float pulseSpeed = 2.5f;

        [SerializeField, Range(0f, 1f)]
        private float pulseAmplitude = 0.35f;

        [SerializeField]
        private bool distanceFadeEnabled = false;

        [SerializeField]
        private float fadeMinDistance = 15f;

        [SerializeField]
        private float fadeMaxDistance = 45f;

        #endregion

        #region Serialized Properties - Stylized Jitter / Wobble

        [SerializeField]
        private bool jitterEnabled = false;

        [SerializeField]
        private float jitterSpeed = 2.0f;

        [SerializeField]
        private float jitterFrequency = 3.0f;

        [SerializeField, Range(0f, 1f)]
        private float jitterScale = 0.5f;

        [SerializeField]
        private float jitterFramerate = 5.0f;

        #endregion

        #region Serialized Properties - Filtering

        [SerializeField]
        private LayerMask includeLayers = ~0;

        [SerializeField]
        private List<string> excludedKeywords = new List<string>();

        [SerializeField]
        private List<Renderer> ignoredRenderers = new List<Renderer>();

        #endregion

        #region Public Accessors

        public Mode OutlineMode
        {
            get => outlineMode;
            set { if (outlineMode == value) return; outlineMode = value; _needsPassRefresh = _needsUpdate = true; }
        }

        public Color OutlineColor
        {
            get => outlineColor;
            set { if (outlineColor == value) return; outlineColor = value; _needsUpdate = true; }
        }

        public float OutlineWidth
        {
            get => outlineWidth;
            set { if (outlineWidth == value) return; outlineWidth = Mathf.Max(0f, value); _needsUpdate = true; }
        }

        public WidthMode OutlineWidthMode
        {
            get => widthMode;
            set { if (widthMode == value) return; widthMode = value; _needsUpdate = true; }
        }

        public int StencilReference
        {
            get => stencilReference;
            set { stencilReference = 1; _needsUpdate = true; }
        }

        public bool EnableSurfaceFill
        {
            get => enableSurfaceFill;
            set { if (enableSurfaceFill == value) return; enableSurfaceFill = value; _needsPassRefresh = _needsUpdate = true; }
        }

        public Color FillColor
        {
            get => fillColor;
            set { if (fillColor == value) return; fillColor = value; _needsUpdate = true; }
        }

        public Color FillSecondaryColor
        {
            get => fillSecondaryColor;
            set { if (fillSecondaryColor == value) return; fillSecondaryColor = value; _needsUpdate = true; }
        }

        public PatternType FillPattern
        {
            get => fillPattern;
            set { if (fillPattern == value) return; fillPattern = value; _needsUpdate = true; }
        }

        public Texture2D FillTexture
        {
            get => fillTexture;
            set { if (fillTexture == value) return; fillTexture = value; _needsUpdate = true; }
        }

        public Vector2 FillTiling
        {
            get => fillTiling;
            set { if (fillTiling == value) return; fillTiling = value; _needsUpdate = true; }
        }

        public float FillAngle
        {
            get => fillAngle;
            set { if (fillAngle == value) return; fillAngle = value; _needsUpdate = true; }
        }

        public float FillScrollSpeed
        {
            get => fillScrollSpeed;
            set { if (fillScrollSpeed == value) return; fillScrollSpeed = value; _needsUpdate = true; }
        }

        public float FresnelPower
        {
            get => fresnelPower;
            set { if (fresnelPower == value) return; fresnelPower = value; _needsUpdate = true; }
        }

        public CoordSpace FillSpace
        {
            get => fillSpace;
            set { if (fillSpace == value) return; fillSpace = value; _needsUpdate = true; }
        }

        public FillOcclusion FillOcclusionMode
        {
            get => fillOcclusion;
            set { if (fillOcclusion == value) return; fillOcclusion = value; _needsUpdate = true; }
        }

        public bool PulseEnabled
        {
            get => pulseEnabled;
            set { if (pulseEnabled == value) return; pulseEnabled = value; _needsUpdate = true; }
        }

        public float PulseSpeed
        {
            get => pulseSpeed;
            set { if (pulseSpeed == value) return; pulseSpeed = value; _needsUpdate = true; }
        }

        public float PulseAmplitude
        {
            get => pulseAmplitude;
            set { if (pulseAmplitude == value) return; pulseAmplitude = Mathf.Clamp01(value); _needsUpdate = true; }
        }

        public bool DistanceFadeEnabled
        {
            get => distanceFadeEnabled;
            set { if (distanceFadeEnabled == value) return; distanceFadeEnabled = value; _needsUpdate = true; }
        }

        public float FadeMinDistance
        {
            get => fadeMinDistance;
            set
            {
                fadeMinDistance = Mathf.Max(0f, value);
                fadeMaxDistance = Mathf.Max(fadeMinDistance + 0.1f, fadeMaxDistance);
                _needsUpdate = true;
            }
        }

        public float FadeMaxDistance
        {
            get => fadeMaxDistance;
            set { fadeMaxDistance = Mathf.Max(fadeMinDistance + 0.1f, value); _needsUpdate = true; }
        }

        public bool JitterEnabled
        {
            get => jitterEnabled;
            set { if (jitterEnabled == value) return; jitterEnabled = value; _needsUpdate = true; }
        }

        public float JitterSpeed
        {
            get => jitterSpeed;
            set { if (jitterSpeed == value) return; jitterSpeed = value; _needsUpdate = true; }
        }

        public float JitterFrequency
        {
            get => jitterFrequency;
            set { if (jitterFrequency == value) return; jitterFrequency = value; _needsUpdate = true; }
        }

        public float JitterScale
        {
            get => jitterScale;
            set { if (jitterScale == value) return; jitterScale = Mathf.Clamp01(value); _needsUpdate = true; }
        }

        public float JitterFramerate
        {
            get => jitterFramerate;
            set { if (jitterFramerate == value) return; jitterFramerate = value; _needsUpdate = true; }
        }

        public LayerMask IncludeLayers
        {
            get => includeLayers;
            set { includeLayers = value; _needsRendererRefresh = true; }
        }

        public List<string> ExcludedKeywords => excludedKeywords;
        public List<Renderer> IgnoredRenderers => ignoredRenderers;

        #endregion

        [SerializeField] private MeshOutlineStyle style;
        [SerializeField] private List<OutlinePreparedMesh> preparedMeshes = new List<OutlinePreparedMesh>();
        [SerializeField, Min(0f)] private float screenSpaceBoundsPadding = 0.25f;
        [SerializeField] private bool showInSceneView = true;
        [SerializeField] private bool showInReflections;
        [SerializeField] private bool unscaledEffects;
        [SerializeField] private bool twoSidedSurface;
        [SerializeField, Range(0.01f, 0.49f)] private float patternLineWidth = 0.12f;
        [SerializeField] private float animationPhase;

        private readonly List<Renderer> _renderers = new List<Renderer>();
        private readonly List<OutlineRendererBinding> _bindings = new List<OutlineRendererBinding>();
        private Material _maskMaterial, _fillMaterial, _silhouetteMaterial;
        private Material[] _effectMaterials;
        private bool _needsUpdate = true, _needsRendererRefresh = true, _needsPassRefresh = true, _validatePending, _needsPassApply;
        private bool _persistentVisible, _temporaryActivation;
        private bool _isPinging, _isFlashing;
        private float _pingDuration, _pingElapsed, _flashDuration, _flashElapsed;
        private Color _pingTargetColor, _flashOverrideColor;
        private int _filterHash;
        public string LastPreparationError { get; private set; }
        public int BoundRendererCount => _bindings.Count;
        public IReadOnlyList<OutlinePreparedMesh> PreparedMeshes => preparedMeshes;
        private bool HasSurface => enableSurfaceFill || outlineMode == Mode.OutlineAndSilhouette || outlineMode == Mode.SilhouetteOnly;

        private void OnEnable()
        {
            if (!Application.IsPlaying(gameObject)) return;
            _persistentVisible = !_temporaryActivation;
            _temporaryActivation = false;
            EnsureMaterials();
            UpdateMaterialProperties();
            if (_needsRendererRefresh || _bindings.Count == 0) RefreshRenderers();
            else ApplyBindings();
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            foreach (var binding in _bindings) binding.Remove();
            _isPinging = _isFlashing = _temporaryActivation = false;
        }

        private void OnDestroy()
        {
            DisposeBindings();
            OutlineMeshPreparation.Destroy(_maskMaterial);
            OutlineMeshPreparation.Destroy(_fillMaterial);
            OutlineMeshPreparation.Destroy(_silhouetteMaterial);
        }

        private void OnTransformChildrenChanged() => _needsRendererRefresh = true;

        private void OnValidate()
        {
            stencilReference = 1;
            outlineWidth = Mathf.Max(0f, outlineWidth);
            fadeMinDistance = Mathf.Max(0f, fadeMinDistance);
            fadeMaxDistance = Mathf.Max(fadeMinDistance + 0.1f, fadeMaxDistance);
            fresnelPower = Mathf.Max(0.1f, fresnelPower);
            pulseAmplitude = Mathf.Clamp01(pulseAmplitude);
            jitterScale = Mathf.Clamp01(jitterScale);
            patternLineWidth = Mathf.Clamp(patternLineWidth, 0.01f, 0.49f);
            screenSpaceBoundsPadding = Mathf.Max(0f, screenSpaceBoundsPadding);
            _needsUpdate = _needsPassRefresh = _validatePending = true;
        }

        private void Update()
        {
            if (!Application.IsPlaying(gameObject)) return;
            if (_validatePending)
            {
                _validatePending = false;
                if (_filterHash != FilterHash()) _needsRendererRefresh = true;
            }
            if (_needsRendererRefresh) RefreshRenderers();
            if (_needsPassRefresh)
            {
                EnsureMaterials();
            }
            if (_needsPassApply) ApplyBindings();
            foreach (var binding in _bindings)
                if (binding.HasExternalChanges()) ApplyBinding(binding);
            if (_needsUpdate) UpdateMaterialProperties();
            if (_isPinging || _isFlashing)
            {
                AdvanceEffects(unscaledEffects ? Time.unscaledDeltaTime : Time.deltaTime);
                ApplyAppearance();
                if (!_isPinging && !_isFlashing && !_persistentVisible) enabled = false;
            }
        }

        public void SetHighlighted(bool visible)
        {
            _persistentVisible = visible;
            if (visible) enabled = true;
            else if (!_isPinging && !_isFlashing) enabled = false;
        }

        public void Ping(float duration = 1.5f, Color? pingColor = null)
        {
            EnableForEffect();
            _isPinging = true;
            _pingElapsed = 0f;
            _pingDuration = Mathf.Max(0.05f, duration);
            _pingTargetColor = pingColor ?? new Color(outlineColor.r * 2.5f, outlineColor.g * 2.5f, outlineColor.b * 2.5f, outlineColor.a);
            ApplyAppearance();
        }

        public void Flash(Color flashColor, float duration = 0.15f)
        {
            EnableForEffect();
            _isFlashing = true;
            _flashElapsed = 0f;
            _flashDuration = Mathf.Max(0.02f, duration);
            _flashOverrideColor = flashColor;
            ApplyAppearance();
        }

        private void EnableForEffect()
        {
            if (enabled) return;
            _temporaryActivation = true;
            enabled = true;
        }

        public void StopEffects()
        {
            _isPinging = _isFlashing = false;
            ApplyAppearance();
            if (!_persistentVisible) enabled = false;
        }

        private void AdvanceEffects(float dt)
        {
            if (_isPinging) { _pingElapsed += dt; _isPinging = _pingElapsed < _pingDuration; }
            if (_isFlashing) { _flashElapsed += dt; _isFlashing = _flashElapsed < _flashDuration; }
        }

        private void ApplyAppearance()
        {
            if (!_fillMaterial) return;
            float ping = _isPinging ? 1f - Mathf.SmoothStep(0f, 1f, _pingElapsed / _pingDuration) : 0f;
            var color = Color.Lerp(outlineColor, _pingTargetColor, ping);
            if (_isFlashing) color = Color.Lerp(_flashOverrideColor, color, _flashElapsed / _flashDuration);
            _fillMaterial.SetColor(ShaderIds.OutlineColor, color);
            _fillMaterial.SetFloat(ShaderIds.OutlineWidth, outlineMode == Mode.SilhouetteOnly ? 0f : Mathf.Max(0f, outlineWidth) * (1f + ping * 0.8f));
        }

        public void RequestRendererRefresh() => _needsRendererRefresh = true;

        public void SetRendererIgnored(Renderer renderer, bool ignored)
        {
            if (ignored && !ignoredRenderers.Contains(renderer)) ignoredRenderers.Add(renderer);
            else if (!ignored) ignoredRenderers.Remove(renderer);
            RequestRendererRefresh();
        }

        public void SetKeywordExcluded(string keyword, bool excluded)
        {
            if (string.IsNullOrEmpty(keyword)) return;
            if (excluded && !excludedKeywords.Contains(keyword)) excludedKeywords.Add(keyword);
            else if (!excluded) excludedKeywords.Remove(keyword);
            RequestRendererRefresh();
        }

        public bool IncludesRenderer(Renderer renderer)
        {
            if (!renderer || !(renderer is MeshRenderer || renderer is SkinnedMeshRenderer)) return false;
            if (renderer.GetComponentInParent<MeshOutline>(true) != this) return false;
            if (((1 << renderer.gameObject.layer) & includeLayers.value) == 0 || ignoredRenderers.Contains(renderer)) return false;
            foreach (var keyword in excludedKeywords)
                if (!string.IsNullOrEmpty(keyword) && renderer.name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0) return false;
            return true;
        }

        public Mesh PreparationSource(Mesh source)
        {
            foreach (var prepared in preparedMeshes)
                if (prepared && prepared.Source == source && prepared.ReadableCopy) return prepared.ReadableCopy;
            return source;
        }

        public Mesh SourceMesh(Renderer renderer)
        {
            foreach (var binding in _bindings)
                if (binding.Renderer == renderer) return binding.Source;
            return renderer is SkinnedMeshRenderer skin ? skin.sharedMesh : renderer.GetComponent<MeshFilter>()?.sharedMesh;
        }

        public void SetPreparedMeshes(IEnumerable<OutlinePreparedMesh> meshes)
        {
            preparedMeshes.Clear();
            preparedMeshes.AddRange(meshes);
            RequestRendererRefresh();
        }

        public void RefreshRenderers()
        {
            _needsRendererRefresh = false;
            _filterHash = FilterHash();
            if (!Application.IsPlaying(gameObject)) return; // Authoring never swaps scene/prefab meshes.
            DisposeBindings();
            GetComponentsInChildren(true, _renderers);
            foreach (var renderer in _renderers)
                if (IncludesRenderer(renderer)) _bindings.Add(new OutlineRendererBinding(renderer));
            if (isActiveAndEnabled) { EnsureMaterials(); ApplyBindings(); }
        }

        private void ApplyBindings()
        {
            LastPreparationError = null;
            foreach (var binding in _bindings) ApplyBinding(binding);
            _needsPassApply = false;
        }

        private void ApplyBinding(OutlineRendererBinding binding)
        {
            var renderer = binding.Renderer;
            if (!renderer) return;
            binding.Remove();
            var mesh = renderer is SkinnedMeshRenderer skin ? skin.sharedMesh : renderer.GetComponent<MeshFilter>()?.sharedMesh;
            if (!mesh) return;
            try
            {
                if (renderer.isPartOfStaticBatch) throw new InvalidOperationException("Disable static batching for outlined renderers.");
                binding.Apply(_effectMaterials, PreparationSource(mesh));
                binding.SetXRay(outlineMode != Mode.OutlineVisible || (HasSurface && fillOcclusion != FillOcclusion.WhenVisibleOnly));
            }
            catch (InvalidOperationException error)
            {
                LastPreparationError = renderer.name + ": " + error.Message;
                AppLog.LogWarning(LogChannel.General, LastPreparationError, this);
            }
        }

        private void DisposeBindings()
        {
            foreach (var binding in _bindings) binding.Dispose();
            _bindings.Clear();
        }

        private int FilterHash()
        {
            int hash = includeLayers.value;
            foreach (var keyword in excludedKeywords) hash = unchecked(hash * 31 + (keyword?.GetHashCode() ?? 0));
            foreach (var renderer in ignoredRenderers) hash = unchecked(hash * 31 + (renderer ? renderer.GetHashCode() : 0));
            return hash;
        }

        private static bool SameMaterials(Material[] a, Material[] b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null || a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }

        private void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            bool draw = camera.cameraType == CameraType.Game || (showInSceneView && camera.cameraType == CameraType.SceneView) || (showInReflections && camera.cameraType == CameraType.Reflection);
            if (_effectMaterials == null) return;
            foreach (var material in _effectMaterials) material.SetFloat(ShaderIds.CameraEnabled, draw ? 1f : 0f);
            if (!draw) return;
            float width = Mathf.Max(0f, outlineWidth) * (1f + Mathf.Clamp01(pulseAmplitude)) * (1f + Mathf.Clamp01(jitterScale)) * (_isPinging ? 1.8f : 1f);
            foreach (var binding in _bindings)
            {
                if (!binding.Renderer) continue;
                binding.SetXRay(outlineMode != Mode.OutlineVisible || (HasSurface && fillOcclusion != FillOcclusion.WhenVisibleOnly));
                float padding = widthMode == WidthMode.ScreenSpacePixels ? screenSpaceBoundsPadding : width * (widthMode == WidthMode.WorldSpaceCentimeters ? 0.01f : 1f);
                if (widthMode == WidthMode.ScreenSpacePixels)
                {
                    var bounds = binding.BaseWorldBounds;
                    float depth = Mathf.Abs(Vector3.Dot(bounds.center - camera.transform.position, camera.transform.forward)) + bounds.extents.magnitude;
                    float unitsPerPixel = 2f * (camera.orthographic ? 1f : depth) / Mathf.Max(0.001f, Mathf.Abs(camera.projectionMatrix.m11) * camera.scaledPixelHeight);
                    padding = Mathf.Max(padding, unitsPerPixel * width);
                }
                binding.ExpandBounds(padding);
            }
        }
    }
}
