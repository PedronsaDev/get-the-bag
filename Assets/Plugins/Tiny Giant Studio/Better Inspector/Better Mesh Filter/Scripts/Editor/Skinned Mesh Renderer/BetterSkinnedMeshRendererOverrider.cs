using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace TinyGiantStudio.BetterInspector
{
    [InitializeOnLoad]
    public static class BetterSkinnedMeshRendererOverrider //Renamed to fix a big path length warning by Unity
    {
        static BetterSkinnedMeshRendererOverrider()
        { 
            RemoveCompetingEditor();
        }

        //This is the first version of the code. I am adding null check to everything out of paranoia
        static void RemoveCompetingEditor()
        {
            Type cea = typeof(Editor).Assembly.GetType("UnityEditor.CustomEditorAttributes");

            FieldInfo instanceField = cea.GetField("k_Instance", BindingFlags.NonPublic | BindingFlags.Static);
            if (instanceField == null) return;

            object lazyInstance = instanceField.GetValue(null);
            object instance = lazyInstance.GetType().GetProperty("Value")?.GetValue(lazyInstance);
            if (instance == null) return;

            FieldInfo cacheField =
                instance.GetType().GetField("m_Cache", BindingFlags.NonPublic | BindingFlags.Instance);
            if (cacheField == null) return;
            object cache = cacheField.GetValue(instance);

            FieldInfo dictField = cache.GetType().GetField("m_CustomEditorCache",
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
            if (dictField == null) return;

            IDictionary dict = dictField.GetValue(cache) as IDictionary;
            if (dict == null) return;

            Type targetType = typeof(SkinnedMeshRenderer);
            if (!dict.Contains(targetType)) return;

            object storage = dict[targetType];
            FieldInfo listField = storage.GetType()
                .GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(f => typeof(IList).IsAssignableFrom(f.FieldType));
            if (listField == null) return;

            IList editorList = listField.GetValue(storage) as IList;
            if (editorList == null) return;
            if (editorList.Count == 0) return;

            FieldInfo inspectorTypeField = editorList[0].GetType()
                .GetField("inspectorType", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            if (inspectorTypeField == null) return;

            Type yourEditorType = typeof(BetterMesh.BetterSkinnedMeshRendererEditor);
            Type unityDefaultType = Type.GetType("UnityEditor.SkinnedMeshRendererEditor, UnityEditor");
            for (int i = editorList.Count - 1; i >= 0; i--)
            {
                if (inspectorTypeField.GetValue(editorList[i]) is not Type inspectorType ||
                    inspectorType == yourEditorType || inspectorType == unityDefaultType) continue;

                // Debug.Log("Removed " + inspectorTypeField.GetValue(editorList[i]));
                editorList.RemoveAt(i);
            }
        }
    } 
}