using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Project.Combat.Weapons;

// Run with Unity CLI run_script while the Editor is in Play Mode.
public static class VerifyWeaponFireAnimation
{
    public static string Main()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Run in Play Mode.");
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Project/Prefabs/Weapons/WEAP_FP_Pistol_Placeholder.prefab");
        var root = UnityEngine.Object.Instantiate(prefab, new Vector3(0, -1000, 0), Quaternion.identity);
        try
        {
            var weapon = root.GetComponent<WeaponInstance>();
            var consumer = root.GetComponent<WeaponFireAnimator>();
            var animator = root.transform.Find("Mesh Root").GetComponent<Animator>();
            var pose = animator.transform.Find("AuthoredPose");
            int idle = Animator.StringToHash("Base Layer.Idle");
            int fire = Animator.StringToHash("Base Layer.Fire");
            var eventField = typeof(WeaponInstance).GetField("Fired", BindingFlags.Instance | BindingFlags.NonPublic);
            Func<int> subscribers = () => (eventField.GetValue(weapon) as Delegate)?.GetInvocationList()
                .Count(d => ReferenceEquals(d.Target, consumer)) ?? 0;
            Action<bool, string> check = (pass, message) => { if (!pass) throw new Exception(message); };
            animator.Update(0);
            check(subscribers() == 1, "Initial subscription must occur once.");
            weapon.PlayFire();
            animator.Update(0); // Commit Play's pending state change before advancing its clock.
            animator.Update(0.03f);
            check(animator.GetCurrentAnimatorStateInfo(0).fullPathHash == fire && pose.localPosition.z < -0.01f,
                "Accepted shot did not produce a visible fire kick.");
            weapon.PlayFire();
            animator.Update(0);
            check(animator.GetCurrentAnimatorStateInfo(0).normalizedTime < 0.01f, "Rapid shot did not restart fire.");
            for (int i = 0; i < 30; ++i) animator.Update(0.02f);
            check(animator.GetCurrentAnimatorStateInfo(0).fullPathHash == idle && pose.localPosition.sqrMagnitude < 0.000001f,
                "Fire did not return to idle/rest.");
            consumer.enabled = false;
            check(subscribers() == 0, "Disabled consumer retains event subscription.");
            weapon.PlayFire();
            animator.Update(0.03f);
            check(animator.GetCurrentAnimatorStateInfo(0).fullPathHash == idle, "Disabled consumer still plays fire.");
            for (int i = 0; i < 10; ++i) { consumer.enabled = true; consumer.enabled = false; }
            consumer.enabled = true;
            check(subscribers() == 1, "Enable cycles duplicated subscription.");
            weapon.PlayFire();
            animator.Update(0);
            animator.Update(0.03f);
            check(pose.localPosition.z < -0.01f, "Reenabled consumer does not play.");
            root.SetActive(false);
            check(subscribers() == 0, "Released visual retains subscription.");
            root.SetActive(true);
            animator.Update(0);
            check(subscribers() == 1 && pose.localPosition.sqrMagnitude < 0.000001f,
                "Reactivated visual did not reset to rest with one binding.");
            return "PASS: accepted fire, rapid restart, return to rest, disabled isolation, ten subscription cycles, release and reactivation.";
        }
        finally { root.SetActive(false); UnityEngine.Object.Destroy(root); }
    }
}
