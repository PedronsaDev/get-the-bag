using UnityEngine;

namespace Plugins.Outline
{
    // An author-time copy keeps imported model data read-only at runtime.
    public sealed class OutlinePreparedMesh : ScriptableObject
    {
        [SerializeField] private Mesh source;
        [SerializeField] private Mesh readableCopy;
        [SerializeField] private string sourceDependencyHash;
        public Mesh Source => source;
        public Mesh ReadableCopy => readableCopy;
        public string SourceDependencyHash => sourceDependencyHash;

#if UNITY_EDITOR
        public void SetData(Mesh sourceMesh, Mesh preparedCopy, string dependencyHash)
        {
            source = sourceMesh;
            readableCopy = preparedCopy;
            sourceDependencyHash = dependencyHash;
        }
#endif
    }
}
