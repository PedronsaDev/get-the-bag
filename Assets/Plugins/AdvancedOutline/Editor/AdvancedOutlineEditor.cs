using UnityEditor;
using UnityEngine;

namespace ITISKIRUHERE
{
    [CustomEditor( typeof( AdvancedOutline ) )]
    [CanEditMultipleObjects]
    public class AdvancedOutlineEditor : Editor
    {
        static readonly GUIContent _outlineMaskShaderLabel = new GUIContent( "Mask Shader", "Shader used for masking the outline silhouette." );
        static readonly GUIContent _outlineFillShaderLabel = new GUIContent( "Fill Shader", "Shader used for rendering the outline stroke." );
        static readonly GUIContent _outlineModeLabel = new GUIContent( "Outline Mode", "Select where and how the outline should be rendered relative to depth." );
        static readonly GUIContent _outlineColorLabel = new GUIContent( "Outline Color", "Base outline color." );
        static readonly GUIContent _outlineWidthLabel = new GUIContent( "Outline Width", "Base outline thickness." );
        
        static readonly GUIContent _pulseWidthLabel = new GUIContent( "Pulse Width", "Enable pulsing width effect." );
        static readonly GUIContent _pulseSpeedLabel = new GUIContent( "Pulse Speed", "Speed of the pulse oscillation." );
        static readonly GUIContent _pulseMinWidthLabel = new GUIContent( "Pulse Min Width", "Minimum width during pulse." );
        static readonly GUIContent _pulseMaxWidthLabel = new GUIContent( "Pulse Max Width", "Maximum width during pulse." );
        
        static readonly GUIContent _rainbowColorLabel = new GUIContent( "Rainbow Color", "Enable rainbow color cycle effect." );
        static readonly GUIContent _rainbowSpeedLabel = new GUIContent( "Rainbow Speed", "Speed of the rainbow color cycling." );
        
        static readonly GUIContent _dashedPatternLabel = new GUIContent( "Dashed Pattern", "Enable dashed crawling-ants line effect." );
        static readonly GUIContent _dashScaleLabel = new GUIContent( "Dash Scale", "Scale / density of the dashes." );
        static readonly GUIContent _dashSpeedLabel = new GUIContent( "Dash Speed", "Speed of the crawling dash movement." );
        
        static readonly GUIContent _precomputeOutlineLabel = new GUIContent( "Precompute Outline", "Precompute smooth normals in the editor to avoid Awake() calculations at runtime." );

        SerializedProperty _outlineMaskShader;
        SerializedProperty _outlineFillShader;
        SerializedProperty _outlineMode;
        SerializedProperty _outlineColor;
        SerializedProperty _outlineWidth;
        SerializedProperty _pulseWidth;
        SerializedProperty _pulseSpeed;
        SerializedProperty _pulseMinWidth;
        SerializedProperty _pulseMaxWidth;
        SerializedProperty _rainbowColor;
        SerializedProperty _rainbowSpeed;
        SerializedProperty _dashedPattern;
        SerializedProperty _dashScale;
        SerializedProperty _dashSpeed;
        SerializedProperty _precomputeOutline;

        bool _generalFoldout = true;
        bool _effectsFoldout = true;
        bool _performanceFoldout = true;

