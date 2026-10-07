using System.Collections.Generic;
using Project.Core.Match;
using Project.Logging;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Project.Core.Editor
{
    [CustomEditor(typeof(SpawnPoint))]
    [CanEditMultipleObjects]
    public class SpawnPointEditor : UnityEditor.Editor
    {
        private VisualElement _root;
        private VisualElement _statusCard;
        private Label _statusTitle;
        private Label _statusDescription;

        public override VisualElement CreateInspectorGUI()
        {
            _root = new VisualElement();
            _root.style.paddingLeft = 4;
            _root.style.paddingRight = 4;
            _root.style.paddingTop = 6;
            _root.style.paddingBottom = 6;

            // 1. Header
            var headerContainer = new VisualElement();
            headerContainer.style.marginBottom = 10;
            headerContainer.style.paddingBottom = 6;
            headerContainer.style.borderBottomWidth = 1;
            headerContainer.style.borderBottomColor = new Color(0.25f, 0.28f, 0.35f, 0.6f);

            var titleLabel = new Label("PLAYER SPAWN POINT");
            titleLabel.style.fontSize = 14;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.color = new Color(0.88f, 0.92f, 0.98f);
            headerContainer.Add(titleLabel);

            var subtitleLabel = new Label("Validates collision clearance, ground contact, and synchronizes with PurrNet.");
            subtitleLabel.style.fontSize = 11;
            subtitleLabel.style.color = new Color(0.6f, 0.65f, 0.72f);
            subtitleLabel.style.whiteSpace = WhiteSpace.Normal;
            headerContainer.Add(subtitleLabel);

            _root.Add(headerContainer);

            // 2. Status Card
            _statusCard = new VisualElement();
            _statusCard.style.paddingLeft = 10;
            _statusCard.style.paddingRight = 10;
            _statusCard.style.paddingTop = 8;
            _statusCard.style.paddingBottom = 8;
            _statusCard.style.marginBottom = 12;
            _statusCard.style.borderLeftWidth = 4;
            _statusCard.style.borderTopRightRadius = 4;
            _statusCard.style.borderBottomRightRadius = 4;

            _statusTitle = new Label("STATUS");
            _statusTitle.style.fontSize = 12;
            _statusTitle.style.unityFontStyleAndWeight = FontStyle.Bold;

            _statusDescription = new Label("Validating...");
            _statusDescription.style.fontSize = 11;
            _statusDescription.style.marginTop = 2;
            _statusDescription.style.whiteSpace = WhiteSpace.Normal;

            _statusCard.Add(_statusTitle);
            _statusCard.Add(_statusDescription);
            _root.Add(_statusCard);

            // 3. Status Properties
            var enabledProp = serializedObject.FindProperty("_isEnabled");
            var enabledField = new PropertyField(enabledProp, "Enable Spawn Point");
            enabledField.RegisterValueChangeCallback(evt =>
            {
                serializedObject.ApplyModifiedProperties();
                UpdateStatusCard();
                SceneView.RepaintAll();
            });
            _root.Add(enabledField);

            var autoDisableProp = serializedObject.FindProperty("_autoDisableIfObstructed");
            var autoDisableField = new PropertyField(autoDisableProp, "Auto-Disable When Obstructed");
            _root.Add(autoDisableField);

            // 4. Quick Actions
            var actionRow = new VisualElement();
            actionRow.style.flexDirection = FlexDirection.Row;
            actionRow.style.marginTop = 8;
            actionRow.style.marginBottom = 12;

            var snapBtn = new Button(OnSnapToGroundClicked) { text = "Snap to Ground" };
            snapBtn.style.flexGrow = 1;
            snapBtn.style.height = 26;
            actionRow.Add(snapBtn);

            var validateBtn = new Button(OnValidateClicked) { text = "Validate" };
            validateBtn.style.width = 80;
            validateBtn.style.height = 26;
            actionRow.Add(validateBtn);

            _root.Add(actionRow);

            // 5. Foldout for Dimensions and Collision Layers
            var foldout = new Foldout { text = "Dimensions & Collision Settings", value = false };
            foldout.style.marginBottom = 12;

            foldout.Add(new PropertyField(serializedObject.FindProperty("_capsuleRadius")));
            foldout.Add(new PropertyField(serializedObject.FindProperty("_capsuleHeight")));
            foldout.Add(new PropertyField(serializedObject.FindProperty("_capsuleCenter")));
            foldout.Add(new PropertyField(serializedObject.FindProperty("_obstacleLayers")));
            foldout.Add(new PropertyField(serializedObject.FindProperty("_groundLayers")));
            foldout.Add(new PropertyField(serializedObject.FindProperty("_groundRaycastDistance")));
            foldout.Add(new PropertyField(serializedObject.FindProperty("_maxGroundDistance")));
            foldout.Add(new PropertyField(serializedObject.FindProperty("_maxSlopeAngle")));

            _root.Add(foldout);

            // 6. Scene Spawner Sync Section
            var sceneBox = new VisualElement();
            sceneBox.style.paddingLeft = 8;
            sceneBox.style.paddingRight = 8;
            sceneBox.style.paddingTop = 8;
            sceneBox.style.paddingBottom = 8;
            sceneBox.style.backgroundColor = new Color(0.18f, 0.20f, 0.25f, 0.5f);
            sceneBox.style.borderTopWidth = 1;
            sceneBox.style.borderBottomWidth = 1;
            sceneBox.style.borderLeftWidth = 1;
            sceneBox.style.borderRightWidth = 1;
            sceneBox.style.borderTopColor = new Color(0.3f, 0.35f, 0.42f, 0.4f);
            sceneBox.style.borderBottomColor = new Color(0.3f, 0.35f, 0.42f, 0.4f);
            sceneBox.style.borderLeftColor = new Color(0.3f, 0.35f, 0.42f, 0.4f);
            sceneBox.style.borderRightColor = new Color(0.3f, 0.35f, 0.42f, 0.4f);
            sceneBox.style.borderTopLeftRadius = 4;
            sceneBox.style.borderTopRightRadius = 4;
            sceneBox.style.borderBottomLeftRadius = 4;
            sceneBox.style.borderBottomRightRadius = 4;

            var sceneHeader = new Label("Scene Spawner Controls");
            sceneHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
            sceneHeader.style.marginBottom = 4;
            sceneBox.Add(sceneHeader);

            var sceneHelp = new Label("Manage all spawn points in this scene and keep PurrNet's PlayerSpawner in sync.");
            sceneHelp.style.fontSize = 10;
            sceneHelp.style.color = new Color(0.6f, 0.65f, 0.72f);
            sceneHelp.style.marginBottom = 8;
            sceneHelp.style.whiteSpace = WhiteSpace.Normal;
            sceneBox.Add(sceneHelp);

            var validateAllBtn = new Button(OnValidateAllInScene) { text = "Validate All Spawn Points in Scene" };
            validateAllBtn.style.height = 24;
            validateAllBtn.style.marginBottom = 4;
            sceneBox.Add(validateAllBtn);

            var disableInvalidBtn = new Button(OnDisableInvalidInScene) { text = "Disable Obstructed Points in Scene" };
            disableInvalidBtn.style.height = 24;
            disableInvalidBtn.style.marginBottom = 4;
            sceneBox.Add(disableInvalidBtn);

            var syncBtn = new Button(OnSyncPlayerSpawner) { text = "Sync PurrNet PlayerSpawner List" };
            syncBtn.style.height = 26;
            syncBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
            sceneBox.Add(syncBtn);

            _root.Add(sceneBox);

            UpdateStatusCard();
            return _root;
        }

        private void UpdateStatusCard()
        {
            if (target is not SpawnPoint spawnPoint)
                return;

            var info = spawnPoint.Validate(true);

            switch (info.Status)
            {
                case SpawnPointStatus.Valid:
                    _statusCard.style.backgroundColor = new Color(0.06f, 0.28f, 0.16f, 0.35f);
                    _statusCard.style.borderLeftColor = new Color(0.13f, 0.77f, 0.36f);
                    _statusTitle.style.color = new Color(0.34f, 0.92f, 0.52f);
                    _statusTitle.text = "VALID - READY TO SPAWN";
                    _statusDescription.style.color = new Color(0.85f, 0.95f, 0.88f);
                    _statusDescription.text = info.Description;
                    break;

                case SpawnPointStatus.Obstructed:
                    _statusCard.style.backgroundColor = new Color(0.35f, 0.08f, 0.08f, 0.40f);
                    _statusCard.style.borderLeftColor = new Color(0.94f, 0.27f, 0.27f);
                    _statusTitle.style.color = new Color(0.98f, 0.45f, 0.45f);
                    _statusTitle.text = "OBSTRUCTED - CLIPPING GEOMETRY";
                    _statusDescription.style.color = new Color(0.98f, 0.85f, 0.85f);
                    _statusDescription.text = info.Description;
                    break;

                case SpawnPointStatus.Floating:
                    _statusCard.style.backgroundColor = new Color(0.35f, 0.22f, 0.05f, 0.40f);
                    _statusCard.style.borderLeftColor = new Color(0.96f, 0.62f, 0.07f);
                    _statusTitle.style.color = new Color(0.98f, 0.75f, 0.25f);
                    _statusTitle.text = "FLOATING - NO GROUND DETECTED";
                    _statusDescription.style.color = new Color(0.98f, 0.92f, 0.80f);
                    _statusDescription.text = info.Description;
                    break;

                case SpawnPointStatus.SteepSlope:
                case SpawnPointStatus.Underground:
                    _statusCard.style.backgroundColor = new Color(0.35f, 0.15f, 0.05f, 0.40f);
                    _statusCard.style.borderLeftColor = new Color(0.98f, 0.45f, 0.09f);
                    _statusTitle.style.color = new Color(0.98f, 0.60f, 0.25f);
                    _statusTitle.text = $"UNSTABLE - {info.Status.ToString().ToUpper()}";
                    _statusDescription.style.color = new Color(0.98f, 0.90f, 0.80f);
                    _statusDescription.text = info.Description;
                    break;

                case SpawnPointStatus.Disabled:
                default:
                    _statusCard.style.backgroundColor = new Color(0.20f, 0.22f, 0.27f, 0.40f);
                    _statusCard.style.borderLeftColor = new Color(0.45f, 0.50f, 0.58f);
                    _statusTitle.style.color = new Color(0.70f, 0.75f, 0.82f);
                    _statusTitle.text = "DISABLED - EXCLUDED FROM SPAWNING";
                    _statusDescription.style.color = new Color(0.65f, 0.70f, 0.76f);
                    _statusDescription.text = info.Description;
                    break;
            }
        }

        private void OnSnapToGroundClicked()
        {
            foreach (var obj in targets)
            {
                if (obj is SpawnPoint sp && sp.TryGetGroundSnapPosition(out var groundPos))
                {
                    Undo.RecordObject(sp.transform, "Snap Spawn Point to Ground");
                    sp.transform.position = groundPos;
                    EditorUtility.SetDirty(sp);
                }
            }

            UpdateStatusCard();
            SceneView.RepaintAll();
        }

        private void OnValidateClicked()
        {
            UpdateStatusCard();
            SceneView.RepaintAll();
        }

        private void OnValidateAllInScene()
        {
            var allPoints = FindObjectsByType<SpawnPoint>(FindObjectsInactive.Include);
            int validCount = 0;
            int obstructedCount = 0;
            int floatingCount = 0;
            int disabledCount = 0;

            for (int i = 0; i < allPoints.Length; i++)
            {
                var val = allPoints[i].Validate(true);
                switch (val.Status)
                {
                    case SpawnPointStatus.Valid:
                        validCount++;
                        break;
                    case SpawnPointStatus.Obstructed:
                        obstructedCount++;
                        break;
                    case SpawnPointStatus.Floating:
                    case SpawnPointStatus.SteepSlope:
                    case SpawnPointStatus.Underground:
                        floatingCount++;
                        break;
                    case SpawnPointStatus.Disabled:
                        disabledCount++;
                        break;
                }
            }

            Debug.Log($"[SpawnPoint] Validated {allPoints.Length} spawn points in scene: " +
                      $"{validCount} Valid, {obstructedCount} Obstructed, {floatingCount} Floating/Unstable, {disabledCount} Disabled.");

            UpdateStatusCard();
            SceneView.RepaintAll();
        }

        private void OnDisableInvalidInScene()
        {
            var allPoints = FindObjectsByType<SpawnPoint>(FindObjectsInactive.Include);
            int disabledCount = 0;

            for (int i = 0; i < allPoints.Length; i++)
            {
                var pt = allPoints[i];
                var val = pt.Validate(true);
                if (val.Status != SpawnPointStatus.Valid && pt.IsEnabled)
                {
                    Undo.RecordObject(pt, "Disable Invalid Spawn Point");
                    pt.IsEnabled = false;
                    EditorUtility.SetDirty(pt);
                    disabledCount++;
                }
            }

            Debug.Log($"[SpawnPoint] Disabled {disabledCount} invalid spawn points in the scene.");
            SyncScenePlayerSpawnerInternal();
            UpdateStatusCard();
            SceneView.RepaintAll();
        }

        private void OnSyncPlayerSpawner()
        {
            SyncScenePlayerSpawnerInternal();
        }

        private static void SyncScenePlayerSpawnerInternal()
        {
            var spawner = FindAnyObjectByType<PurrNet.PlayerSpawner>();
            if (spawner == null)
            {
                Debug.LogWarning("[SpawnPoint] No PurrNet.PlayerSpawner found in active scene to synchronize.");
                return;
            }

            var serializedSpawner = new SerializedObject(spawner);
            var spawnPointsProp = serializedSpawner.FindProperty("spawnPoints");

            if (spawnPointsProp == null)
            {
                Debug.LogError("[SpawnPoint] Failed to locate 'spawnPoints' serialized property on PlayerSpawner.");
                return;
            }

            var allPoints = FindObjectsByType<SpawnPoint>(FindObjectsInactive.Include);
            var validTransforms = new List<Transform>();

            for (int i = 0; i < allPoints.Length; i++)
            {
                var pt = allPoints[i];
                if (pt.gameObject.activeInHierarchy && pt.IsEnabled)
                {
                    validTransforms.Add(pt.transform);
                }
            }

            spawnPointsProp.ClearArray();
            for (int i = 0; i < validTransforms.Count; i++)
            {
                spawnPointsProp.InsertArrayElementAtIndex(i);
                spawnPointsProp.GetArrayElementAtIndex(i).objectReferenceValue = validTransforms[i];
            }

            serializedSpawner.ApplyModifiedProperties();
            EditorUtility.SetDirty(spawner);

            if (spawner.gameObject.scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(spawner.gameObject.scene);
            }

            AppLog.Log(LogChannel.Editor, $"Synchronized PlayerSpawner '{spawner.name}': {validTransforms.Count} active & enabled spawn points registered.", spawner);
        }
    }
}
