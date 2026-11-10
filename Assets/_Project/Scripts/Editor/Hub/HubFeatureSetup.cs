using Project.Combat.Props;
using Project.Core.Match;
using Project.UI.Match;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Project.Hub.Editor
{
public static class HubFeatureSetup
{
    private const string RootName = "HubFeatures";

    [MenuItem("Tools/Hub/Add Hub Features To Open Scene")]
    private static void AddFeatures()
    {
        if (GameObject.Find(RootName))
        {
            EditorUtility.DisplayDialog("Hub features", $"'{RootName}' already exists in this scene. Delete it first to rebuild.", "OK");
            return;
        }

        var anchor = Object.FindAnyObjectByType<LevelSelectorConsole>();
        Vector3 origin = anchor ? anchor.transform.position : Vector3.zero;
        origin.y = 0f;

        var root = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(root, "Add hub features");
        root.transform.position = origin;

        CreateReadyPad(root.transform);
        CreateBoard(root.transform);
        CreateRange(root.transform);
        CreateLightSwitch(root.transform);
        CreateRadio(root.transform);

        EditorSceneManager.MarkSceneDirty(root.scene);
        Selection.activeGameObject = root;
        Debug.Log($"[HubFeatureSetup] Created '{RootName}' at {origin}. Move the root or its children as needed, then save the scene.");
    }

    [MenuItem("Tools/Hub/Add Deploy Zone To Open Scene")]
    private static void AddDeployZone()
    {
        var launcher = Object.FindAnyObjectByType<LevelLaunchController>();
        if (!launcher)
        {
            EditorUtility.DisplayDialog("Deploy zone", "No LevelLaunchController in the open scene.", "OK");
            return;
        }

        var launcherSo = new SerializedObject(launcher);
        var zoneProp = launcherSo.FindProperty("_deployZone");
        if (zoneProp.objectReferenceValue)
        {
            EditorUtility.DisplayDialog("Deploy zone", "The launcher already has a deploy zone assigned.", "OK");
            return;
        }

        var anchor = Object.FindAnyObjectByType<LevelSelectorConsole>();
        Vector3 pos = anchor ? anchor.transform.position : Vector3.zero;
        pos.y = 0f;

        var zone = new GameObject("DeployZone");
        Undo.RegisterCreatedObjectUndo(zone, "Add deploy zone");
        var box = zone.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(6f, 3f, 6f);
        box.center = new Vector3(0f, 1.5f, 0f);
        zone.AddComponent<AreaPlayerDetector>();
        zone.transform.position = pos + new Vector3(0f, 0f, -6f);

        var pad = Prim("FloorMarker", PrimitiveType.Cube, zone.transform, new Vector3(0f, 0.02f, 0f),
            new Vector3(6f, 0.04f, 6f), new Color(1f, 0.8f, 0.2f));
        Object.DestroyImmediate(pad.GetComponent<Collider>());

        zoneProp.objectReferenceValue = zone.GetComponent<AreaPlayerDetector>();
        launcherSo.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(zone.scene);
        Selection.activeGameObject = zone;
        Debug.Log("[HubFeatureSetup] Created 'DeployZone' and assigned it to the LevelLaunchController. Resize its BoxCollider and move it where players should gather.");
    }

    private static GameObject Prim(string name, PrimitiveType type, Transform parent, Vector3 local, Vector3 scale, Color color)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = local;
        go.transform.localScale = scale;
        var r = go.GetComponent<Renderer>();
        if (r)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            mat.color = color;
            r.sharedMaterial = mat;
        }
        return go;
    }

    private static void CreateReadyPad(Transform root)
    {
        var pad = Prim("ReadyPad", PrimitiveType.Cube, root, new Vector3(-3f, 0.05f, 2f), new Vector3(1.5f, 0.1f, 1.5f),
            new Color(0.2f, 0.8f, 0.3f));
        pad.AddComponent<HubReadyState>();
    }

    private static void CreateBoard(Transform root)
    {
        var board = new GameObject("LevelBoard");
        board.transform.SetParent(root, false);
        board.transform.localPosition = new Vector3(0f, 2.2f, 5f);

        var panel = Prim("Panel", PrimitiveType.Cube, board.transform, Vector3.zero, new Vector3(4f, 2.2f, 0.1f),
            new Color(0.05f, 0.05f, 0.08f));
        Object.DestroyImmediate(panel.GetComponent<Collider>());

        var textGo = new GameObject("BoardText");
        textGo.transform.SetParent(board.transform, false);
        textGo.transform.localPosition = new Vector3(0f, 0f, -0.06f);
        var tmp = textGo.AddComponent<TextMeshPro>();
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize = 3f;
        tmp.color = Color.white;
        tmp.text = "NO JOB SELECTED";
        tmp.rectTransform.sizeDelta = new Vector2(3.8f, 2f);

        var comp = board.AddComponent<HubLevelBoard>();
        var so = new SerializedObject(comp);
        so.FindProperty("_text").objectReferenceValue = tmp;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void CreateRange(Transform root)
    {
        var range = new GameObject("PracticeRange");
        range.transform.SetParent(root, false);
        range.transform.localPosition = new Vector3(6f, 0f, 0f);

        for (int i = 0; i < 3; i++)
        {
            var target = Prim($"Target_{i + 1}", PrimitiveType.Cube, range.transform,
                new Vector3(i * 2f - 2f, 1f, 8f + i * 2f), new Vector3(0.7f, 1.8f, 0.1f), new Color(0.9f, 0.3f, 0.2f));
            target.AddComponent<PracticeTarget>();
        }
    }

    private static void CreateLightSwitch(Transform root)
    {
        var sw = Prim("LightSwitch", PrimitiveType.Cube, root, new Vector3(-6f, 1.3f, 0f), new Vector3(0.25f, 0.4f, 0.1f),
            new Color(0.9f, 0.9f, 0.9f));

        var lightGo = new GameObject("SwitchedLight");
        lightGo.transform.SetParent(root, false);
        lightGo.transform.localPosition = new Vector3(-6f, 3.5f, 1f);
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Point;
        light.range = 8f;
        light.intensity = 3f;

        var toggle = sw.AddComponent<NetworkedToggle>();
        var so = new SerializedObject(toggle);
        so.FindProperty("_targetName").stringValue = "Light Switch";
        var arr = so.FindProperty("_enableWhenOn");
        arr.arraySize = 1;
        arr.GetArrayElementAtIndex(0).objectReferenceValue = lightGo;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void CreateRadio(Transform root)
    {
        var radio = Prim("Radio", PrimitiveType.Cube, root, new Vector3(-6f, 0.6f, -2f), new Vector3(0.6f, 0.35f, 0.25f),
            new Color(0.35f, 0.25f, 0.15f));

        var src = radio.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.loop = true;
        src.spatialBlend = 1f;
        src.maxDistance = 15f;

        var toggle = radio.AddComponent<NetworkedToggle>();
        var so = new SerializedObject(toggle);
        so.FindProperty("_targetName").stringValue = "Radio";
        so.FindProperty("_startOn").boolValue = false;
        var audio = so.FindProperty("_audioWhenOn");
        audio.arraySize = 1;
        audio.GetArrayElementAtIndex(0).objectReferenceValue = src;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
}
