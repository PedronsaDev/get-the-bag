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
            Texture = 3
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

        // Cache smooth normals per mesh instance across the entire session to prevent redundant computation
        private static readonly Dictionary<Mesh, Vector3[]> s_SmoothNormalsCache = new Dictionary<Mesh, Vector3[]>();
        private static readonly HashSet<Mesh> s_RegisteredMeshes = new HashSet<Mesh>();

        #region Serialized Properties - Outline

        [SerializeField]
        private Mode outlineMode = Mode.OutlineVisible;

        [SerializeField, ColorUsage(true, true)]
        private Color outlineColor = Color.white;

        [SerializeField, Range(0f, 50f)]
        private float outlineWidth = 6f;

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

        [SerializeField]
        private CoordSpace fillSpace = CoordSpace.ObjectUV;

        [SerializeField]
        private FillOcclusion fillOcclusion = FillOcclusion.Always;

        #endregion

        #region Public Properties

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
        }

        private void OnDestroy()
        {
            RemoveOutlines();
            CleanupMaterials();
        }

        private void OnTransformChildrenChanged()
        {
            // Modular assembly or child parts added/removed dynamically
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

            if (_needsUpdate)
            {
                _needsUpdate = false;
                UpdateMaterialProperties();
            }
        }

        private void OnValidate()
        {
            stencilReference = Mathf.Clamp(stencilReference, 1, 255);
            _needsUpdate = true;
            _needsRendererRefresh = true;
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
        /// Scans all child MeshRenderers and SkinnedMeshRenderers, caching pre-allocated material arrays
        /// for zero-garbage swapping on hover enter/exit.
        /// </summary>
        public void RefreshRenderers()
        {
            EnsureMaterials();

            // Revert existing outlines before rebuilding cache
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

                var origMaterials = r.sharedMaterials;
                if (origMaterials == null || origMaterials.Length == 0) continue;

                // Build filtered original list (stripping any accidental previous outline materials)
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

                // Add mask
                if (_maskMaterial) outlinedList.Add(_maskMaterial);

                // Add fill (outline exterior)
                if (_fillMaterial && outlineMode != Mode.SilhouetteOnly) outlinedList.Add(_fillMaterial);

                // Add silhouette (interior pattern / surface fill)
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

            // 1. Synchronize Mask
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

            // 2. Synchronize Outline Fill
            if (_fillMaterial)
            {
                _fillMaterial.SetColor("_OutlineColor", outlineColor);
                _fillMaterial.SetFloat("_OutlineWidth", (outlineMode == Mode.SilhouetteOnly) ? 0f : outlineWidth);
                _fillMaterial.SetFloat("_WidthMode", (float)widthMode);
                _fillMaterial.SetFloat("_StencilRef", stencilReference);

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

            // 3. Synchronize Surface Fill / Silhouette
            if (_silhouetteMaterial)
            {
                _silhouetteMaterial.SetColor("_FillColor", fillColor);
                _silhouetteMaterial.SetColor("_FillSecondaryColor", fillSecondaryColor);
                _silhouetteMaterial.SetFloat("_PatternType", (float)fillPattern);
                if (fillTexture) _silhouetteMaterial.SetTexture("_PatternTex", fillTexture);
                _silhouetteMaterial.SetVector("_PatternTiling", fillTiling);
                _silhouetteMaterial.SetFloat("_PatternAngle", fillAngle);
                _silhouetteMaterial.SetFloat("_ScrollSpeed", fillScrollSpeed);
                _silhouetteMaterial.SetFloat("_CoordSpace", (float)fillSpace);
                _silhouetteMaterial.SetFloat("_StencilRef", stencilReference);

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

        #region Smooth Normals Generation (Zero Asset Mutation)

        private void LoadSmoothNormals()
        {
            // Process static meshes
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

            // Process skinned meshes
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

            // Quantize position into integer grid to cluster coincident corner vertices accurately
            const float tolerance = 1000f; // 1mm precision
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
