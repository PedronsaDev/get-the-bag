using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ITISKIRUHERE
{
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public class AdvancedOutline : MonoBehaviour
    {
        public enum Mode
        {
            OutlineAll,
            OutlineVisible,
            OutlineHidden,
            OutlineAndSilhouette,
            SilhouetteOnly
        }

        [Serializable]
        class ListVector3
        {
            public List<Vector3> data;
        }

        [Header("References")]
        [SerializeField] Shader _outlineMaskShader;
        [SerializeField] Shader _outlineFillShader;

        [Header("General Settings")]
        [SerializeField] Mode _outlineMode = Mode.OutlineVisible;
        [SerializeField] Color _outlineColor = Color.cyan;
        [SerializeField] [Range( 0f, 10f )] float _outlineWidth = 4f;

        [Header("Aesthetic Effects")]
        [SerializeField] bool _pulseWidth;
        [SerializeField] [Range( 0.1f, 10f )] float _pulseSpeed = 2f;
        [SerializeField] [Range( 0f, 10f )] float _pulseMinWidth = 1.5f;
        [SerializeField] [Range( 0f, 10f )] float _pulseMaxWidth = 5f;

        [Space]
        [SerializeField] bool _rainbowColor;
        [SerializeField] [Range( 0.1f, 5f )] float _rainbowSpeed = 0.5f;

        [Space]
        [SerializeField] bool _dashedPattern;
        [SerializeField] [Range( 0.001f, 0.1f )] float _dashScale = 0.03f;
        [SerializeField] [Range( -50f, 50f )] float _dashSpeed = 15f;

        [Header("Performance Settings")]
        [SerializeField] bool _precomputeOutline;

        [SerializeField] [HideInInspector] List<Mesh> _bakeKeys = new List<Mesh>();
        [SerializeField] [HideInInspector] List<ListVector3> _bakeValues = new List<ListVector3>();

        Renderer[] _renderers;
        MeshFilter[] _meshFilters;
        SkinnedMeshRenderer[] _skinnedMeshRenderers;
        Material _outlineMaskMaterial;
        Material _outlineFillMaterial;
        MaterialPropertyBlock _propertyBlock;

        bool _needsUpdate;
        
        int _outlineColorId;
        int _outlineWidthId;
        int _zTestId;
        int _dashScaleId;
        int _dashSpeedId;

        List<Material> _tempMaterials = new List<Material>();
        Dictionary<Mesh, List<Vector3>> _runtimeBakeCache = new Dictionary<Mesh, List<Vector3>>();
        Dictionary<MeshFilter, Mesh> _clonedMeshes = new Dictionary<MeshFilter, Mesh>();
        Dictionary<MeshFilter, Mesh> _originalMeshes = new Dictionary<MeshFilter, Mesh>();
        Dictionary<SkinnedMeshRenderer, Mesh> _clonedSkinnedMeshes = new Dictionary<SkinnedMeshRenderer, Mesh>();
        Dictionary<SkinnedMeshRenderer, Mesh> _originalSkinnedMeshes = new Dictionary<SkinnedMeshRenderer, Mesh>();

        public Mode OutlineMode
        {
            get => _outlineMode;
            set
            {
                _outlineMode = value;
                _needsUpdate = true;
            }
        }

        public Color OutlineColor
        {
            get => _outlineColor;
            set
            {
                _outlineColor = value;
                _needsUpdate = true;
            }
        }

        public float OutlineWidth
        {
            get => _outlineWidth;
            set
            {
                _outlineWidth = value;
                _needsUpdate = true;
            }
        }

        public bool PulseWidth { get => _pulseWidth; set => _pulseWidth = value; }
        public bool RainbowColor { get => _rainbowColor; set => _rainbowColor = value; }
        public bool DashedPattern { get => _dashedPattern; set => _dashedPattern = value; }

        public int RendererCount => _renderers != null ? _renderers.Length : 0;
        public int MeshCount => ( _meshFilters != null ? _meshFilters.Length : 0 ) + ( _skinnedMeshRenderers != null ? _skinnedMeshRenderers.Length : 0 );

        public Shader OutlineMaskShader { get => _outlineMaskShader; set { _outlineMaskShader = value; _needsUpdate = true; } }
        public Shader OutlineFillShader { get => _outlineFillShader; set { _outlineFillShader = value; _needsUpdate = true; } }

        void Awake()
        {
            _propertyBlock = new MaterialPropertyBlock();
            CachePropertyIDs();
            InitializeBakeCache();
            InitializeMaterials();
            RefreshRenderers();
        }

        void OnEnable()
        {
            if ( _propertyBlock == null )
            {
                _propertyBlock = new MaterialPropertyBlock();
            }
            CachePropertyIDs();
            InitializeBakeCache();
            InitializeMaterials();
            RefreshRenderers();
        }

        void OnDisable()
        {
            CleanAllOutlineMaterialsFromRenderers();
            ClearClonedMeshes();
        }

        void OnDestroy()
        {
            if ( _outlineMaskMaterial )
            {
                DestroyOutlineObject( _outlineMaskMaterial );
            }
            if ( _outlineFillMaterial )
            {
                DestroyOutlineObject( _outlineFillMaterial );
            }
            ClearClonedMeshes();
        }

        void OnTransformChildrenChanged()
        {
            RefreshRenderers();
        }

        void Update()
        {
            if ( _needsUpdate )
            {
                _needsUpdate = false;
                UpdateStaticMaterialProperties();
            }

            HandleDynamicEffects();
        }

        void OnValidate()
        {
            _needsUpdate = true;
            InitializeMaterials();

            if ( _pulseMinWidth > _pulseMaxWidth )
            {
                _pulseMinWidth = _pulseMaxWidth;
            }
            if ( _pulseMaxWidth < _pulseMinWidth )
            {
                _pulseMaxWidth = _pulseMinWidth;
            }
            if ( _dashScale < 0f )
            {
                _dashScale = 0f;
            }

            #if UNITY_EDITOR
            if ( !_precomputeOutline && _bakeKeys.Count != 0 || _bakeKeys.Count != _bakeValues.Count )
            {
                _bakeKeys.Clear();
                _bakeValues.Clear();
            }

            if ( _precomputeOutline && _bakeKeys.Count == 0 )
            {
                Bake();
            }
            #endif
        }

        public void RefreshRenderers()
        {
            CacheComponents();
            CleanAllOutlineMaterialsFromRenderers();
            LoadSmoothNormals();
            ApplyMaterialsToRenderers();
            _needsUpdate = true;
        }

        void CacheComponents()
        {
            _renderers = GetComponentsInChildren<Renderer>();
            _meshFilters = GetComponentsInChildren<MeshFilter>();
            _skinnedMeshRenderers = GetComponentsInChildren<SkinnedMeshRenderer>();
        }

        void CachePropertyIDs()
        {
            _outlineColorId = Shader.PropertyToID( "_OutlineColor" );
            _outlineWidthId = Shader.PropertyToID( "_OutlineWidth" );
            _zTestId = Shader.PropertyToID( "_ZTest" );
            _dashScaleId = Shader.PropertyToID( "_DashScale" );
            _dashSpeedId = Shader.PropertyToID( "_DashSpeed" );
        }

        void InitializeBakeCache()
        {
            _runtimeBakeCache.Clear();
            if ( _bakeKeys != null && _bakeValues != null )
            {
                int count = Mathf.Min( _bakeKeys.Count, _bakeValues.Count );
                for ( int i = 0; i < count; i++ )
                {
                    if ( _bakeKeys[i] != null && _bakeValues[i] != null )
                    {
                        _runtimeBakeCache[_bakeKeys[i]] = _bakeValues[i].data;
                    }
                }
            }
        }

        void InitializeMaterials()
        {
            if ( _outlineMaskMaterial && _outlineFillMaterial ) return;

            if ( !_outlineMaskShader )
            {
                _outlineMaskShader = Shader.Find( "Custom/Advanced Outline Mask" );
            }
            if ( !_outlineFillShader )
            {
                _outlineFillShader = Shader.Find( "Custom/Advanced Outline Fill" );
            }

            if ( !_outlineMaskMaterial && _outlineMaskShader )
            {
                _outlineMaskMaterial = new Material( _outlineMaskShader );
                _outlineMaskMaterial.name = "AdvancedOutlineMask (Instance)";
            }

            if ( !_outlineFillMaterial && _outlineFillShader )
            {
                _outlineFillMaterial = new Material( _outlineFillShader );
                _outlineFillMaterial.name = "AdvancedOutlineFill (Instance)";
            }
        }

        void ApplyMaterialsToRenderers()
        {
            if ( _renderers == null ) return;

            foreach ( Renderer renderer in _renderers )
            {
                if ( !renderer ) continue;

                _tempMaterials.Clear();
                renderer.GetSharedMaterials( _tempMaterials );

                _tempMaterials.RemoveAll( mat => mat != null && 
                    ( mat.shader == _outlineMaskShader || mat.shader == _outlineFillShader ) );

                if ( _outlineMaskMaterial )
                {
                    _tempMaterials.Add( _outlineMaskMaterial );
                }
                if ( _outlineFillMaterial )
                {
                    _tempMaterials.Add( _outlineFillMaterial );
                }

                renderer.sharedMaterials = _tempMaterials.ToArray();
            }
            _tempMaterials.Clear();
        }

        void CleanAllOutlineMaterialsFromRenderers()
        {
            if ( _renderers == null ) return;

            foreach ( Renderer renderer in _renderers )
            {
                if ( !renderer ) continue;

                _tempMaterials.Clear();
                renderer.GetSharedMaterials( _tempMaterials );

                _tempMaterials.RemoveAll( mat => mat != null && 
                    ( mat.shader == _outlineMaskShader || mat.shader == _outlineFillShader ) );

                renderer.sharedMaterials = _tempMaterials.ToArray();
            }
            _tempMaterials.Clear();
        }

        void LoadSmoothNormals()
        {
            if ( _meshFilters == null ) return;

            foreach ( MeshFilter meshFilter in _meshFilters )
            {
                if ( !meshFilter ) continue;
                
                Mesh originalMesh;
                if ( !_originalMeshes.TryGetValue( meshFilter, out originalMesh ) )
                {
                    originalMesh = meshFilter.sharedMesh;
                }
                if ( !originalMesh ) continue;

                Mesh meshCopy;
                if ( !_clonedMeshes.TryGetValue( meshFilter, out meshCopy ) )
                {
                    meshCopy = Instantiate( originalMesh );
                    _clonedMeshes[meshFilter] = meshCopy;
                    _originalMeshes[meshFilter] = originalMesh;
                    meshFilter.sharedMesh = meshCopy;
                }

                List<Vector3> smoothNormals;
                if ( !_runtimeBakeCache.TryGetValue( originalMesh, out smoothNormals ) )
                {
                    smoothNormals = SmoothNormals( originalMesh );
                    _runtimeBakeCache[originalMesh] = smoothNormals;
                }

                meshCopy.SetUVs( 3, smoothNormals );

                Renderer renderer = meshFilter.GetComponent<Renderer>();
                if ( renderer )
                {
                    CombineSubmeshes( meshCopy, renderer.sharedMaterials );
                }
            }

            if ( _skinnedMeshRenderers != null )
            {
                foreach ( SkinnedMeshRenderer skinnedMeshRenderer in _skinnedMeshRenderers )
                {
                    if ( !skinnedMeshRenderer ) continue;
                    
                    Mesh originalMesh;
                    if ( !_originalSkinnedMeshes.TryGetValue( skinnedMeshRenderer, out originalMesh ) )
                    {
                        originalMesh = skinnedMeshRenderer.sharedMesh;
                    }
                    if ( !originalMesh ) continue;

                    Mesh meshCopy;
                    if ( !_clonedSkinnedMeshes.TryGetValue( skinnedMeshRenderer, out meshCopy ) )
                    {
                        meshCopy = Instantiate( originalMesh );
                        _clonedSkinnedMeshes[skinnedMeshRenderer] = meshCopy;
                        _originalSkinnedMeshes[skinnedMeshRenderer] = originalMesh;
                        skinnedMeshRenderer.sharedMesh = meshCopy;
                    }

                    meshCopy.uv4 = new Vector2[meshCopy.vertexCount];
                    CombineSubmeshes( meshCopy, skinnedMeshRenderer.sharedMaterials );
                }
            }
        }

        List<Vector3> SmoothNormals( Mesh mesh )
        {
            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            List<Vector3> smoothNormals = new List<Vector3>( normals );

            Dictionary<Vector3, List<int>> groups = new Dictionary<Vector3, List<int>>( vertices.Length );

            for ( int i = 0; i < vertices.Length; i++ )
            {
                Vector3 vertex = vertices[i];
                List<int> group;
                if ( !groups.TryGetValue( vertex, out group ) )
                {
                    group = new List<int>();
                    groups[vertex] = group;
                }
                group.Add( i );
            }

            foreach ( KeyValuePair<Vector3, List<int>> group in groups )
            {
                if ( group.Value.Count == 1 ) continue;

                Vector3 smoothNormal = Vector3.zero;
                for ( int i = 0; i < group.Value.Count; i++ )
                {
                    smoothNormal += normals[group.Value[i]];
                }

                smoothNormal.Normalize();

                for ( int i = 0; i < group.Value.Count; i++ )
                {
                    smoothNormals[group.Value[i]] = smoothNormal;
                }
            }

            return smoothNormals;
        }

        void CombineSubmeshes( Mesh mesh, Material[] materials )
        {
            if ( mesh.subMeshCount == materials.Length + 1 ) return;
            if ( mesh.subMeshCount == 1 ) return;
            if ( mesh.subMeshCount > materials.Length ) return;

            mesh.subMeshCount++;
            mesh.SetTriangles( mesh.triangles, mesh.subMeshCount - 1 );
        }

        #if UNITY_EDITOR
        public void EditorBake()
        {
            Bake();
            InitializeBakeCache();
            RefreshRenderers();
        }

        void Bake()
        {
            var bakedMeshes = new HashSet<Mesh>();
            _bakeKeys.Clear();
            _bakeValues.Clear();

            CacheComponents();
            if ( _meshFilters == null ) return;

            foreach ( MeshFilter meshFilter in _meshFilters )
            {
                if ( !meshFilter || !meshFilter.sharedMesh ) continue;
                if ( !bakedMeshes.Add( meshFilter.sharedMesh ) ) continue;

                List<Vector3> smoothNormals = SmoothNormals( meshFilter.sharedMesh );
                _bakeKeys.Add( meshFilter.sharedMesh );
                _bakeValues.Add( new ListVector3() { data = smoothNormals } );
            }
        }
        #endif

        void UpdateStaticMaterialProperties()
        {
            if ( !_outlineMaskMaterial || !_outlineFillMaterial ) return;

            switch ( _outlineMode )
            {
                case Mode.OutlineAll:
                    _outlineMaskMaterial.SetFloat( _zTestId, (float)UnityEngine.Rendering.CompareFunction.Always );
                    _outlineFillMaterial.SetFloat( _zTestId, (float)UnityEngine.Rendering.CompareFunction.Always );
                    break;

                case Mode.OutlineVisible:
                    _outlineMaskMaterial.SetFloat( _zTestId, (float)UnityEngine.Rendering.CompareFunction.Always );
                    _outlineFillMaterial.SetFloat( _zTestId, (float)UnityEngine.Rendering.CompareFunction.LessEqual );
                    break;

                case Mode.OutlineHidden:
                    _outlineMaskMaterial.SetFloat( _zTestId, (float)UnityEngine.Rendering.CompareFunction.Always );
                    _outlineFillMaterial.SetFloat( _zTestId, (float)UnityEngine.Rendering.CompareFunction.Greater );
                    break;

                case Mode.OutlineAndSilhouette:
                    _outlineMaskMaterial.SetFloat( _zTestId, (float)UnityEngine.Rendering.CompareFunction.LessEqual );
                    _outlineFillMaterial.SetFloat( _zTestId, (float)UnityEngine.Rendering.CompareFunction.Always );
                    break;

                case Mode.SilhouetteOnly:
                    _outlineMaskMaterial.SetFloat( _zTestId, (float)UnityEngine.Rendering.CompareFunction.LessEqual );
                    _outlineFillMaterial.SetFloat( _zTestId, (float)UnityEngine.Rendering.CompareFunction.Greater );
                    break;
            }

            // Write static properties via property block
            if ( _propertyBlock == null )
            {
                _propertyBlock = new MaterialPropertyBlock();
            }

            _propertyBlock.Clear();
            _propertyBlock.SetColor( _outlineColorId, _outlineColor );
            _propertyBlock.SetFloat( _outlineWidthId, _outlineMode == Mode.SilhouetteOnly ? 0f : _outlineWidth );
            _propertyBlock.SetFloat( _dashScaleId, _dashedPattern ? _dashScale : 0f );
            _propertyBlock.SetFloat( _dashSpeedId, _dashedPattern ? _dashSpeed : 0f );

            ApplyPropertyBlock();
        }

        void HandleDynamicEffects()
        {
            if ( !_outlineFillMaterial ) return;

            bool hasAnimations = _pulseWidth || _rainbowColor || _dashedPattern;
            if ( !hasAnimations ) return;

            Color color = _outlineColor;
            if ( _rainbowColor )
            {
                float hue = ( Time.time * _rainbowSpeed ) % 1.0f;
                color = Color.HSVToRGB( hue, 1f, 1f );
            }

            float width = _outlineWidth;
            if ( _outlineMode == Mode.SilhouetteOnly )
            {
                width = 0f;
            }
            else if ( _pulseWidth )
            {
                float lerpVal = ( Mathf.Sin( Time.time * _pulseSpeed ) + 1f ) / 2f;
                width = Mathf.Lerp( _pulseMinWidth, _pulseMaxWidth, lerpVal );
            }

            float scale = _dashedPattern ? _dashScale : 0f;
            float speed = _dashedPattern ? _dashSpeed : 0f;

            if ( _propertyBlock == null )
            {
                _propertyBlock = new MaterialPropertyBlock();
            }

            _propertyBlock.Clear();
            _propertyBlock.SetColor( _outlineColorId, color );
            _propertyBlock.SetFloat( _outlineWidthId, width );
            _propertyBlock.SetFloat( _dashScaleId, scale );
            _propertyBlock.SetFloat( _dashSpeedId, speed );

            ApplyPropertyBlock();
        }

        void ApplyPropertyBlock()
        {
            if ( _renderers == null ) return;

            foreach ( Renderer renderer in _renderers )
            {
                if ( renderer )
                {
                    renderer.SetPropertyBlock( _propertyBlock );
                }
            }
        }

        void ClearClonedMeshes()
        {
            foreach ( KeyValuePair<MeshFilter, Mesh> pair in _originalMeshes )
            {
                if ( pair.Key && pair.Value )
                {
                    pair.Key.sharedMesh = pair.Value;
                }
            }
            _originalMeshes.Clear();

            foreach ( KeyValuePair<MeshFilter, Mesh> pair in _clonedMeshes )
            {
                if ( pair.Value )
                {
                    DestroyOutlineObject( pair.Value );
                }
            }
            _clonedMeshes.Clear();

            foreach ( KeyValuePair<SkinnedMeshRenderer, Mesh> pair in _originalSkinnedMeshes )
            {
                if ( pair.Key && pair.Value )
                {
                    pair.Key.sharedMesh = pair.Value;
                }
            }
            _originalSkinnedMeshes.Clear();

            foreach ( KeyValuePair<SkinnedMeshRenderer, Mesh> pair in _clonedSkinnedMeshes )
            {
                if ( pair.Value )
                {
                    DestroyOutlineObject( pair.Value );
                }
            }
            _clonedSkinnedMeshes.Clear();
        }

        void DestroyOutlineObject( UnityEngine.Object obj )
        {
            if ( !obj ) return;
            if ( Application.isPlaying )
            {
                Destroy( obj );
            }
            else
            {
                DestroyImmediate( obj );
            }
        }
    }
}
