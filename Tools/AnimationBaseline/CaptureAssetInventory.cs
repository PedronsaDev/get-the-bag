using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.AddressableAssets;
using Newtonsoft.Json;

public static class AnimationBaselineInventory
{
    static string PathOf(Transform t, Transform root) => !t ? null : t==root ? "." : AnimationUtility.CalculateTransformPath(t,root);
    static object Reference(UnityEngine.Object o, Transform root)
    {
        if(!o) return null;
        var t=o as Transform ?? (o as Component)?.transform ?? (o as GameObject)?.transform;
        var asset=AssetDatabase.GetAssetPath(o);
        return new { name=o.name,type=o.GetType().FullName,asset,guid=AssetDatabase.AssetPathToGUID(asset),hierarchy=t?PathOf(t,root):null };
    }
    static Dictionary<string,object> Fields(UnityEngine.Object obj, Transform root)
    {
        var fields=new Dictionary<string,object>();
        var p=new SerializedObject(obj).GetIterator();
        while(p.NextVisible(true))
        {
            if(new[]{"m_ObjectHideFlags","m_CorrespondingSourceObject","m_PrefabInstance","m_PrefabAsset","m_GameObject","m_Script","m_EditorHideFlags"}.Contains(p.propertyPath) || p.propertyType==SerializedPropertyType.Generic) continue;
            object value=null;
            switch(p.propertyType)
            {
                case SerializedPropertyType.ObjectReference: value=Reference(p.objectReferenceValue,root); break;
                case SerializedPropertyType.Boolean: value=p.boolValue;break;
                case SerializedPropertyType.Integer: value=p.longValue;break;
                case SerializedPropertyType.Float: value=p.doubleValue;break;
                case SerializedPropertyType.String: value=p.stringValue;break;
                case SerializedPropertyType.Enum: value=p.enumValueIndex>=0 && p.enumValueIndex<p.enumDisplayNames.Length?p.enumDisplayNames[p.enumValueIndex]:p.intValue.ToString();break;
                case SerializedPropertyType.Vector2: value=new[]{p.vector2Value.x,p.vector2Value.y};break;
                case SerializedPropertyType.Vector3: value=new[]{p.vector3Value.x,p.vector3Value.y,p.vector3Value.z};break;
                case SerializedPropertyType.Quaternion: value=p.quaternionValue.eulerAngles.ToString("F5");break;
                case SerializedPropertyType.AnimationCurve: value=p.animationCurveValue.keys.Select(k=>new{k.time,k.value,k.inTangent,k.outTangent,k.inWeight,k.outWeight,weighted=k.weightedMode.ToString()}).ToArray();break;
                case SerializedPropertyType.LayerMask: value=p.intValue;break;
            }
            if(value!=null)fields[p.propertyPath]=value;
        }
        return fields;
    }
    static object MotionInfo(Motion m)
    {
        if(!m)return null;
        if(m is BlendTree b) return new { name=b.name,kind="BlendTree",x=b.blendParameter,y=b.blendParameterY,children=b.children.Select(c=>new{c.threshold,position=c.position.ToString(),c.timeScale,motion=MotionInfo(c.motion)}).ToArray() };
        var clip=m as AnimationClip;
        return new {name=m.name,asset=AssetDatabase.GetAssetPath(m),length=clip?clip.length:0f};
    }
    static IEnumerable<object> States(AnimatorStateMachine sm,string parent)
    {
        foreach(var s in sm.states)yield return new {path=parent+"/"+s.state.name,s.state.speed,s.state.speedParameter,s.state.speedParameterActive,motion=MotionInfo(s.state.motion),transitions=s.state.transitions.Select(t=>new{to=t.destinationState?t.destinationState.name:null,t.hasExitTime,t.exitTime,t.duration,conditions=t.conditions.Select(c=>new{c.parameter,mode=c.mode.ToString(),c.threshold}).ToArray()}).ToArray()};
        foreach(var child in sm.stateMachines)foreach(var s in States(child.stateMachine,parent+"/"+child.stateMachine.name))yield return s;
    }
    public static object Main()
    {
        if(EditorApplication.isPlaying)throw new Exception("Capture saved inventory in Edit Mode.");
        var prefabPaths=new[]{"Assets/_Project/Prefabs/Player/CHAR_PF_Player.prefab","Assets/_Project/Prefabs/Weapons/Pistols/WEAP_FP_Pistol_A.prefab","Assets/_Project/Prefabs/Weapons/Pistols/WEAP_PF_Pistol_A.prefab","Assets/_Project/Prefabs/Weapons/Rifles/WEAP_PF_Rifle_Debug.prefab"};
        var prefabs=prefabPaths.Select(path=>{
            var root=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if(!root)throw new Exception("Missing baseline asset: "+path);
            return new {path,guid=AssetDatabase.AssetPathToGUID(path),
                transforms=root.GetComponentsInChildren<Transform>(true).Select(t=>new{path=PathOf(t,root.transform),position=t.localPosition.ToString("F5"),rotation=t.localEulerAngles.ToString("F5"),scale=t.localScale.ToString("F5"),active=t.gameObject.activeSelf}).ToArray(),
                components=root.GetComponentsInChildren<Component>(true).Where(c=>c && !(c is Transform)).Select(c=>new{path=PathOf(c.transform,root.transform),type=c.GetType().FullName,assembly=c.GetType().Assembly.GetName().Name,fields=Fields(c,root.transform)}).ToArray(),
                animators=root.GetComponentsInChildren<Animator>(true).Select(a=>new{path=PathOf(a.transform,root.transform),a.isHuman,a.applyRootMotion,culling=a.cullingMode.ToString(),controller=Reference(a.runtimeAnimatorController,root.transform),avatar=Reference(a.avatar,root.transform),humanBones=a.avatar&&a.avatar.isHuman?a.avatar.humanDescription.human.Select(h=>new{h.humanName,h.boneName}).ToArray():null}).ToArray(),
                renderers=root.GetComponentsInChildren<Renderer>(true).Select(r=>new{path=PathOf(r.transform,root.transform),type=r.GetType().Name,r.enabled,layer=LayerMask.LayerToName(r.gameObject.layer),shadow=r.shadowCastingMode.ToString(),bones=(r as SkinnedMeshRenderer)?.bones.Select(b=>PathOf(b,root.transform)).ToArray()}).ToArray()};
        }).ToArray();
        var controllerPaths=AssetDatabase.FindAssets("t:AnimatorController",new[]{"Assets/_Project/Animations"}).Select(AssetDatabase.GUIDToAssetPath).ToArray();
        var controllers=controllerPaths.Select(path=>{var c=AssetDatabase.LoadAssetAtPath<AnimatorController>(path);return new{path,parameters=c.parameters.Select(p=>new{p.name,type=p.type.ToString(),p.defaultFloat,p.defaultBool,p.defaultInt}).ToArray(),layers=c.layers.Select(l=>new{l.name,l.defaultWeight,blend=l.blendingMode.ToString(),mask=Reference(l.avatarMask,null),states=States(l.stateMachine,l.name).ToArray()}).ToArray()};}).ToArray();
        var models=AssetDatabase.FindAssets("t:Model",new[]{"Assets/_Project/Animations"}).Select(AssetDatabase.GUIDToAssetPath).Concat(new[]{"Assets/_Project/Art/Models/Player/Player_Body.fbx"}).Distinct().Select(path=>{
            var m=AssetImporter.GetAtPath(path) as ModelImporter;
            return new{path,guid=AssetDatabase.AssetPathToGUID(path),type=m.animationType.ToString(),avatarSetup=m.avatarSetup.ToString(),sourceAvatar=Reference(m.sourceAvatar,null),m.globalScale,m.useFileScale,m.useFileUnits,m.bakeAxisConversion,m.resampleCurves,m.importAnimation,m.animationCompression,m.animationRotationError,m.animationPositionError,m.animationScaleError,
                importClips=(m.clipAnimations.Length>0?m.clipAnimations:m.defaultClipAnimations).Select(c=>new{c.name,c.firstFrame,c.lastFrame,c.loopTime,c.loopPose,c.lockRootRotation,c.lockRootHeightY,c.lockRootPositionXZ,c.keepOriginalOrientation,c.keepOriginalPositionY,c.keepOriginalPositionXZ,c.heightFromFeet,c.rotationOffset,c.heightOffset}).ToArray(),
                clips=AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__preview__")).Select(c=>new{c.name,c.length,c.frameRate,c.isHumanMotion,c.hasRootCurves,c.hasMotionCurves,bindings=AnimationUtility.GetCurveBindings(c).Select(b=>new{b.path,type=b.type.Name,b.propertyName}).ToArray()}).ToArray()};
        }).ToArray();
        var settings=AddressableAssetSettingsDefaultObject.Settings;
        var definitions=AssetDatabase.FindAssets("t:WeaponDefinition",new[]{"Assets/_Project"}).Select(AssetDatabase.GUIDToAssetPath).Select(path=>new{path,guid=AssetDatabase.AssetPathToGUID(path),fields=Fields(AssetDatabase.LoadMainAssetAtPath(path),null)}).ToArray();
        var masks=AssetDatabase.FindAssets("t:AvatarMask",new[]{"Assets/_Project/Animations"}).Select(AssetDatabase.GUIDToAssetPath).Select(path=>{var m=AssetDatabase.LoadAssetAtPath<AvatarMask>(path);return new{path,body=Enumerable.Range(0,(int)AvatarMaskBodyPart.LastBodyPart).Select(i=>new{part=((AvatarMaskBodyPart)i).ToString(),active=m.GetHumanoidBodyPartActive((AvatarMaskBodyPart)i)}).ToArray(),transforms=Enumerable.Range(0,m.transformCount).Select(i=>new{path=m.GetTransformPath(i),active=m.GetTransformActive(i)}).ToArray()};}).ToArray();
        var data=new{capturedUtc=DateTime.UtcNow.ToString("O"),unity=Application.unityVersion,scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene().path,prefabs,controllers,models,definitions,masks,addressables=settings.groups.Where(g=>g).SelectMany(g=>g.entries.Where(e=>prefabPaths.Contains(e.AssetPath)||e.AssetPath.Contains("Weapon")||e.AssetPath.Contains("Player.prefab")).Select(e=>new{e.address,e.guid,path=e.AssetPath,group=g.Name,labels=e.labels.ToArray()})).ToArray()};
        var directory=Path.Combine(Path.GetDirectoryName(Application.dataPath),"Docs/Animation/Baseline",DateTime.UtcNow.ToString("yyyy-MM-dd_HHmmss"));if(Directory.Exists(directory))throw new IOException("Capture destination already exists; preserve the existing snapshot and retry."); Directory.CreateDirectory(directory);
        File.WriteAllText(directory+"/asset-inventory.json",JsonConvert.SerializeObject(data,Formatting.Indented));
        return new{file=directory+"/asset-inventory.json",prefabs=prefabs.Length,controllers=controllers.Length,models=models.Length,masks=masks.Length,definitions=definitions.Length};
    }
}
