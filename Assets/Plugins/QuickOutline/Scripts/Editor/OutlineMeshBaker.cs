using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Plugins.Outline.Editor
{
    public static class OutlineMeshBaker
    {
        private const string Folder = "Assets/_Project/Art/Generated/Outlines";

        public static void Bake(MeshOutline outline)
        {
            if (Application.isPlaying) throw new InvalidOperationException("Bake outline meshes outside Play Mode.");
            EnsureFolder(Folder);
            var prepared = new List<OutlinePreparedMesh>();
            var sources = new HashSet<Mesh>();
            foreach (var renderer in outline.GetComponentsInChildren<Renderer>(true))
            {
                if (!outline.IncludesRenderer(renderer)) continue;
                var source = outline.SourceMesh(renderer);
                if (!source || !sources.Add(source)) continue;
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source, out string guid, out long localId);
                if (string.IsNullOrEmpty(guid)) throw new InvalidOperationException("Save generated meshes as assets before baking.");
                string path = Folder + "/" + guid + "-" + localId + ".asset";
                var data = AssetDatabase.LoadAssetAtPath<OutlinePreparedMesh>(path);
                var copy = OutlineMeshPreparation.Bake(source);
                if (!data)
                {
                    data = ScriptableObject.CreateInstance<OutlinePreparedMesh>();
                    AssetDatabase.CreateAsset(data, path);
                }
                else if (data.ReadableCopy) UnityEngine.Object.DestroyImmediate(data.ReadableCopy, true);
                copy.hideFlags = HideFlags.None;
                // An imported clone inherits the model's CPU-readability flag. Only the
                // generated copy needs CPU data; the imported source remains unchanged.
                var meshSettings = new SerializedObject(copy);
                meshSettings.FindProperty("m_IsReadable").boolValue = true;
                meshSettings.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.AddObjectToAsset(copy, data);
                data.name = source.name + " Outline Data";
                data.SetData(source, copy, AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(source)).ToString());
                EditorUtility.SetDirty(data);
                AssetDatabase.SaveAssetIfDirty(data);
                prepared.Add(data);
            }
            Undo.RecordObject(outline, "Bake Outline Meshes");
            outline.SetPreparedMeshes(prepared);
            EditorUtility.SetDirty(outline);
            if (PrefabUtility.IsPartOfPrefabInstance(outline)) PrefabUtility.RecordPrefabInstancePropertyModifications(outline);
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
