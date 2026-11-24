using System;
using System.Collections.Generic;
using UnityEngine;

namespace Plugins.Outline
{
    // One binding owns only its appended materials and its mesh replacement.
    internal sealed class OutlineRendererBinding : IDisposable
    {
        public readonly Renderer Renderer;
        public Mesh Source => _source ? _source : CurrentMesh;
        public Bounds BaseWorldBounds
        {
            get
            {
                var matrix = Renderer.localToWorldMatrix;
                var extents = _originalBounds.extents;
                var worldExtents = new Vector3(
                    Mathf.Abs(matrix.m00) * extents.x + Mathf.Abs(matrix.m01) * extents.y + Mathf.Abs(matrix.m02) * extents.z,
                    Mathf.Abs(matrix.m10) * extents.x + Mathf.Abs(matrix.m11) * extents.y + Mathf.Abs(matrix.m12) * extents.z,
                    Mathf.Abs(matrix.m20) * extents.x + Mathf.Abs(matrix.m21) * extents.y + Mathf.Abs(matrix.m22) * extents.z);
                return new Bounds(matrix.MultiplyPoint3x4(_originalBounds.center), worldExtents * 2f);
            }
        }
        private readonly MeshFilter _filter;
        private readonly SkinnedMeshRenderer _skin;
        private readonly List<Material> _scratch = new List<Material>();
        private Mesh _source;
        private Mesh _preparationSource;
        private Mesh _owned;
        private Material[] _original;
        private Material[] _outlined;
        private Material[] _effects;
        private Bounds _originalBounds;
        private Bounds _appliedBounds;
        private int _slots;
        private Vector3Int _scaleKey;
        private bool _applied;
        private bool _originalOcclusion;

        public OutlineRendererBinding(Renderer renderer)
        {
            Renderer = renderer;
            _filter = renderer.GetComponent<MeshFilter>();
            _skin = renderer as SkinnedMeshRenderer;
        }

        private Mesh CurrentMesh
        {
            get => _skin ? _skin.sharedMesh : _filter ? _filter.sharedMesh : null;
            set { if (_skin) _skin.sharedMesh = value; else if (_filter) _filter.sharedMesh = value; }
        }

        public void Apply(Material[] effects, Mesh preparationSource)
        {
            if (!Renderer) return;
            Remove();
            var source = CurrentMesh;
            if (!source) return;
            Renderer.GetSharedMaterials(_scratch);
            bool sameBase = _original != null && _original.Length == _scratch.Count;
            for (int i = 0; sameBase && i < _scratch.Count; i++) sameBase = _original[i] == _scratch[i];
            if (!sameBase) _original = _scratch.ToArray();
            bool sameEffects = _effects != null && _effects.Length == effects.Length;
            for (int i = 0; sameEffects && i < effects.Length; i++) sameEffects = _effects[i] == effects[i];
            int slots = Mathf.Max(source.subMeshCount, _original.Length);
            var scaleKey = CurrentScaleKey();
            if (_source != source || _preparationSource != preparationSource || _slots != slots || _scaleKey != scaleKey || !_owned)
            {
                ReleaseMesh();
                _source = source;
                _preparationSource = preparationSource;
                _slots = slots;
                _scaleKey = scaleKey;
                _owned = OutlineMeshCache.Acquire(preparationSource, slots, _skin, scaleKey);
            }
            _effects = effects;
            if (!sameBase || !sameEffects || _outlined == null || _outlined.Length != slots + effects.Length)
            {
                _outlined = new Material[slots + effects.Length];
                Array.Copy(_original, _outlined, _original.Length);
                Array.Copy(effects, 0, _outlined, slots, effects.Length);
            }
            _originalBounds = Renderer.localBounds;
            _appliedBounds = _originalBounds;
            _originalOcclusion = Renderer.allowOcclusionWhenDynamic;
            CurrentMesh = _owned;
            Renderer.sharedMaterials = _outlined;
            _applied = true;
        }

        public bool HasExternalChanges()
        {
            if (!Renderer || !_applied) return false;
            if (CurrentMesh != _owned) return true;
            Renderer.GetSharedMaterials(_scratch);
            if (_scratch.Count != _outlined.Length) return true;
            for (int i = 0; i < _scratch.Count; i++)
                if (_scratch[i] != _outlined[i]) return true;
            return false;
        }

        // Runs per frame per binding: one matrix read and three square roots, no allocation.
        public bool ScaleChanged()
        {
            if (!Renderer || !_applied || _skin) return false;
            return CurrentScaleKey() != _scaleKey;
        }

        // Skinned copies carry no smoothed directions, so scale never affects them.
        private Vector3Int CurrentScaleKey()
        {
            return _skin ? OutlineScaleKey.Uniform : OutlineScaleKey.From(Renderer.localToWorldMatrix);
        }

        public void ExpandBounds(float worldPadding)
        {
            if (!Renderer || !_applied) return;
            if (Renderer.localBounds != _appliedBounds) _originalBounds = Renderer.localBounds;
            var inverse = Renderer.transform.worldToLocalMatrix;
            var padding = new Vector3(
                new Vector3(inverse.m00, inverse.m01, inverse.m02).magnitude,
                new Vector3(inverse.m10, inverse.m11, inverse.m12).magnitude,
                new Vector3(inverse.m20, inverse.m21, inverse.m22).magnitude) * worldPadding;
            _appliedBounds = _originalBounds;
            _appliedBounds.Expand(padding * 2f);
            Renderer.localBounds = _appliedBounds;
        }

        public void SetXRay(bool xray)
        {
            if (Renderer && _applied) Renderer.allowOcclusionWhenDynamic = xray ? false : _originalOcclusion;
        }

        public void Remove()
        {
            if (!Renderer || !_applied) return;
            Renderer.GetSharedMaterials(_scratch);
            bool unchanged = _scratch.Count == _outlined.Length;
            for (int i = 0; unchanged && i < _scratch.Count; i++) unchanged = _scratch[i] == _outlined[i];
            if (unchanged) Renderer.sharedMaterials = _original;
            else
            {
                for (int i = _scratch.Count - 1; i >= 0; i--)
                    if (Array.IndexOf(_effects, _scratch[i]) >= 0) _scratch.RemoveAt(i);
                Renderer.SetSharedMaterials(_scratch);
            }
            if (CurrentMesh == _owned) CurrentMesh = _source;
            if (Renderer.localBounds == _appliedBounds) Renderer.localBounds = _originalBounds;
            Renderer.allowOcclusionWhenDynamic = _originalOcclusion;
            _applied = false;
        }

        private void ReleaseMesh()
        {
            if (_owned) OutlineMeshCache.Release(_preparationSource, _slots, _skin, _scaleKey);
            _owned = null;
        }

        public void Dispose() { Remove(); ReleaseMesh(); }
    }
}
