using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TinyGiantStudio.BetterInspector
{
    public static class FindAllEditorsForComponent
    {
        public static void Find(Type componentType, string myEditorFullName, string[] defaultEditorFullName)
        {
            TypeCache.TypeCollection editorTypes = TypeCache.GetTypesWithAttribute<CustomEditor>();
            int count = 0;
            foreach (Type type in editorTypes)
            {
                object[] attr = type.GetCustomAttributes(typeof(CustomEditor), true);
                foreach (CustomEditor ce in attr)
                {
                    Type inspectedType = typeof(CustomEditor)
                        .GetField("m_InspectedType",
                            System.Reflection.BindingFlags.NonPublic |
                            System.Reflection.BindingFlags.Instance)
                        ?.GetValue(ce) as Type;

                    if (inspectedType != componentType) continue;

                    if (type.FullName == myEditorFullName)
                    {
                        Debug.Log("<color=green>Better Editor</color> in Path: <color=yellow>" +
                                  GetScriptPath(type) + "</color>");
                    }
                    else if (defaultEditorFullName.Contains(type.FullName))
                    {
                        Debug.Log("<color=green>Default Editor</color> " +
                                  type.FullName);
                    }
                    else
                    {
                        Debug.LogError("<i>Unknown Editor</i> <color=orange><b>" + type.FullName +
                                       "</b></color> in Path: <color=yellow>" + GetScriptPath(type) + "</color>");
                    }

                    count++;
                }
            }

            Debug.Log("Total number of editors found: " + count);
        }

        static string GetScriptPath(Type type)
        {
            string[] guids = AssetDatabase.FindAssets($"{type.Name} t:MonoScript");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (script != null && script.GetClass() == type)
                    return path;
            }

            return "Path not found";
        }
    }
}