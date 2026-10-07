#if UNITY_EDITOR

using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Project.AI.Cover.Editor
{
public class CoverBakerWindow : EditorWindow
{
    private LayerMaskField _layerMaskField;
    private FloatField _stepIntervalField;
    private FloatField _standoffField;
    private FloatField _lowHeightField;
    private FloatField _highHeightField;
    private Label _statusLabel;

    [MenuItem("Tools/AI/Cover Baker")]
    public static void Open()
    {
        var window = GetWindow<CoverBakerWindow>();
        window.titleContent = new GUIContent("Cover Baker");
        window.minSize = new Vector2(350, 420);
    }
    
    [MenuItem("Tools/AI/Toggle Draw Cover Points")]
    public static void Toggle()
    {
        CoverBakerEngine.SetDrawPoints(!CoverBakerEngine.DrawPoints);
    }
    
    public void ToggleDrawPoints()
    {
        CoverBakerEngine.SetDrawPoints(!CoverBakerEngine.DrawPoints);
        _statusLabel.text = CoverBakerEngine.DrawPoints ? "Cover points are now visible." : "Cover points are now hidden.";
        _statusLabel.style.color = Color.cyan;
    }

    private void CreateGUI()
    {
        VisualElement root = rootVisualElement;
        root.style.paddingTop = 10;
        root.style.paddingBottom = 10;
        root.style.paddingLeft = 12;
        root.style.paddingRight = 12;

        // 1. Header Banner
        var header = new Label("AI Cover Point Baker")
        {
            style =
            {
                fontSize = 16,
                unityFontStyleAndWeight = FontStyle.Bold,
                marginBottom = 12
            }
        };
        root.Add(header);

        // 2. Settings Container
        var settingsBox = new VisualElement
        {
            style =
            {
                marginBottom = 12
            }
        };

        _layerMaskField = new LayerMaskField("Obstacle Layer", ~0);
        _stepIntervalField = new FloatField("Step Interval (m)") { value = 1.0f };
        _standoffField = new FloatField("Standoff Distance (m)") { value = 0.35f };
        _lowHeightField = new FloatField("Low Cover Height (m)") { value = 1.1f };
        _highHeightField = new FloatField("High Cover Height (m)") { value = 2.0f };

        settingsBox.Add(_layerMaskField);
        settingsBox.Add(_stepIntervalField);
        settingsBox.Add(_standoffField);
        settingsBox.Add(_lowHeightField);
        settingsBox.Add(_highHeightField);
        root.Add(settingsBox);

        // 3. Action Buttons
        var bakeSelectedBtn = new Button(OnBakeSelectedClicked) { text = "Bake Selected GameObject" };
        bakeSelectedBtn.style.height = 30;
        bakeSelectedBtn.style.marginBottom = 6;
        root.Add(bakeSelectedBtn);

        var clearSelectedBtn = new Button(OnClearSelectedClicked) { text = "Clear Selected Cover" };
        clearSelectedBtn.style.height = 24;
        clearSelectedBtn.style.marginBottom = 12;
        root.Add(clearSelectedBtn);

        // 4. Status Box
        _statusLabel = new Label("Ready. Select an environment prop or room root.");
        _statusLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
        
        var toggleDrawPointsBtn = new Button(ToggleDrawPoints)
        {
            text = "Toggle Cover Points Visibility"
        };
        toggleDrawPointsBtn.style.height = 24;
        toggleDrawPointsBtn.style.marginBottom = 6;
        root.Add(toggleDrawPointsBtn);
        
        root.Add(_statusLabel);
    }

    private void OnBakeSelectedClicked()
    {
        if (!Selection.activeGameObject)
        {
            _statusLabel.text = "Error: No GameObject selected in Hierarchy.";
            _statusLabel.style.color = Color.red;
            return;
        }

        var settings = new CoverBakerEngine.BakeSettings
        {
            ObstacleMask = _layerMaskField.value,
            StepInterval = Mathf.Max(0.2f, _stepIntervalField.value),
            StandoffDistance = Mathf.Max(0.1f, _standoffField.value),
            LowCoverHeight = _lowHeightField.value,
            HighCoverHeight = _highHeightField.value,
            MinSpacingBetweenPointsSqr = 0.64f // 0.8m min separation
        };

        int count = CoverBakerEngine.BakeForHierarchy(Selection.activeGameObject, settings);
        _statusLabel.text = $"Successfully baked {count} cover points.";
        _statusLabel.style.color = Color.green;
    }

    private void OnClearSelectedClicked()
    {
        if (!Selection.activeGameObject) return;
        CoverBakerEngine.ClearCoverPoints(Selection.activeGameObject);
        _statusLabel.text = "Cover points cleared for selected object.";
        _statusLabel.style.color = Color.yellow;
    }
}
}
#endif
