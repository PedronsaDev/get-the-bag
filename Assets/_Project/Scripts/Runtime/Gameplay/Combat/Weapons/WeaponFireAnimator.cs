using Project.Logging;
using UnityEngine;

namespace Project.Combat.Weapons
{
    /// <summary>Plays cosmetic fire animation from the visual's accepted-shot event.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(WeaponInstance))]
    public sealed class WeaponFireAnimator : MonoBehaviour
    {
        [SerializeField] private Animator _animator;
        [SerializeField] private string _fireState = "Base Layer.Fire";
        [SerializeField, Min(0)] private int _layer;

        private WeaponInstance _weapon;
        private int _fireStateHash;
        private bool _reportedInvalidBinding;

        private void Awake()
        {
            _weapon = GetComponent<WeaponInstance>();
            _fireStateHash = Animator.StringToHash(_fireState);
        }

        private void OnEnable()
        {
            _reportedInvalidBinding = false;
            _weapon.Fired += OnFired;
        }

        private void OnDisable()
        {
            _weapon.Fired -= OnFired;
        }

        private void OnFired()
        {
            if (!_animator || !_animator.isActiveAndEnabled || !_animator.runtimeAnimatorController ||
                _layer < 0 || _layer >= _animator.layerCount || !_animator.HasState(_layer, _fireStateHash))
            {
                if (!_reportedInvalidBinding)
                {
                    AppLog.LogWarning(LogChannel.Combat,
                        "Weapon fire animation requires an enabled Animator and a valid full state path/layer.", this);
                    _reportedInvalidBinding = true;
                }
                return;
            }

            // Restart on each accepted shot; no animation event changes gameplay state.
            _animator.Play(_fireStateHash, _layer, 0f);
        }
    }
}
