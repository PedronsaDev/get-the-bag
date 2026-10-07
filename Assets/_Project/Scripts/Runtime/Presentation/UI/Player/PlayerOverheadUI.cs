using Project.Core;
using Project.Player.Profile;
using PurrNet;
using TMPro;
using UnityEngine;

namespace Project.UI.Player
{
public class PlayerOverheadUI : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private RuntimePlayerProfile _playerProfile;
    [SerializeField] private Canvas _worldCanvas;
    [SerializeField] private TMP_Text _nameText;
    
    protected override void OnSpawned()
    {
        base.OnSpawned();

        if (isOwner)
        {
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);
        _playerProfile.OnNameChanged += HandlePlayerNameChanged;
        _playerProfile.OnExperienceChanged += HandlePlayerExperienceChanged;
        _playerProfile.OnLevelChanged += HandlePlayerLevelChanged;

        if (_worldCanvas && CameraRig.Instance)
        {
            _worldCanvas.worldCamera = CameraRig.Instance.Camera;
        }

        if (!string.IsNullOrEmpty(_playerProfile.Name))
        {
            HandlePlayerNameChanged(_playerProfile.Name);
        }
    }

    protected override void OnDespawned()
    {
        base.OnDespawned();

        if (!isOwner && _playerProfile)
        {
            _playerProfile.OnNameChanged -= HandlePlayerNameChanged;
            _playerProfile.OnExperienceChanged -= HandlePlayerExperienceChanged;
            _playerProfile.OnLevelChanged -= HandlePlayerLevelChanged;
        }
    }

    public void HandlePlayerNameChanged(string newName)
    {
        if (_nameText)
        {
            _nameText.text = newName;
        }
    }
    
    private void HandlePlayerLevelChanged(int newLevel)
    {
        
    }
    
    private void HandlePlayerExperienceChanged(int newExperience, int delta)
    {
        
    }
}
}