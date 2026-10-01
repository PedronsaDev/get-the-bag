//
//  MeshOutline.cs
//  Advanced Outline & Surface Fill Toolkit for Unity 6.6 / URP
//  Zero-GC, Scale-Invariant, Multi-Mesh & Compound Hierarchy Support
//

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Plugins.Outline
{
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public class MeshOutline : MonoBehaviour
    {
        public enum Mode
        {
            OutlineAll,
            OutlineVisible,
            OutlineHidden,
            OutlineAndSilhouette,
            SilhouetteOnly
        }

        public enum WidthMode
        {
            ScreenSpacePixels = 0,
            WorldSpaceMeters = 1
        }

        public enum PatternType
        {
            Solid = 0,
            Stripes = 1,
            Grid = 2,
            Texture = 3,
            FresnelRim = 4
        }

        public enum CoordSpace
        {
            ObjectUV = 0,
            ScreenSpace = 1,
            WorldSpace = 2
        }

        public enum FillOcclusion
        {
            Always,
            WhenOccludedOnly,
            WhenVisibleOnly
        }

        private static readonly Dictionary<Mesh, Vector3[]> s_SmoothNormalsCache = new Dictionary<Mesh, Vector3[]>();
        private static readonly HashSet<Mesh> s_RegisteredMeshes = new HashSet<Mesh>();

        #region Serialized Properties - Outline

        [SerializeField]
        private Mode outlineMode = Mode.OutlineVisible;

        [SerializeField, ColorUsage(true, true)]
        private Color outlineColor = Color.white;

        [SerializeField, Range(0f, 50f)]
        private float outlineWidth = 15f;

        [SerializeField]
        private WidthMode widthMode = WidthMode.ScreenSpacePixels;

        [SerializeField, Range(1, 255)]
        private int stencilReference = 32;

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
            set { outlineMode = value; _needsUpdate = true; }
        }

        public Color OutlineColor
        {
            get => outlineColor;
            set { outlineColor = value; _needsUpdate = true; }
        }

        public float OutlineWidth
        {
            get => outlineWidth;
            set { outlineWidth = value; _needsUpdate = true; }
        }

        public WidthMode OutlineWidthMode
        {
            get => widthMode;
            set { widthMode = value; _needsUpdate = true; }
        }

        public int StencilReference
        {
            get => stencilReference;
            set { stencilReference = Mathf.Clamp(value, 1, 255); _needsUpdate = true; }
        }

        public bool EnableSurfaceFill
        {
            get => enableSurfaceFill;
            set { enableSurfaceFill = value; _needsRendererRefresh = true; }
        }

        public Color FillColor
        {
            get => fillColor;
            set { fillColor = value; _needsUpdate = true; }
        }

        public Color FillSecondaryColor
        {
            get => fillSecondaryColor;
            set { fillSecondaryColor = value; _needsUpdate = true; }
        }

        public PatternType FillPattern
        {
            get => fillPattern;
            set { fillPattern = value; _needsUpdate = true; }
        }

        public Texture2D FillTexture
        {
            get => fillTexture;
            set { fillTexture = value; _needsUpdate = true; }
        }

        public Vector2 FillTiling
        {
            get => fillTiling;
            set { fillTiling = value; _needsUpdate = true; }
        }

        public float FillAngle
        {
            get => fillAngle;
            set { fillAngle = value; _needsUpdate = true; }
        }

        public float FillScrollSpeed
        {
            get => fillScrollSpeed;
            set { fillScrollSpeed = value; _needsUpdate = true; }
        }

        public float FresnelPower
        {
            get => fresnelPower;
            set { fresnelPower = value; _needsUpdate = true; }
        }

        public CoordSpace FillSpace
        {
            get => fillSpace;
            set { fillSpace = value; _needsUpdate = true; }
        }

        public FillOcclusion FillOcclusionMode
        {
            get => fillOcclusion;
            set { fillOcclusion = value; _needsUpdate = true; }
        }

        public bool PulseEnabled
        {
            get => pulseEnabled;
            set { pulseEnabled = value; _needsUpdate = true; }
        }

        public float PulseSpeed
        {
            get => pulseSpeed;
            set { pulseSpeed = value; _needsUpdate = true; }
        }

        public float PulseAmplitude
        {
            get => pulseAmplitude;
            set { pulseAmplitude = value; _needsUpdate = true; }
        }

        public bool DistanceFadeEnabled
        {
            get => distanceFadeEnabled;
            set { distanceFadeEnabled = value; _needsUpdate = true; }
        }

        public float FadeMinDistance
        {
            get => fadeMinDistance;
            set { fadeMinDistance = value; _needsUpdate = true; }
        }

        public float FadeMaxDistance
        {
            get => fadeMaxDistance;
            set { fadeMaxDistance = value; _needsUpdate = true; }
        }

        public bool JitterEnabled
        {
            get => jitterEnabled;
            set { jitterEnabled = value; _needsUpdate = true; }
        }

        public float JitterSpeed
        {
            get => jitterSpeed;
            set { jitterSpeed = value; _needsUpdate = true; }
        }

        public float JitterFrequency
        {
            get => jitterFrequency;
            set { jitterFrequency = value; _needsUpdate = true; }
        }

        public float JitterScale
        {
            get => jitterScale;
            set { jitterScale = value; _needsUpdate = true; }
        }

        public float JitterFramerate
        {
            get => jitterFramerate;
            set { jitterFramerate = value; _needsUpdate = true; }
        }

        public LayerMask IncludeLayers
        {
            get => includeLayers;
            set { includeLayers = value; _needsRendererRefresh = true; }
        }

        public List<string> ExcludedKeywords => excludedKeywords;
        public List<Renderer> IgnoredRenderers => ignoredRenderers;

        #endregion

        private class RendererData
        {
            public Renderer renderer;
            public Material[] originalMaterials;
            public Material[] outlinedMaterials;
            public bool isSkinned;
        }

        private readonly List<RendererData> _cachedRenderers = new List<RendererData>();
        private Material _maskMaterial;
        private Material _fillMaterial;
        private Material _silhouetteMaterial;

        private bool _isOutlined;
        private bool _needsUpdate;
        private bool _needsRendererRefresh;

        // Dynamic Runtime Ping & Flash state
        private bool _isPinging;
        private float _pingDuration;
        private float _pingElapsed;
        private Color _pingBaseColor;
        private Color _pingTargetColor;
        private float _pingBaseWidth;

        private bool _isFlashing;
        private float _flashDuration;
        private float _flashElapsed;
        private Color _flashBaseColor;
        private Color _flashOverrideColor;

        #region Unity Lifecycle

        private void Awake()
        {
            EnsureMaterials();
            RefreshRenderers();
            LoadSmoothNormals();
            _needsUpdate = true;
        }

        private void OnEnable()
        {
            EnsureMaterials();
            if (_cachedRenderers.Count == 0)
            {
                RefreshRenderers();
            }
            ApplyOutlines();
            _needsUpdate = true;
        }

        private void OnDisable()
        {
            RemoveOutlines();
            _isPinging = false;
            _isFlashing = false;
        }

        private void OnDestroy()
        {
            RemoveOutlines();
            CleanupMaterials();
        }

        private void OnTransformChildrenChanged()
        {
            _needsRendererRefresh = true;
        }

        private void Update()
        {
            if (_needsRendererRefresh)
            {
                _needsRendererRefresh = false;
                RefreshRenderers();
                if (isActiveAndEnabled)
                {
                    ApplyOutlines();
                }
                _needsUpdate = true;
            }

            // Update Ping & Flash decay
            if (Application.isPlaying)
            {
                UpdatePingAndFlash(Time.deltaTime);
            }

            if (_needsUpdate)
            {
                _needsUpdate = false;
                UpdateMaterialProperties();
            }
        }

        private void OnValidate()
        {
            stencilReference = Mathf.Clamp(stencilReference, 1, 255);
            fadeMinDistance = Mathf.Max(0f, fadeMinDistance);
            fadeMaxDistance = Mathf.Max(fadeMinDistance + 0.1f, fadeMaxDistance);
            _needsUpdate = true;
            _needsRendererRefresh = true;
        }

        #endregion

        #region Dynamic Ping & Hit Flash

        /// <summary>
        /// Triggers an expanding radar ping pulse that smoothly decays back to base values.
        /// </summary>
        public void Ping(float duration = 1.5f, Color? pingColor = null)
        {
            if (!isActiveAndEnabled) enabled = true;

            _isPinging = true;
            _pingDuration = Mathf.Max(0.05f, duration);
            _pingElapsed = 0f;
            _pingBaseColor = outlineColor;
            _pingBaseWidth = outlineWidth;
            _pingTargetColor = pingColor ?? new Color(outlineColor.r * 2.5f, outlineColor.g * 2.5f, outlineColor.b * 2.5f, 1f);
        }

        /// <summary>
        /// Triggers an instant hit flash reaction (e.g. bullet impact flare) that decays rapidly.
        /// </summary>
        public void Flash(Color flashColor, float duration = 0.15f)
        {
            if (!isActiveAndEnabled) enabled = true;

            _isFlashing = true;
            _flashDuration = Mathf.Max(0.02f, duration);
            _flashElapsed = 0f;
            _flashBaseColor = outlineColor;
            _flashOverrideColor = flashColor;
        }

        private void UpdatePingAndFlash(float dt)
        {
            if (!_fillMaterial) return;

            if (_isFlashing)
            {
                _flashElapsed += dt;
                float t = Mathf.Clamp01(_flashElapsed / _flashDuration);
                Color currentColor = Color.Lerp(_flashOverrideColor, _flashBaseColor, t);
                _fillMaterial.SetColor("_OutlineColor", currentColor);

                if (t >= 1f)
                {
                    _isFlashing = false;
                    _fillMaterial.SetColor("_OutlineColor", outlineColor);
                }
            }
            else if (_isPinging)
            {
                _pingElapsed += dt;
                float t = Mathf.Clamp01(_pingElapsed / _pingDuration);
                float curve = 1f - Mathf.SmoothStep(0f, 1f, t);

                Color currentColor = Color.Lerp(_pingBaseColor, _pingTargetColor, curve);
                float currentWidth = Mathf.Lerp(_pingBaseWidth, _pingBaseWidth * 1.8f, curve);

                _fillMaterial.SetColor("_OutlineColor", currentColor);
                _fillMaterial.SetFloat("_OutlineWidth", currentWidth);

                if (t >= 1f)
                {
                    _isPinging = false;
                    _fillMaterial.SetColor("_OutlineColor", outlineColor);
                    _fillMaterial.SetFloat("_OutlineWidth", outlineWidth);
                }
            }
        }

        #endregion

        #region Material & Renderer Management

        private void EnsureMaterials()
        {
            if (!_maskMaterial)
            {
                Shader maskShader = Shader.Find("Custom/Outline/Mask") ?? Resources.Load<Shader>("Shaders/OutlineMask");
                if (maskShader)
                {
                    _maskMaterial = new Material(maskShader) { name = "OutlineMask (Instance)", hideFlags = HideFlags.DontSave };
                }
                else
                {
                    var baseMat = Resources.Load<Material>("Materials/OutlineMask");
                    if (baseMat) _maskMaterial = Instantiate(baseMat);
                }
            }

            if (!_fillMaterial)
            {
                Shader fillShader = Shader.Find("Custom/Outline/Fill") ?? Resources.Load<Shader>("Shaders/OutlineFill");
                if (fillShader)
                {
                    _fillMaterial = new Material(fillShader) { name = "OutlineFill (Instance)", hideFlags = HideFlags.DontSave };
                }
                else
                {
                    var baseMat = Resources.Load<Material>("Materials/OutlineFill");
                    if (baseMat) _fillMaterial = Instantiate(baseMat);
                }
            }

            if (!_silhouetteMaterial)
            {
                Shader silShader = Shader.Find("Custom/Outline/Silhouette") ?? Resources.Load<Shader>("Shaders/OutlineSilhouette");
                if (silShader)
                {
                    _silhouetteMaterial = new Material(silShader) { name = "OutlineSilhouette (Instance)", hideFlags = HideFlags.DontSave };
                }
            }
        }

        private void CleanupMaterials()
        {
            if (_maskMaterial)
            {
                if (Application.isPlaying) Destroy(_maskMaterial);
                else DestroyImmediate(_maskMaterial);
                _maskMaterial = null;
            }

            if (_fillMaterial)
            {
                if (Application.isPlaying) Destroy(_fillMaterial);
                else DestroyImmediate(_fillMaterial);
                _fillMaterial = null;
            }

            if (_silhouetteMaterial)
            {
                if (Application.isPlaying) Destroy(_silhouetteMaterial);
                else DestroyImmediate(_silhouetteMaterial);
                _silhouetteMaterial = null;
            }
        }

        /// <summary>
        /// Scans all child renderers with layer and keyword filtering, caching pre-allocated material arrays
        /// for zero-garbage swapping on hover enter/exit.
        /// </summary>
        public void RefreshRenderers()
        {
            EnsureMaterials();

            if (_isOutlined)
            {
                RemoveOutlines();
            }

            _cachedRenderers.Clear();

            var childRenderers = GetComponentsInChildren<Renderer>(true);
            bool shouldIncludeSilhouette = enableSurfaceFill || outlineMode == Mode.OutlineAndSilhouette || outlineMode == Mode.SilhouetteOnly;

            for (int i = 0; i < childRenderers.Length; i++)
            {
                var r = childRenderers[i];
                if (!r || !(r is MeshRenderer || r is SkinnedMeshRenderer)) continue;

                // 1. Layer filter check
                if (((1 << r.gameObject.layer) & includeLayers.value) == 0) continue;

                // 2. Explicit ignore list
                if (ignoredRenderers != null && ignoredRenderers.Contains(r)) continue;

                // 3. Excluded keywords check (e.g. "Cloth", "Shadow", "Decal")
                if (excludedKeywords != null && excludedKeywords.Count > 0)
                {
                    string rName = r.gameObject.name;
                    bool skip = false;
                    for (int k = 0; k < excludedKeywords.Count; k++)
                    {
                        var kw = excludedKeywords[k];
                        if (!string.IsNullOrEmpty(kw) && rName.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            skip = true;
                            break;
                        }
                    }
                    if (skip) continue;
                }

                var origMaterials = r.sharedMaterials;
                if (origMaterials == null || origMaterials.Length == 0) continue;

                var cleanedList = new List<Material>();
                for (int m = 0; m < origMaterials.Length; m++)
                {
                    var mat = origMaterials[m];
                    if (mat && mat != _maskMaterial && mat != _fillMaterial && mat != _silhouetteMaterial)
                    {
                        cleanedList.Add(mat);
                    }
                }

                if (cleanedList.Count == 0) continue;

                var origArray = cleanedList.ToArray();
                var outlinedList = new List<Material>(origArray);

                if (_maskMaterial) outlinedList.Add(_maskMaterial);
                if (_fillMaterial && outlineMode != Mode.SilhouetteOnly) outlinedList.Add(_fillMaterial);
                if (_silhouetteMaterial && shouldIncludeSilhouette) outlinedList.Add(_silhouetteMaterial);

                _cachedRenderers.Add(new RendererData
                {
                    renderer = r,
                    originalMaterials = origArray,
                    outlinedMaterials = outlinedList.ToArray(),
                    isSkinned = (r is SkinnedMeshRenderer)
                });
            }

            LoadSmoothNormals();

            if (isActiveAndEnabled)
            {
                ApplyOutlines();
            }
        }

        private void ApplyOutlines()
        {
            if (_isOutlined) return;

            for (int i = 0; i < _cachedRenderers.Count; i++)
            {
                var data = _cachedRenderers[i];
                if (data.renderer)
                {
                    data.renderer.sharedMaterials = data.outlinedMaterials;
                }
            }

            _isOutlined = true;
        }

        private void RemoveOutlines()
        {
            if (!_isOutlined) return;

            for (int i = 0; i < _cachedRenderers.Count; i++)
            {
                var data = _cachedRenderers[i];
                if (data.renderer && data.originalMaterials != null)
                {
                    data.renderer.sharedMaterials = data.originalMaterials;
                }
            }

            _isOutlined = false;
        }

        #endregion

        #region Material Properties Synchronization

        public void UpdateMaterialProperties()
        {
            EnsureMaterials();

            // 1. Mask
            if (_maskMaterial)
            {
                _maskMaterial.SetFloat("_StencilRef", stencilReference);
                switch (outlineMode)
                {
                    case Mode.OutlineAll:
                    case Mode.OutlineVisible:
                    case Mode.OutlineHidden:
                        _maskMaterial.SetFloat("_ZTest", (float)CompareFunction.Always);
                        break;
                    case Mode.OutlineAndSilhouette:
                    case Mode.SilhouetteOnly:
                        _maskMaterial.SetFloat("_ZTest", (float)CompareFunction.LessEqual);
                        break;
                }
            }

            // 2. Fill (Exterior Outline)
            if (_fillMaterial)
            {
                if (!_isFlashing && !_isPinging)
                {
                    _fillMaterial.SetColor("_OutlineColor", outlineColor);
                    _fillMaterial.SetFloat("_OutlineWidth", (outlineMode == Mode.SilhouetteOnly) ? 0f : outlineWidth);
                }

                _fillMaterial.SetFloat("_WidthMode", (float)widthMode);
                _fillMaterial.SetFloat("_StencilRef", stencilReference);

                // Pulse
                _fillMaterial.SetFloat("_PulseEnabled", pulseEnabled ? 1f : 0f);
                _fillMaterial.SetFloat("_PulseSpeed", pulseSpeed);
                _fillMaterial.SetFloat("_PulseAmplitude", pulseAmplitude);

                // Distance Fade
                _fillMaterial.SetFloat("_DistanceFadeEnabled", distanceFadeEnabled ? 1f : 0f);
                _fillMaterial.SetFloat("_FadeMinDistance", fadeMinDistance);
                _fillMaterial.SetFloat("_FadeMaxDistance", fadeMaxDistance);

                // Jitter Wobble
                _fillMaterial.SetFloat("_JitterEnabled", jitterEnabled ? 1f : 0f);
                _fillMaterial.SetFloat("_JitterSpeed", jitterSpeed);
                _fillMaterial.SetFloat("_JitterFrequency", jitterFrequency);
                _fillMaterial.SetFloat("_JitterScale", jitterScale);
                _fillMaterial.SetFloat("_JitterFramerate", jitterFramerate);

                switch (outlineMode)
                {
                    case Mode.OutlineAll:
                        _fillMaterial.SetFloat("_ZTest", (float)CompareFunction.Always);
                        break;
                    case Mode.OutlineVisible:
                        _fillMaterial.SetFloat("_ZTest", (float)CompareFunction.LessEqual);
                        break;
                    case Mode.OutlineHidden:
                        _fillMaterial.SetFloat("_ZTest", (float)CompareFunction.Greater);
                        break;
                    case Mode.OutlineAndSilhouette:
                        _fillMaterial.SetFloat("_ZTest", (float)CompareFunction.Always);
                        break;
                    case Mode.SilhouetteOnly:
                        _fillMaterial.SetFloat("_ZTest", (float)CompareFunction.Greater);
                        break;
                }
            }

            // 3. Surface Fill / Silhouette
            if (_silhouetteMaterial)
            {
                _silhouetteMaterial.SetColor("_FillColor", fillColor);
                _silhouetteMaterial.SetColor("_FillSecondaryColor", fillSecondaryColor);
                _silhouetteMaterial.SetFloat("_PatternType", (float)fillPattern);
                if (fillTexture) _silhouetteMaterial.SetTexture("_PatternTex", fillTexture);
                _silhouetteMaterial.SetVector("_PatternTiling", fillTiling);
                _silhouetteMaterial.SetFloat("_PatternAngle", fillAngle);
                _silhouetteMaterial.SetFloat("_ScrollSpeed", fillScrollSpeed);
                _silhouetteMaterial.SetFloat("_FresnelPower", fresnelPower);
                _silhouetteMaterial.SetFloat("_CoordSpace", (float)fillSpace);
                _silhouetteMaterial.SetFloat("_StencilRef", stencilReference);

                // Distance Fade
                _silhouetteMaterial.SetFloat("_DistanceFadeEnabled", distanceFadeEnabled ? 1f : 0f);
                _silhouetteMaterial.SetFloat("_FadeMinDistance", fadeMinDistance);
                _silhouetteMaterial.SetFloat("_FadeMaxDistance", fadeMaxDistance);

                switch (fillOcclusion)
                {
                    case FillOcclusion.Always:
                        _silhouetteMaterial.SetFloat("_ZTest", (float)CompareFunction.Always);
                        break;
                    case FillOcclusion.WhenOccludedOnly:
                        _silhouetteMaterial.SetFloat("_ZTest", (float)CompareFunction.Greater);
                        break;
                    case FillOcclusion.WhenVisibleOnly:
                        _silhouetteMaterial.SetFloat("_ZTest", (float)CompareFunction.LessEqual);
                        break;
                }
            }
        }

        #endregion

        #region Smooth Normals Generation

        private void LoadSmoothNormals()
        {
            foreach (var meshFilter in GetComponentsInChildren<MeshFilter>(true))
            {
                if (!meshFilter) continue;
                var mesh = meshFilter.sharedMesh;
                if (!mesh || !s_RegisteredMeshes.Add(mesh)) continue;

                if (!s_SmoothNormalsCache.TryGetValue(mesh, out var smoothNormals))
                {
                    smoothNormals = ComputeSmoothNormals(mesh);
                    s_SmoothNormalsCache[mesh] = smoothNormals;
                }

                mesh.SetUVs(3, smoothNormals);
            }

            foreach (var smr in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (!smr) continue;
                var mesh = smr.sharedMesh;
                if (!mesh || !s_RegisteredMeshes.Add(mesh)) continue;

                if (!s_SmoothNormalsCache.TryGetValue(mesh, out var smoothNormals))
                {
                    smoothNormals = ComputeSmoothNormals(mesh);
                    s_SmoothNormalsCache[mesh] = smoothNormals;
                }

                mesh.SetUVs(3, smoothNormals);
            }
        }

        private static Vector3[] ComputeSmoothNormals(Mesh mesh)
        {
            var vertices = mesh.vertices;
            var normals = mesh.normals;
            int vertexCount = vertices.Length;
            var smoothNormals = new Vector3[vertexCount];

            if (normals == null || normals.Length != vertexCount)
            {
                mesh.RecalculateNormals();
                normals = mesh.normals;
            }

            const float tolerance = 1000f;
            var positionGroups = new Dictionary<Vector3Int, List<int>>(vertexCount);

            for (int i = 0; i < vertexCount; i++)
            {
                var v = vertices[i];
                var key = new Vector3Int(
                    Mathf.RoundToInt(v.x * tolerance),
                    Mathf.RoundToInt(v.y * tolerance),
                    Mathf.RoundToInt(v.z * tolerance)
                );

                if (!positionGroups.TryGetValue(key, out var list))
                {
                    list = new List<int>(4);
                    positionGroups[key] = list;
                }
                list.Add(i);
            }

            foreach (var pair in positionGroups)
            {
                var list = pair.Value;
                if (list.Count == 1)
                {
                    smoothNormals[list[0]] = normals[list[0]];
                    continue;
                }

                Vector3 avg = Vector3.zero;
                for (int j = 0; j < list.Count; j++)
                {
                    avg += normals[list[j]];
                }
                avg.Normalize();

                for (int j = 0; j < list.Count; j++)
                {
                    smoothNormals[list[j]] = avg;
                }
            }

            return smoothNormals;
        }

        #endregion

        #region Preset Shortcuts

        public void ApplyPresetLootPickup()
        {
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

        #endregion

#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticCaches()
        {
            s_RegisteredMeshes.Clear();
            s_SmoothNormalsCache.Clear();
        }
#endif
    }
}