        void OnEnable()
        {
            _outlineMaskShader = serializedObject.FindProperty( "_outlineMaskShader" );
            _outlineFillShader = serializedObject.FindProperty( "_outlineFillShader" );
            _outlineMode = serializedObject.FindProperty( "_outlineMode" );
            _outlineColor = serializedObject.FindProperty( "_outlineColor" );
            _outlineWidth = serializedObject.FindProperty( "_outlineWidth" );
            
            _pulseWidth = serializedObject.FindProperty( "_pulseWidth" );
            _pulseSpeed = serializedObject.FindProperty( "_pulseSpeed" );
            _pulseMinWidth = serializedObject.FindProperty( "_pulseMinWidth" );
            _pulseMaxWidth = serializedObject.FindProperty( "_pulseMaxWidth" );
            
            _rainbowColor = serializedObject.FindProperty( "_rainbowColor" );
            _rainbowSpeed = serializedObject.FindProperty( "_rainbowSpeed" );
            
            _dashedPattern = serializedObject.FindProperty( "_dashedPattern" );
            _dashScale = serializedObject.FindProperty( "_dashScale" );
            _dashSpeed = serializedObject.FindProperty( "_dashSpeed" );
            
            _precomputeOutline = serializedObject.FindProperty( "_precomputeOutline" );
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            AdvancedOutline firstTarget = (AdvancedOutline)target;

            DrawWarnings( firstTarget );

            EditorGUI.BeginChangeCheck();

            _generalFoldout = EditorGUILayout.Foldout( _generalFoldout, "General Settings", true );
            if ( _generalFoldout )
            {
                EditorGUI.indentLevel++;
                
                EditorGUILayout.PropertyField( _outlineMaskShader, _outlineMaskShaderLabel );
                EditorGUILayout.PropertyField( _outlineFillShader, _outlineFillShaderLabel );
                EditorGUILayout.Space();

                EditorGUILayout.PropertyField( _outlineMode, _outlineModeLabel );

                if ( _outlineMode.enumValueIndex != (int)AdvancedOutline.Mode.SilhouetteOnly )
                {
                    EditorGUI.BeginDisabledGroup( _rainbowColor.boolValue );
                    EditorGUILayout.PropertyField( _outlineColor, _outlineColorLabel );
                    EditorGUI.EndDisabledGroup();

                    EditorGUI.BeginDisabledGroup( _pulseWidth.boolValue );
                    EditorGUILayout.PropertyField( _outlineWidth, _outlineWidthLabel );
                    EditorGUI.EndDisabledGroup();
                }

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();

            _effectsFoldout = EditorGUILayout.Foldout( _effectsFoldout, "Dynamic Effects (Blendable)", true );
            if ( _effectsFoldout )
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.PropertyField( _pulseWidth, _pulseWidthLabel );
                EditorGUI.BeginDisabledGroup( !_pulseWidth.boolValue );
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField( _pulseSpeed, _pulseSpeedLabel );
                    
                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.PropertyField( _pulseMinWidth, _pulseMinWidthLabel );
                    EditorGUILayout.PropertyField( _pulseMaxWidth, _pulseMaxWidthLabel );
                    if ( EditorGUI.EndChangeCheck() )
                    {
                        if ( _pulseMinWidth.floatValue > _pulseMaxWidth.floatValue )
                        {
                            _pulseMinWidth.floatValue = _pulseMaxWidth.floatValue;
                        }
                    }
                    EditorGUI.indentLevel--;
                }
                EditorGUI.EndDisabledGroup();

                EditorGUILayout.Space();

                EditorGUILayout.PropertyField( _rainbowColor, _rainbowColorLabel );
                EditorGUI.BeginDisabledGroup( !_rainbowColor.boolValue );
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField( _rainbowSpeed, _rainbowSpeedLabel );
                    EditorGUI.indentLevel--;
                }
                EditorGUI.EndDisabledGroup();

                EditorGUILayout.Space();

                EditorGUILayout.PropertyField( _dashedPattern, _dashedPatternLabel );
                EditorGUI.BeginDisabledGroup( !_dashedPattern.boolValue );
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField( _dashScale, _dashScaleLabel );
                    EditorGUILayout.PropertyField( _dashSpeed, _dashSpeedLabel );
                    EditorGUI.indentLevel--;
                }
                EditorGUI.EndDisabledGroup();

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();

            _performanceFoldout = EditorGUILayout.Foldout( _performanceFoldout, "System & Performance", true );
            if ( _performanceFoldout )
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField( _precomputeOutline, _precomputeOutlineLabel );
                
                DrawPerformanceStats( firstTarget );

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space();

            DrawControlButtons( firstTarget );

