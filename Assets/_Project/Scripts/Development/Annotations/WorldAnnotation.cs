using UnityEngine;
using TMPro;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Project.Core.Dev
{
    public enum AnnotationType : byte
    {
        BasicText,   // White
        Info,        // Blue / Cyan
        Mechanic,    // Green
        Warning,     // Yellow
        Todo,        // Red
        Metric       // Purple
    }

    [SelectionBase]
    [DisallowMultipleComponent]
    public class WorldAnnotation : MonoBehaviour
    {
        [Header("Note Content")]
        [SerializeField] private string _title = "TEST NOTE";
        [TextArea(2, 6)]
        [SerializeField] private string _description = "Details about this mechanic or setup.";
        [SerializeField] private AnnotationType _type = AnnotationType.Info;

        [Header("Display Settings")]
        [SerializeField] private bool _showInPlayMode = false;
        [SerializeField] private bool _billboardToCamera = true;
        [SerializeField] private float _viewDistance = 25.0f;

        private TextMeshPro _runtimeText;
        private Camera _camera;

        public string Title => _title;
        public string Description => _description;
        public AnnotationType Type => _type;

        private void Start()
        {
            _camera = Camera.main;
        }

        private void Awake()
        {
            if (_showInPlayMode)
            {
                SetupRuntimeText();
            }
        }

        private void LateUpdate()
        {
            if (_showInPlayMode && _billboardToCamera && _runtimeText && _camera)
            {
                transform.rotation = Quaternion.LookRotation(transform.position - _camera.transform.position);
            }
        }

        private void SetupRuntimeText()
        {
            var textGo = new GameObject("[3D_Text]");
            textGo.transform.SetParent(transform, false);

            _runtimeText = textGo.AddComponent<TextMeshPro>();
            _runtimeText.text = $"<b>{_title}</b>\n<size=75%>{_description}</size>";
            _runtimeText.fontSize = 4;
            _runtimeText.alignment = TextAlignmentOptions.Center;
            _runtimeText.color = GetBadgeColor(_type);
        }

        public static Color GetBadgeColor(AnnotationType type) => type switch
        {
            AnnotationType.Mechanic => new Color(0.2f, 1f, 0.4f),
            AnnotationType.Warning => new Color(1f, 0.85f, 0.2f),
            AnnotationType.Todo => new Color(1f, 0.3f, 0.2f),
            AnnotationType.Metric => new Color(0.7f, 0.4f, 1f),
            AnnotationType.BasicText => new Color(1f, 1f, 1f),
            _ => new Color(0.2f, 0.8f, 1f)
        };

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!Camera.current) return;

            float dist = Vector3.Distance(Camera.current.transform.position, transform.position);
            if (dist > _viewDistance) return;

            Color color = GetBadgeColor(_type);

            Gizmos.color = color;
            // Gizmos.DrawWireSphere(transform.position, 0.2f);
            // Gizmos.DrawLine(transform.position, transform.position + Vector3.down * 0.5f);

            GUIStyle labelStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = Mathf.Clamp(Mathf.RoundToInt(13 - (dist * 0.3f)), 9, 13),
                normal = { textColor = color },
                alignment = TextAnchor.MiddleCenter
            };

            string labelText;
            
            if (_type != AnnotationType.BasicText)
            {
                labelText = string.IsNullOrEmpty(_description) 
                    ? $"[{_type.ToString().ToUpper()}] {_title}" 
                    : $"[{_type.ToString().ToUpper()}] {_title}\n{_description}";    
            }
            else
            {
                labelText = string.IsNullOrEmpty(_description) 
                    ? $"{_title}" 
                    : $"{_title}\n{_description}";
            }
            
            

            Handles.Label(transform.position + Vector3.up * 0.35f, labelText, labelStyle);
        }
#endif
    }
}
