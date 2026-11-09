using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json;
using Project.Combat.Data;
using Project.Combat.Weapons;
using Project.Content;
using Project.Definitions.Weapons;
using Project.Player.Animation;
using Project.Player.Combat;
using PurrNet;
using PurrNet.Modules;
using UnityEditor;
using UnityEngine;

public static class VerifyGroupedBodyReload
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const BindingFlags StaticFlags = BindingFlags.Static | BindingFlags.NonPublic;
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, Flags).SetValue(target, value);
    private static object Get(object target, string field) => target.GetType().GetField(field, Flags).GetValue(target);
    private static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, Flags).Invoke(target, args);
    private static AnimationClip ImportedClip(string path)
    {
        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
            if (asset is AnimationClip clip && !clip.name.StartsWith("__")) return clip;
        throw new Exception("No animation clip: " + path);
    }

    public static string Main()
    {
        NetworkManager.CallAllRegisters();
        var registryField = typeof(GameContent).GetField("_weaponRegistry", StaticFlags);
        var readyField = typeof(GameContent).GetField("_loadState", StaticFlags);
        var previousRegistry = registryField.GetValue(null); var previousReady = readyField.GetValue(null);
        var roots = new List<GameObject>(); var objects = new List<UnityEngine.Object>();
        var poses = new List<UpperBodyPoseController>(); var animators = new List<Animator>();
        var originals = new List<RuntimeAnimatorController>(); var controllers = new List<PlayerWeaponController>();
        var checks = new List<object>(); Action<string, bool> check = (name, passed) => checks.Add(new {name, passed});
        try
        {
            var actual = AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/_Project/Data/Weapons/Weapon Definitions/Pistols/WEAP_Pistol_A.asset");
            var normal = actual.PresentationDefinition.ReloadNormal.BodyClip; var hold = actual.PresentationDefinition.BodyHoldClip; var savedEmpty = actual.PresentationDefinition.ReloadEmpty.BodyClip;

            var empty = ImportedClip("Assets/_Project/Animations/Characters/Human/Locomotion/Jump/Body_Human_Jump.fbx");
            var otherNormal = ImportedClip("Assets/_Project/Animations/Characters/Human/Locomotion/Jump/Body_Human_Land.fbx");
            var ap = ScriptableObject.CreateInstance<WeaponPresentationDefinition>(); objects.Add(ap);
            Set(ap,"_bodyHoldClip",hold); Set(ap.ReloadNormal,"_bodyClip",normal); Set(ap.ReloadEmpty,"_bodyClip",empty);
            Set(ap.ReloadNormal,"_supportHandWeight",AnimationCurve.Linear(0,0,1,1));
            Set(ap.ReloadEmpty,"_supportHandWeight",AnimationCurve.Constant(0,1,.75f));
            var bp = ScriptableObject.CreateInstance<WeaponPresentationDefinition>(); objects.Add(bp);
            Set(bp,"_bodyHoldClip",hold); Set(bp.ReloadNormal,"_bodyClip",otherNormal);
            Set(bp.ReloadNormal,"_supportHandWeight",AnimationCurve.Constant(0,1,.4f));
            Set(bp.ReloadEmpty,"_supportHandWeight",AnimationCurve.Constant(0,1,.9f));
            var a = UnityEngine.Object.Instantiate(actual);objects.Add(a);Set(a,"_id","variant_test_a");Set(a,"_presentationDefinition",ap);
            var b = UnityEngine.Object.Instantiate(actual);objects.Add(b);Set(b,"_id","variant_test_b");Set(b,"_presentationDefinition",bp);
            var registry = ScriptableObject.CreateInstance<WeaponRegistry>();objects.Add(registry);
            registry.GetType().BaseType.GetField("_items",Flags).SetValue(registry,new List<WeaponDefinition>{a,b});registry.InitializeLookup();
            registryField.SetValue(null,registry);readyField.SetValue(null,GameContentLoadState.Ready);
            for(int i=0;i<2;i++)
            {
                var root=PrefabUtility.LoadPrefabContents("Assets/_Project/Prefabs/Player/CHAR_PF_Player.prefab");roots.Add(root);
                var pose=root.GetComponentInChildren<UpperBodyPoseController>(true);poses.Add(pose);
                var rig=(CharacterRigBinding)Get(pose,"_rigBinding");var animator=rig.BodyAnimator;
                animators.Add(animator);originals.Add(animator.runtimeAnimatorController);
                Set(pose, "_bodyAnimator", animator);
                animator.runtimeAnimatorController = null;
                Call(pose, "OnDestroy");
                check("Failed initialization cleanup is safe for character " + i, animator.runtimeAnimatorController == null);
                animator.runtimeAnimatorController = originals[i];
                animator.Rebind();animator.Update(0);
                Call(pose,"Awake");objects.Add(animator.runtimeAnimatorController);
                controllers.Add((PlayerWeaponController)Get(pose,"_weaponController"));
            }
            var manager=roots[0].AddComponent<NetworkManager>();var ticks=new TickManager(60,manager,null,true);Set(manager,"_serverTickManager",ticks);
            typeof(NetworkIdentity).GetProperty("networkManager",Flags).SetValue(controllers[0],manager);
            typeof(NetworkIdentity).GetField("_isSpawnedServer",Flags).SetValue(controllers[0],true);
            Action<double> clock=seconds=>{double t=seconds*60;Set(ticks,"<localTick>k__BackingField",(uint)Math.Floor(t));Set(ticks,"<floatingPoint>k__BackingField",t-Math.Floor(t));};
            var state=new WeaponEquipmentState {Main=new WeaponItemState{WeaponId=a.Id},ActiveSlot=WeaponSlot.Main,EquipRevision=1,Action=WeaponActionState.Idle,ReloadType=ReloadVariant.Empty};
            foreach(var pose in poses)Call(pose,"HandleWeaponEquipmentChanged",state);
            var first=(AnimatorOverrideController)animators[0].runtimeAnimatorController;
            var second=(AnimatorOverrideController)animators[1].runtimeAnimatorController;
            var key=(AnimationClip)Get(poses[0],"_reloadClip");int layer=animators[0].GetLayerIndex("Upper_Body_Layer");
            check("Equipment binding caches both reload candidates",ReferenceEquals(Get(poses[0],"_normalReload"),ap.ReloadNormal) && ReferenceEquals(Get(poses[0],"_emptyReload"),ap.ReloadEmpty));
            check("Idle equipment binding uses normal default despite previous Empty type",first[key.name]==normal);
            state.Action=WeaponActionState.Reloading;state.ActionSequence=1;state.ReloadType=ReloadVariant.Normal;state.ActionStartedAt=100;state.ActionDuration=2;clock(100.5);
            Call(poses[0],"HandleWeaponEquipmentChanged",state);animators[0].Update(0);
            check("Normal reload selects normal clip",first[key.name]==normal && (AnimationClip)Get(poses[0],"_activeReloadClip")==normal);
            check("Normal speed follows confirmed duration",Math.Abs(animators[0].GetFloat("BodyReloadSpeed")-normal.length/2)<0.0001);
            check("Normal delay seeks to quarter phase",Math.Abs(animators[0].GetCurrentAnimatorStateInfo(layer).normalizedTime-0.25)<0.001);
            state.ActionSequence=2;state.ReloadType=ReloadVariant.Empty;
            Call(poses[0],"HandleWeaponEquipmentChanged",state);animators[0].Update(0);
            check("Empty reload changes clip without equipment change or Idle",first[key.name]==empty && (AnimationClip)Get(poses[0],"_activeReloadClip")==empty && (uint)Get(poses[0],"_boundEquipRevision")==1);
            check("Empty speed uses selected clip length",Math.Abs(animators[0].GetFloat("BodyReloadSpeed")-empty.length/2)<0.0001);
            check("Empty reload preserves confirmed progress",Math.Abs(animators[0].GetCurrentAnimatorStateInfo(layer).normalizedTime-0.25)<0.001);
            check("Other character keeps its normal override",second[key.name]==normal);
            check("Reload variant does not alter hold override",first[hold.name]==hold);
            animators[0].Update(0.2f);float phase=animators[0].GetCurrentAnimatorStateInfo(layer).normalizedTime;
            Call(poses[0],"HandleWeaponEquipmentChanged",state);animators[0].Update(0);
            check("Duplicate Empty state does not restart playback",Math.Abs(animators[0].GetCurrentAnimatorStateInfo(layer).normalizedTime-phase)<0.001);
            state.ActionSequence=3;state.ReloadType=ReloadVariant.Normal;
            Call(poses[0],"HandleWeaponEquipmentChanged",state);animators[0].Update(0);
            check("Normal after Empty restores the normal clip",first[key.name]==normal && (AnimationClip)Get(poses[0],"_activeReloadClip")==normal);
            check("Normal after Empty restores normal playback speed",Math.Abs(animators[0].GetFloat("BodyReloadSpeed")-normal.length/2)<0.0001);
            state.Action=WeaponActionState.Idle;Call(poses[0],"HandleWeaponEquipmentChanged",state);animators[0].Update(0.1f);
            check("Completion returns to holding",!(bool)Get(poses[0],"_playingReload") && animators[0].GetCurrentAnimatorStateInfo(layer).IsName("Upper_Body_Layer.Weap_Hold"));
            state.Main=new WeaponItemState{WeaponId=b.Id};state.EquipRevision=2;state.ReloadType=ReloadVariant.Empty;
            Call(poses[0],"HandleWeaponEquipmentChanged",state);
            check("Switching clears the previous weapon's empty clip",((WeaponReloadAnimation)Get(poses[0],"_emptyReload")).BodyClip==null && ((WeaponReloadAnimation)Get(poses[0],"_normalReload")).BodyClip==otherNormal);
            state.Action=WeaponActionState.Reloading;state.ActionSequence=4;state.ActionStartedAt=101;state.ActionDuration=1.5f;clock(101.75);
            Call(poses[0],"HandleWeaponEquipmentChanged",state);animators[0].Update(0);
            check("Missing optional Empty clip falls back to this weapon's normal",first[key.name]==otherNormal && (AnimationClip)Get(poses[0],"_activeReloadClip")==otherNormal && (bool)Get(poses[0],"_playingReload"));
            check("Fallback speed uses this weapon's normal clip",Math.Abs(animators[0].GetFloat("BodyReloadSpeed")-otherNormal.length/1.5f)<0.0001);
            check("Fallback preserves half-phase progress",Math.Abs(animators[0].GetCurrentAnimatorStateInfo(layer).normalizedTime-0.5)<0.001);
            state.Main=new WeaponItemState{WeaponId=a.Id};state.EquipRevision=3;state.ActionSequence=5;state.ActionStartedAt=102;state.ActionDuration=2;clock(102.5);
            Call(poses[0],"HandleWeaponEquipmentChanged",state);animators[0].Update(0);
            check("Switching back restores available Empty variant",first[key.name]==empty && (AnimationClip)Get(poses[0],"_activeReloadClip")==empty);
            state.ActionSequence=6;state.ActionStartedAt=90;clock(110);
            Call(poses[0],"HandleWeaponEquipmentChanged",state);animators[0].Update(0.1f);
            check("Expired action does not start a variant",!(bool)Get(poses[0],"_playingReload") && animators[0].GetCurrentAnimatorStateInfo(layer).IsName("Upper_Body_Layer.Weap_Hold"));
            state.ActionSequence=7;state.ActionStartedAt=108;clock(108.5);
            ((SyncVar<WeaponEquipmentState>)Get(controllers[0],"_confirmedState")).value=state;
            Call(poses[0],"OnEnable");animators[0].Update(0);
            check("Enable mid-reload reconstructs Empty variant and progress",first[key.name]==empty && (bool)Get(poses[0],"_playingReload") && Math.Abs(animators[0].GetCurrentAnimatorStateInfo(layer).normalizedTime-0.25)<0.001);
            Call(poses[0],"OnDisable");animators[0].Update(0.1f);
            check("Disable cancels variant playback and restores aim",!(bool)Get(poses[0],"_playingReload") && ((CharacterRigBinding)Get(poses[0],"_rigBinding")).UpperBodyRig.weight==1);
            check("Template reload key remains original",(AnimationClip)Get(poses[0],"_reloadClip")==key);
            check("Source presentation asset remains unchanged",actual.PresentationDefinition.ReloadNormal.BodyClip==normal && actual.PresentationDefinition.ReloadEmpty.BodyClip==savedEmpty && !EditorUtility.IsDirty(actual.PresentationDefinition));
            check("Scene remains clean",!UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty);

            Set(ap.ReloadEmpty,"_bodyClip",normal);
            state.Action=WeaponActionState.Reloading; state.ActionStartedAt=120; state.ActionDuration=2; clock(120.5);
            Action syncHand=()=>{ ((SyncVar<WeaponEquipmentState>)Get(controllers[0],"_confirmedState")).value=state; Call(poses[0],"UpdateSupportHandWeight"); };
            state.ActionSequence=20; state.ReloadType=ReloadVariant.Normal;
            Call(poses[0],"HandleWeaponEquipmentChanged",state); syncHand();
            var support=((CharacterRigBinding)Get(poses[0],"_rigBinding")).SupportHandRig;
            check("Normal curve evaluates confirmed quarter progress",Math.Abs(support.weight-.25f)<.001f);
            state.ActionSequence=21; state.ReloadType=ReloadVariant.Empty;
            Call(poses[0],"HandleWeaponEquipmentChanged",state); syncHand();
            check("Empty changes hand curve even when both variants share the same body clip",first[key.name]==normal && Math.Abs(support.weight-.75f)<.001f);
            state.ActionSequence=22; state.ReloadType=ReloadVariant.Normal;
            Call(poses[0],"HandleWeaponEquipmentChanged",state); syncHand();
            check("Normal restores its curve after same-clip Empty",Math.Abs(support.weight-.25f)<.001f);
            Set(ap.ReloadEmpty,"_bodyClip",null);
            state.ActionSequence=23; state.ReloadType=ReloadVariant.Empty;
            Call(poses[0],"HandleWeaponEquipmentChanged",state); syncHand();
            check("Absent Empty body uses Normal clip and Normal curve together",first[key.name]==normal && Math.Abs(support.weight-.25f)<.001f);
            Set(ap.ReloadEmpty,"_bodyClip",normal); Set(ap.ReloadEmpty,"_supportHandWeight",new AnimationCurve());
            state.ActionSequence=24;
            Call(poses[0],"HandleWeaponEquipmentChanged",state); syncHand();
            check("Empty hand curve leaves authored hand free",support.weight==0f);
            state.Action=WeaponActionState.Idle; Call(poses[0],"HandleWeaponEquipmentChanged",state); syncHand();
            check("Reload cancellation restores holding correction",support.weight==1f);
            Set(ap,"_reloadEmpty",null); state.Action=WeaponActionState.Reloading; state.ActionSequence=25;
            state.EquipRevision=4; Call(poses[0],"HandleWeaponEquipmentChanged",state); syncHand();
            check("Entire absent Empty entry falls back to Normal clip and curve",first[key.name]==normal && Math.Abs(support.weight-.25f)<.001f);
            var result=JsonConvert.SerializeObject(new {checks});
            System.IO.File.WriteAllText(".codex/animation-checks/body-regression.json",result);
            return result;
        }
        finally
        {
            registryField.SetValue(null,previousRegistry);readyField.SetValue(null,previousReady);
            foreach(var pose in poses)if(pose)Call(pose,"OnDisable");
            foreach(var controller in controllers)if(controller)typeof(NetworkIdentity).GetField("_isSpawnedServer",Flags).SetValue(controller,false);
            for(int i=0;i<animators.Count;i++)if(animators[i])animators[i].runtimeAnimatorController=originals[i];
            foreach(var obj in objects)if(obj)UnityEngine.Object.DestroyImmediate(obj);
            foreach(var root in roots)PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
