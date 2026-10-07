using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Project.UI.Match
{
    public class RouteSelectionButtonUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        [Header("Route Data")]
        [SerializeField] private string _routeSceneName = "LevelScene_00";
        [SerializeField] private string _routeDisplayName = "Sector 01: Deep Shafts";
        [SerializeField] private string _hazardLevel = "Hazard 1";

        [Header("Visual Elements")]
        [SerializeField] private GameObject _selectionHighlight;
        [SerializeField] private TMP_Text _routeNameText;
        [SerializeField] private TMP_Text _hazardText;
        [SerializeField] private Image _backgroundImage;
        [SerializeField] private Color _normalColor = new Color(0.12f, 0.12f, 0.14f, 0.9f);
        [SerializeField] private Color _hoverColor = new Color(0.2f, 0.22f, 0.28f, 0.95f);
        [SerializeField] private Color _selectedColor = new Color(0.15f, 0.45f, 0.8f, 1.0f);

        private bool _isSelected;

        public string SceneName => _routeSceneName;
        public string DisplayName => !string.IsNullOrEmpty(_routeDisplayName) ? _routeDisplayName : _routeSceneName;
        public bool IsSelected => _isSelected;

        public event Action<RouteSelectionButtonUI> OnButtonHovered;
        public event Action<RouteSelectionButtonUI> OnButtonUnhovered;
        public event Action<RouteSelectionButtonUI> OnButtonClicked;
        public event Action<RouteSelectionButtonUI> OnButtonRightClicked;

        private void Awake()
        {
            UpdateVisualLabels();
            SetHighlighted(false);
        }

        private void UpdateVisualLabels()
        {
            if (_routeNameText && !string.IsNullOrEmpty(_routeDisplayName))
                _routeNameText.text = _routeDisplayName;

            if (_hazardText && !string.IsNullOrEmpty(_hazardLevel))
                _hazardText.text = _hazardLevel;
        }

        public void SetHighlighted(bool highlighted)
        {
            if (_selectionHighlight)
                _selectionHighlight.SetActive(highlighted || _isSelected);

            if (_backgroundImage && !_isSelected)
            {
                _backgroundImage.color = highlighted ? _hoverColor : _normalColor;
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            SetHighlighted(true);
            OnButtonHovered?.Invoke(this);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            SetHighlighted(false);
            OnButtonUnhovered?.Invoke(this);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                OnButtonClicked?.Invoke(this);
            }
            else if (eventData.button == PointerEventData.InputButton.Right)
            {
                OnButtonRightClicked?.Invoke(this);
            }
        }

        public void MakeSelected()
        {
            _isSelected = true;
            if (_selectionHighlight)
                _selectionHighlight.SetActive(true);

            if (_backgroundImage)
                _backgroundImage.color = _selectedColor;
        }

        public void MakeDeselected()
        {
            _isSelected = false;
            if (_selectionHighlight)
                _selectionHighlight.SetActive(false);

            if (_backgroundImage)
                _backgroundImage.color = _normalColor;
        }
    }
}