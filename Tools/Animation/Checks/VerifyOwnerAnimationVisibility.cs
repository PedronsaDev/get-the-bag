using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Project.Combat.Weapons;
using Project.Player.Animation;
using Project.Weapons.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class VerifyOwnerAnimationVisibility
{
    static void Call(object target, string method) => target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);

    public static string Main()
    {
        var character = PrefabUtility.LoadPrefabContents("Assets/_Project/Prefabs/Player/CHAR_PF_Player.prefab");
        var world = PrefabUtility.LoadPrefabContents("Assets/_Project/Prefabs/Weapons/Pistols/WEAP_PF_Pistol_A.prefab");
        var checks = new List<object>();
        Action<string, bool> check = (name, passed) => { checks.Add(new { name, passed }); if (!passed) throw new Exception(name); };
        try
        {
            var rig = character.GetComponent<CharacterRigBinding>();
            var visibility = character.GetComponent<CharacterVisualVisibility>();
            Call(rig, "Awake");
            var original = rig.BodyAnimator.cullingMode;
            visibility.Configure(true, rig);
            check("Owner hidden body keeps sockets and shadows animating", rig.BodyAnimator.cullingMode == AnimatorCullingMode.AlwaysAnimate);
            check("Owner body renderers are shadow-only", rig.BodyRenderers.All(r => r.shadowCastingMode == ShadowCastingMode.ShadowsOnly));
            visibility.ApplyWeaponViews(world.GetComponent<WeaponInstance>(), null);
            check("Owner world mechanisms keep shadow poses animating", world.GetComponentsInChildren<Animator>(true).All(a => a.cullingMode == AnimatorCullingMode.AlwaysAnimate));
            visibility.Configure(false, rig);
            check("Observer keeps the prefab culling policy", rig.BodyAnimator.cullingMode == original);
            visibility.Configure(true, rig);
            Call(visibility, "OnDisable");
            check("Disable restores original body culling", rig.BodyAnimator.cullingMode == original);
            Call(visibility, "OnEnable");
            check("Re-enable restores owner animation policy", rig.BodyAnimator.cullingMode == AnimatorCullingMode.AlwaysAnimate);
            check("Configured character no longer has an unresolved culling warning", !CharacterRigValidator.Validate(rig).Any(i => i.Message.Contains("Culled bodies")));
            Call(visibility, "OnDisable");
            visibility.enabled = false;
            check("Validator diagnoses missing owner culling policy", CharacterRigValidator.Validate(rig).Any(i => i.Message.Contains("Culled bodies")));
            var report = JsonConvert.SerializeObject(new { checks });
            File.WriteAllText(".codex/animation-checks/visibility-regression.json", report);
            return report;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(world);
            PrefabUtility.UnloadPrefabContents(character);
        }
    }
}
