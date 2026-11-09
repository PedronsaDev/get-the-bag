using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Plugins.Outline.Editor
{
    public readonly struct OutlineValidationIssue
    {
        public readonly string Message;
        public readonly bool Error;
        public OutlineValidationIssue(string message, bool error = false) { Message = message; Error = error; }
    }

    public static class OutlineValidator
    {
        public static List<OutlineValidationIssue> Validate(MeshOutline outline)
        {
            var issues = new List<OutlineValidationIssue>();
            foreach (var shaderName in new[] { "Custom/Outline/Mask", "Custom/Outline/Fill", "Custom/Outline/Silhouette" })
            {
                var shader = Shader.Find(shaderName);
                if (!shader || !shader.isSupported) issues.Add(new OutlineValidationIssue("Missing or unsupported shader: " + shaderName, true));
            }
            int included = 0;
            foreach (var renderer in outline.GetComponentsInChildren<Renderer>(true))
            {
                if (!outline.IncludesRenderer(renderer)) continue;
                included++;
                var mesh = outline.SourceMesh(renderer);
                if (!mesh) { issues.Add(new OutlineValidationIssue(renderer.name + ": missing mesh.", true)); continue; }
                var preparation = outline.PreparationSource(mesh);
                if (!preparation || !preparation.isReadable)
                    issues.Add(new OutlineValidationIssue(renderer.name + ": bake outline data before using this unreadable mesh in a player build.", true));
                if (!mesh.HasVertexAttribute(VertexAttribute.Normal))
                    issues.Add(new OutlineValidationIssue(renderer.name + ": missing normals; recalculate normals on the model before outlining.", true));
                if (mesh.subMeshCount == 0) issues.Add(new OutlineValidationIssue(renderer.name + ": empty mesh.", true));
                for (int i = 0; i < mesh.subMeshCount; i++)
                    if (mesh.GetTopology(i) != MeshTopology.Triangles)
                        issues.Add(new OutlineValidationIssue(renderer.name + ": non-triangle topology is unsupported.", true));
                if ((GameObjectUtility.GetStaticEditorFlags(renderer.gameObject) & StaticEditorFlags.BatchingStatic) != 0)
                    issues.Add(new OutlineValidationIssue(renderer.name + ": disable static batching for MeshOutline.", true));
                foreach (var material in renderer.sharedMaterials)
                {
                    if (!material) { issues.Add(new OutlineValidationIssue(renderer.name + ": empty material slot; slots are preserved at runtime.")); continue; }
                    if (material.renderQueue >= 3000 || (material.HasProperty("_AlphaClip") && material.GetFloat("_AlphaClip") > 0.5f))
                        issues.Add(new OutlineValidationIssue(renderer.name + ": transparent/cutout material needs visual review; outline geometry does not reproduce alpha clipping."));
                }
            }
            if (included == 0) issues.Add(new OutlineValidationIssue("No renderers match this outline's ownership and filters.", true));
            foreach (var data in outline.PreparedMeshes)
            {
                if (!data || !data.Source || !data.ReadableCopy)
                    issues.Add(new OutlineValidationIssue("Missing prepared mesh reference; bake again.", true));
                else if (data.SourceDependencyHash != AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(data.Source)).ToString())
                    issues.Add(new OutlineValidationIssue(data.Source.name + ": source asset changed since baking. Bake again.", true));
            }
            return issues;
        }
    }
}
