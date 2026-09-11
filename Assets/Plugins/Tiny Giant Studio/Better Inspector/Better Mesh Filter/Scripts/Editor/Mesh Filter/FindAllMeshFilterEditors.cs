using System;
using UnityEditor;
using UnityEngine;

namespace TinyGiantStudio.BetterInspector
{
    public static class FindAllMeshFilterEditors
    {
        [MenuItem("Tools/Tiny Giant Studio/Utility/Log All Custom Editors for Mesh Filter in Project",false,99999)]
        static void Find()
        {
            FindAllEditorsForComponent.Find(typeof(MeshFilter), 
                "TinyGiantStudio.BetterInspector.BetterMesh.BetterMeshFilterEditor", 
                new []{"UnityEditor.MeshFilterEditor"});
        }
    }
}