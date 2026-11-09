using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using Newtonsoft.Json;
using Project.Combat.Data;
using Project.Definitions.Weapons;
using Project.Weapons.Editor;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class VerifyGroupedReloadValidation
{
    const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Set(object target, string field, object value) => target.GetType().GetField(field, Fields).SetValue(target, value);
    public static string Main()
    {
        var saved = AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/_Project/Data/Weapons/Weapon Definitions/Pistols/WEAP_Pistol_A.asset");
        var character = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Player/CHAR_PF_Player.prefab");
        var definition = Object.Instantiate(saved);
        var presentation = Object.Instantiate(saved.PresentationDefinition);
        string before = EditorJsonUtility.ToJson(saved.PresentationDefinition);
        Set(definition, "_presentationDefinition", presentation);
        var checks = new List<object>();
        Action<string, bool> check = (name, passed) => { checks.Add(new { name, passed }); if (!passed) throw new Exception(name); };
        Func<IReadOnlyList<ValidationIssue>> pair = () => AnimationValidationCommands.ValidatePresentation(character, new[] { definition });
        try
        {
            var savedIssues = pair();
            check("Saved character/pistol has no validation errors", !savedIssues.Any(i => i.Level == ValidationSeverity.Error));
            foreach (string guid in AssetDatabase.FindAssets("t:WeaponDefinition", new[] { "Assets/_Project" }))
            {
                var weapon = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                check("Saved weapon validates: " + weapon.name, !WeaponAssetValidator.Validate(weapon).Any(i => i.Level == ValidationSeverity.Error));
            }
            var rifle = AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/_Project/Data/Weapons/Weapon Definitions/Rifles/WEAP_Rifle_Debug.asset");
            check("Debug rifle has explicit compatible placeholder body reload", !AnimationValidationCommands.ValidatePresentation(character, new[] { rifle }).Any(i => i.Level == ValidationSeverity.Error));
            var body = presentation.ReloadEmpty.BodyClip;
            Set(presentation.ReloadEmpty, "_bodyClip", presentation.ReloadEmpty.FirstPersonClip);
            check("Body validator rejects a Generic FP reload", pair().Any(i => i.Message.Contains("Humanoid/Generic")));
            Set(presentation.ReloadEmpty, "_bodyClip", presentation.BodyHoldClip);
            check("Body validator rejects a looping reload", pair().Any(i => i.Message.Contains("non-looping")));
            Set(presentation.ReloadEmpty, "_bodyClip", null);
            check("Missing optional Empty body clip is accepted", !pair().Any(i => i.Level == ValidationSeverity.Error));
            Set(presentation.ReloadEmpty, "_bodyClip", body);
            var normalBody = presentation.ReloadNormal.BodyClip;
            Set(presentation.ReloadNormal, "_bodyClip", null);
            check("Declared character pair requires Normal body motion", pair().Any(i => i.Message.Contains("Normal body clip")));
            Set(presentation.ReloadNormal, "_bodyClip", normalBody);
            var normalCurve = presentation.ReloadNormal.SupportHandWeight;
            Set(presentation.ReloadNormal, "_supportHandWeight", AnimationCurve.Constant(0,1,1.2f));
            check("Normal hand curve rejects out-of-range weight", WeaponAssetValidator.Validate(definition).Any(i => i.Message.Contains("Normal reload support-hand")));
            Set(presentation.ReloadNormal, "_supportHandWeight", normalCurve);
            var emptyCurve = presentation.ReloadEmpty.SupportHandWeight;
            Set(presentation.ReloadEmpty, "_supportHandWeight", AnimationCurve.Linear(-.1f,0,1,1));
            check("Empty hand curve rejects progress outside the action", WeaponAssetValidator.Validate(definition).Any(i => i.Message.Contains("Empty reload support-hand")));
            Set(presentation.ReloadEmpty, "_supportHandWeight", new AnimationCurve());
            check("Empty hand curve is valid authored-hand control", !pair().Any(i => i.Level == ValidationSeverity.Error));
            Set(presentation.ReloadEmpty, "_supportHandWeight", emptyCurve);
            Set(presentation, "_reloadEmpty", null);
            check("Missing entire Empty entry is accepted", !pair().Any(i => i.Level == ValidationSeverity.Error));
            check("Validation leaves saved presentation untouched", before == EditorJsonUtility.ToJson(saved.PresentationDefinition) && !EditorUtility.IsDirty(saved.PresentationDefinition));
            var result = JsonConvert.SerializeObject(new { checks, savedWarnings = savedIssues.Where(i => i.Level == ValidationSeverity.Warning).Select(i => i.Message) });
            File.WriteAllText(".codex/animation-checks/validator-regression.json", result);
            return result;
        }
        finally { Object.DestroyImmediate(presentation); Object.DestroyImmediate(definition); }
    }
}
