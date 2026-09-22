//
//  Outline.cs
//  QuickOutline (Modernized for Unity 6 & Universal Render Pipeline)
//
//  Created by Chris Nolet on 3/30/18.
//  Copyright © 2018 Chris Nolet. All rights reserved.
//

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Plugins.Outline
{


[DisallowMultipleComponent]
[ExecuteAlways]
public class MeshOutline : MonoBehaviour {
  private static readonly HashSet<Mesh> s_RegisteredMeshes = new HashSet<Mesh>();
  private static readonly List<Vector3> s_Vertices = new List<Vector3>();
  private static readonly List<Vector3> s_Normals = new List<Vector3>();
  private static readonly List<Vector3> s_SmoothNormals = new List<Vector3>();
  private static readonly Dictionary<Vector3, Vector3> s_NormalSums = new Dictionary<Vector3, Vector3>();

  private static readonly int s_OutlineColorId = Shader.PropertyToID("_OutlineColor");
  private static readonly int s_OutlineWidthId = Shader.PropertyToID("_OutlineWidth");
  private static readonly int s_ZTestId = Shader.PropertyToID("_ZTest");

  public enum Mode {
    OutlineAll,
    OutlineVisible,
    OutlineHidden,
    OutlineAndSilhouette,
    SilhouetteOnly
  }

  public Mode OutlineMode {
    get => outlineMode;
    set {
      if (outlineMode == value) return;
      outlineMode = value;
      UpdateMaterialProperties();
    }
  }

  public Color OutlineColor {
    get => outlineColor;
    set {
      if (outlineColor == value) return;
      outlineColor = value;
      UpdateMaterialProperties();
    }
  }

  public float OutlineWidth {
    get => outlineWidth;
    set {
      if (Mathf.Approximately(outlineWidth, value)) return;
      outlineWidth = value;
      UpdateMaterialProperties();
    }
  }

  [Serializable]
  private class ListVector3 {
    public List<Vector3> data;
  }

  [SerializeField]
  private Mode outlineMode = Mode.OutlineAll;

  [SerializeField]
  private Color outlineColor = Color.white;

  [SerializeField, Range(0f, 50f)]
  private float outlineWidth = 10f;

  [Header("Optional")]
  [SerializeField, Tooltip("Precompute enabled: Per-vertex calculations are performed in the editor and serialized with the object. "
  + "Precompute disabled: Per-vertex calculations are performed at runtime in Awake().")]
  private bool precomputeOutline;

  [SerializeField, HideInInspector]
  private List<Mesh> bakeKeys = new List<Mesh>();

  [SerializeField, HideInInspector]
  private List<ListVector3> bakeValues = new List<ListVector3>();

  [SerializeField, HideInInspector]
  private MeshFilter[] serializedMeshFilters;
  [SerializeField, HideInInspector]
  private Mesh[] serializedOriginalMeshes;

  private struct RendererData {
    public Renderer renderer;
    public Material[] inactiveMaterials;
    public Material[] activeMaterials;
  }

  private RendererData[] renderersData;
  private Material outlineMaskMaterial;
  private Material outlineFillMaterial;
  private bool isInitialized;

  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
  private static void ResetStaticCache() {
    s_RegisteredMeshes.Clear();
  }

  void Awake() {
    Initialize();
  }

  void OnEnable() {
    if (!isInitialized) {
      Initialize();
    }
    ApplyActiveMaterials();
  }

  void OnDisable() {
    ApplyInactiveMaterials();
  }

  void OnDestroy() {
    ApplyInactiveMaterials();

    if (outlineMaskMaterial != null) DestroyImmediate(outlineMaskMaterial);
    if (outlineFillMaterial != null) DestroyImmediate(outlineFillMaterial);
  }

  void OnValidate() {
    if (!precomputeOutline && (bakeKeys.Count != 0 || bakeKeys.Count != bakeValues.Count)) {
      bakeKeys.Clear();
      bakeValues.Clear();
    }

    if (precomputeOutline && bakeKeys.Count == 0) {
      Bake();
    }

    UpdateMaterialProperties();
  }

  public void RefreshRenderers() {
    ApplyInactiveMaterials();
    isInitialized = false;
    Initialize();
    if (isActiveAndEnabled) {
      ApplyActiveMaterials();
    }
  }

  private void Initialize() {
    if (isInitialized) return;

    AutoHealMeshFilters();

    if (outlineMaskMaterial == null) {
      var maskPrefab = Resources.Load<Material>("Materials/OutlineMask");
      if (maskPrefab != null) {
        outlineMaskMaterial = new Material(maskPrefab) { name = "OutlineMask (Instance)", hideFlags = HideFlags.DontSave };
      }
    }

    if (outlineFillMaterial == null) {
      var fillPrefab = Resources.Load<Material>("Materials/OutlineFill");
      if (fillPrefab != null) {
        outlineFillMaterial = new Material(fillPrefab) { name = "OutlineFill (Instance)", hideFlags = HideFlags.DontSave };
      }
    }

    UpdateMaterialProperties();
    LoadSmoothNormals();

    var renderers = GetComponentsInChildren<Renderer>(true);
    var validDataList = new List<RendererData>(renderers.Length);

    for (int i = 0; i < renderers.Length; i++) {
      var r = renderers[i];
      if (r == null || r is ParticleSystemRenderer) continue;

      var currentMats = r.sharedMaterials;
      var cleanMatsList = new List<Material>(currentMats.Length);
      for (int m = 0; m < currentMats.Length; m++) {
        var mat = currentMats[m];
        if (mat == null) continue;
        if (mat == outlineMaskMaterial || mat == outlineFillMaterial) continue;
        if (mat.name.StartsWith("OutlineMask") || mat.name.StartsWith("OutlineFill")) continue;
        cleanMatsList.Add(mat);
      }
      var inactive = cleanMatsList.ToArray();

      var active = new Material[inactive.Length + 2];
      for (int m = 0; m < inactive.Length; m++) {
        active[m] = inactive[m];
      }
      active[inactive.Length] = outlineMaskMaterial;
      active[inactive.Length + 1] = outlineFillMaterial;

      validDataList.Add(new RendererData {
        renderer = r,
        inactiveMaterials = inactive,
        activeMaterials = active
      });
    }

    renderersData = validDataList.ToArray();
    isInitialized = true;
  }

  private void AutoHealMeshFilters() {
    var mfs = GetComponentsInChildren<MeshFilter>(true);
    if (serializedMeshFilters != null && serializedOriginalMeshes != null &&
        serializedMeshFilters.Length == serializedOriginalMeshes.Length) {
      for (int i = 0; i < serializedMeshFilters.Length; i++) {
        var mf = serializedMeshFilters[i];
        if (mf != null && mf.sharedMesh == null && serializedOriginalMeshes[i] != null) {
          mf.sharedMesh = serializedOriginalMeshes[i];
        }
      }
    }

    var validFilters = new List<MeshFilter>();
    var validMeshes = new List<Mesh>();
    for (int i = 0; i < mfs.Length; i++) {
      var mf = mfs[i];
      if (mf != null && mf.sharedMesh != null && !mf.sharedMesh.name.EndsWith("_OutlineInstance")) {
        validFilters.Add(mf);
        validMeshes.Add(mf.sharedMesh);
      }
    }
    serializedMeshFilters = validFilters.ToArray();
    serializedOriginalMeshes = validMeshes.ToArray();
  }

  private void ApplyActiveMaterials() {
    if (renderersData == null) return;
    for (int i = 0; i < renderersData.Length; i++) {
      var data = renderersData[i];
      if (data.renderer != null) {
        data.renderer.sharedMaterials = data.activeMaterials;
      }
    }
  }

  private void ApplyInactiveMaterials() {
    if (renderersData == null) return;
    for (int i = 0; i < renderersData.Length; i++) {
      var data = renderersData[i];
      if (data.renderer != null) {
        data.renderer.sharedMaterials = data.inactiveMaterials;
      }
    }
  }

  private void LoadSmoothNormals() {
    foreach (var meshFilter in GetComponentsInChildren<MeshFilter>(true)) {
      var mesh = meshFilter.sharedMesh;
      if (mesh == null) continue;

      if (!s_RegisteredMeshes.Add(mesh)) {
        continue;
      }

      int bakeIndex = bakeKeys.IndexOf(mesh);
      List<Vector3> smoothNormals;
      if (bakeIndex >= 0 && bakeIndex < bakeValues.Count && bakeValues[bakeIndex].data != null && bakeValues[bakeIndex].data.Count == mesh.vertexCount) {
        smoothNormals = bakeValues[bakeIndex].data;
      } else {
        smoothNormals = ComputeSmoothNormals(mesh);
      }

      mesh.SetUVs(3, smoothNormals);
    }
  }

  private static List<Vector3> ComputeSmoothNormals(Mesh mesh) {
    mesh.GetVertices(s_Vertices);
    mesh.GetNormals(s_Normals);

    int count = s_Vertices.Count;
    if (count == 0) return new List<Vector3>();

    if (s_Normals.Count != count) {
      mesh.RecalculateNormals();
      mesh.GetNormals(s_Normals);
    }

    s_NormalSums.Clear();
    for (int i = 0; i < count; i++) {
      Vector3 v = s_Vertices[i];
      Vector3 n = s_Normals[i];
      if (s_NormalSums.TryGetValue(v, out Vector3 sum)) {
        s_NormalSums[v] = sum + n;
      } else {
        s_NormalSums[v] = n;
      }
    }

    s_SmoothNormals.Clear();
    if (s_SmoothNormals.Capacity < count) {
      s_SmoothNormals.Capacity = count;
    }

    for (int i = 0; i < count; i++) {
      Vector3 v = s_Vertices[i];
      Vector3 sum = s_NormalSums[v];
      float sqrMag = sum.sqrMagnitude;
      s_SmoothNormals.Add(sqrMag > 1e-6f ? sum.normalized : s_Normals[i]);
    }

    return new List<Vector3>(s_SmoothNormals);
  }

  private void Bake() {
    var bakedMeshes = new HashSet<Mesh>();

    foreach (var meshFilter in GetComponentsInChildren<MeshFilter>(true)) {
      if (meshFilter.sharedMesh == null) continue;
      if (!bakedMeshes.Add(meshFilter.sharedMesh)) continue;

      var smoothNormals = ComputeSmoothNormals(meshFilter.sharedMesh);
      bakeKeys.Add(meshFilter.sharedMesh);
      bakeValues.Add(new ListVector3 { data = smoothNormals });
    }
  }

  private void UpdateMaterialProperties() {
    if (outlineFillMaterial == null || outlineMaskMaterial == null) return;

    outlineFillMaterial.SetColor(s_OutlineColorId, outlineColor);

    switch (outlineMode) {
      case Mode.OutlineAll:
        outlineMaskMaterial.SetFloat(s_ZTestId, (float)UnityEngine.Rendering.CompareFunction.Always);
        outlineFillMaterial.SetFloat(s_ZTestId, (float)UnityEngine.Rendering.CompareFunction.Always);
        outlineFillMaterial.SetFloat(s_OutlineWidthId, outlineWidth);
        break;

      case Mode.OutlineVisible:
        outlineMaskMaterial.SetFloat(s_ZTestId, (float)UnityEngine.Rendering.CompareFunction.Always);
        outlineFillMaterial.SetFloat(s_ZTestId, (float)UnityEngine.Rendering.CompareFunction.LessEqual);
        outlineFillMaterial.SetFloat(s_OutlineWidthId, outlineWidth);
        break;

      case Mode.OutlineHidden:
        outlineMaskMaterial.SetFloat(s_ZTestId, (float)UnityEngine.Rendering.CompareFunction.Always);
        outlineFillMaterial.SetFloat(s_ZTestId, (float)UnityEngine.Rendering.CompareFunction.Greater);
        outlineFillMaterial.SetFloat(s_OutlineWidthId, outlineWidth);
        break;

      case Mode.OutlineAndSilhouette:
        outlineMaskMaterial.SetFloat(s_ZTestId, (float)UnityEngine.Rendering.CompareFunction.LessEqual);
        outlineFillMaterial.SetFloat(s_ZTestId, (float)UnityEngine.Rendering.CompareFunction.Always);
        outlineFillMaterial.SetFloat(s_OutlineWidthId, outlineWidth);
        break;

      case Mode.SilhouetteOnly:
        outlineMaskMaterial.SetFloat(s_ZTestId, (float)UnityEngine.Rendering.CompareFunction.LessEqual);
        outlineFillMaterial.SetFloat(s_ZTestId, (float)UnityEngine.Rendering.CompareFunction.Greater);
        outlineFillMaterial.SetFloat(s_OutlineWidthId, 0f);
        break;
    }
  }
}
}