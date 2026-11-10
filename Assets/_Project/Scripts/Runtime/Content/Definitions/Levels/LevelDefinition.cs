using UnityEngine;

namespace Project.Levels.Data
{
[CreateAssetMenu(fileName = "LVL_Name", menuName = "Levels/Level Definition")]
public class LevelDefinition : ScriptableObject
{
    [Tooltip("Exact scene name as listed in the build settings, e.g. Scene_Bank.")]
    [SerializeField] private string _sceneName;
    [SerializeField] private string _displayName;
    [SerializeField, TextArea] private string _tagline;
    [Tooltip("Full-screen art shown behind the loading text. Optional.")]
    [SerializeField] private Sprite _loadingImage;

    public string SceneName => _sceneName;
    public string DisplayName => string.IsNullOrEmpty(_displayName) ? _sceneName : _displayName;
    public string Tagline => _tagline;
    public Sprite LoadingImage => _loadingImage;
}
}