            if ( EditorGUI.EndChangeCheck() || serializedObject.hasModifiedProperties )
            {
                serializedObject.ApplyModifiedProperties();
                
                foreach ( Object t in targets )
                {
                    AdvancedOutline outline = (AdvancedOutline)t;
                    Undo.RecordObject( outline, "Outline Settings Changed" );
                    outline.OutlineMode = (AdvancedOutline.Mode)_outlineMode.enumValueIndex;
                    EditorUtility.SetDirty( outline );
                }
            }
        }

        void DrawWarnings( AdvancedOutline targetOutline )
        {
            if ( !_outlineMaskShader.objectReferenceValue )
            {
                EditorGUILayout.HelpBox( "Mask Shader reference is missing! Please assign it under General Settings.", MessageType.Error );
            }
            if ( !_outlineFillShader.objectReferenceValue )
            {
                EditorGUILayout.HelpBox( "Fill Shader reference is missing! Please assign it under General Settings.", MessageType.Error );
            }

            int renderersCount = targetOutline.RendererCount;
            if ( renderersCount > 30 )
            {
                EditorGUILayout.HelpBox( $"Warning: This GameObject contains {renderersCount} child renderers! Running outlines on large hierarchies can significantly impact performance.", MessageType.Warning );
            }

            int meshCount = targetOutline.MeshCount;
            if ( !_precomputeOutline.boolValue && meshCount > 5 )
            {
                EditorGUILayout.HelpBox( $"Notice: Calculating smooth normals at runtime for {meshCount} meshes. If this causes frame drops on load, tick 'Precompute Outline' to bake them.", MessageType.Info );
            }
        }

        void DrawPerformanceStats( AdvancedOutline targetOutline )
        {
            EditorGUILayout.Space();
            EditorGUILayout.BeginVertical( EditorStyles.helpBox );
            EditorGUILayout.LabelField( "Performance Diagnostics", EditorStyles.boldLabel );
            EditorGUILayout.LabelField( $"Outlined Renderers: {targetOutline.RendererCount}" );
            EditorGUILayout.LabelField( $"Total Meshes: {targetOutline.MeshCount}" );
            
            string bakeStatus = _precomputeOutline.boolValue ? "Bake Cached (Editor Precomputed)" : "Runtime Generative (Awake)";
            EditorGUILayout.LabelField( $"Normal Setup Mode: {bakeStatus}" );
            EditorGUILayout.EndVertical();
        }

        void DrawControlButtons( AdvancedOutline targetOutline )
        {
            EditorGUILayout.BeginHorizontal();
            
            if ( GUILayout.Button( "Manual Refresh", GUILayout.Height( 30 ) ) )
            {
                ManualRefreshRenderers();
            }

            if ( _precomputeOutline.boolValue )
            {
                if ( GUILayout.Button( "Rebake Normals", GUILayout.Height( 30 ) ) )
                {
                    RebakeNormals();
                }
            }

            if ( GUILayout.Button( "Reset Defaults", GUILayout.Height( 30 ) ) )
            {
                ResetToDefaults();
            }

            EditorGUILayout.EndHorizontal();
        }

        void ResetToDefaults()
        {
            foreach ( Object t in targets )
            {
                AdvancedOutline outline = (AdvancedOutline)t;
                Undo.RecordObject( outline, "Reset Outline Defaults" );
                outline.OutlineMode = AdvancedOutline.Mode.OutlineVisible;
                outline.OutlineColor = Color.cyan;
                outline.OutlineWidth = 4f;
                outline.PulseWidth = false;
                outline.RainbowColor = false;
                outline.DashedPattern = false;
                EditorUtility.SetDirty( outline );
            }
        }

        void ManualRefreshRenderers()
        {
            foreach ( Object t in targets )
            {
                AdvancedOutline outline = (AdvancedOutline)t;
                Undo.RecordObject( outline, "Manual Refresh Renderers" );
                outline.RefreshRenderers();
                EditorUtility.SetDirty( outline );
            }
        }

        void RebakeNormals()
        {
            foreach ( Object t in targets )
            {
                AdvancedOutline outline = (AdvancedOutline)t;
                Undo.RecordObject( outline, "Rebake Normals" );
                outline.EditorBake();
                EditorUtility.SetDirty( outline );
            }
        }
    }
}
