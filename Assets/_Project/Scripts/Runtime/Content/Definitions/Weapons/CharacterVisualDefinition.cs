using UnityEngine;

namespace Project.Definitions.Weapons
{
[CreateAssetMenu(fileName = "CharacterVisualDefinition", menuName = "Weapons/CharacterVisualDefinition")]
public class CharacterVisualDefinition : ScriptableObject
{
    [SerializeField] private string _bodyRigFamily;
    [SerializeField] private string _firstPersonArmsFamily;
    [SerializeField] private string[] _supportedHoldingFamilies;

    public string BodyRigFamily => _bodyRigFamily;
    public string FirstPersonArmsFamily => _firstPersonArmsFamily;
    public string[] SupportedHoldingFamilies => _supportedHoldingFamilies;
}
}