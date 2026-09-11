using UnityEngine;
using UnityEditor;

namespace TinyGiantStudio.BetterInspector
{
    public static class FindAllSkinnedMeshRendererEditors
    {
        [MenuItem("Tools/Tiny Giant Studio/Utility/Log All Custom Editors for Skinned Mesh Renderer in Project",false,99999)]
        static void Find()
        {
            FindAllEditorsForComponent.Find(typeof(SkinnedMeshRenderer), 
                "TinyGiantStudio.BetterInspector.BetterMesh.BetterSkinnedMeshRendererEditor", 
                new []{"UnityEditor.SkinnedMeshRendererEditor","UnityEditor.Rendering.Universal.SkinnedMeshEditor2DURP"});
        }
    }
}