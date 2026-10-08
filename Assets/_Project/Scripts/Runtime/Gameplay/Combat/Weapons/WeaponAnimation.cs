using UnityEngine;

namespace Project.Combat.Weapons
{
    // Optional Animator contract. Missing controllers/parameters are valid for debug visuals.
    public static class WeaponAnimation
    {
        private static readonly int Fire = Animator.StringToHash("Fire");
        private static readonly int Reload = Animator.StringToHash("Reload");
        private static readonly int Reloading = Animator.StringToHash("IsReloading");

        public static void PlayFire(Animator animator) => Trigger(animator, Fire);
        public static void SetReloading(Animator animator, bool reloading)
        {
            if (!animator || !animator.runtimeAnimatorController) return;
            foreach (var parameter in animator.parameters)
                if (parameter.nameHash == Reloading && parameter.type == AnimatorControllerParameterType.Bool)
                    animator.SetBool(Reloading, reloading);
            foreach (var parameter in animator.parameters)
                if (parameter.nameHash == Reload && parameter.type == AnimatorControllerParameterType.Trigger)
                {
                    if (reloading) animator.SetTrigger(Reload);
                    else animator.ResetTrigger(Reload);
                    break;
                }
        }

        private static void Trigger(Animator animator, int hash)
        {
            if (!animator || !animator.runtimeAnimatorController) return;
            foreach (var parameter in animator.parameters)
                if (parameter.nameHash == hash && parameter.type == AnimatorControllerParameterType.Trigger)
                {
                    animator.SetTrigger(hash);
                    return;
                }
        }
    }
}
