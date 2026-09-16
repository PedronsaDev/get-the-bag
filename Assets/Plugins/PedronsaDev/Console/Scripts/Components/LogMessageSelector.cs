using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PedronsaDev.Console.Components
{
public class LogMessageSelector : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private ConsolePreferences m_Preferences;
    [SerializeField] private Image m_Background;

    private LogMessageSetup m_LogMessage;
    private static int _currentSelectedHash = 0;

    #region Events
    // Select event
    public static event Action<int> OnSelectLogMessage;
    public static event Action<int> OnLogMessageClicked;
    #endregion

    private Color m_BackgroundColor;
    public int Hash;
    private Color _currentColor;
    private bool _isSelected;

    private void Awake()
    {
        m_LogMessage = GetComponent<LogMessageSetup>();
        Hash = gameObject.GetHashCode();
        if (m_Background != null)
        {
            m_BackgroundColor = m_Background.color;
            _currentColor = m_BackgroundColor;
        }
    }

    private void OnEnable()
    {
        OnSelectLogMessage += OnLogMessageSelected;
        OnLogMessageClicked += OnLogMessageSelected;
    }

    private void OnDisable()
    {
        OnSelectLogMessage -= OnLogMessageSelected;
        OnLogMessageClicked -= OnLogMessageSelected;
    }

    public static void LogMessageClicked(int hash)
    {
        OnLogMessageClicked?.Invoke(hash);
    }

    public void SetBaseColor(Color color)
    {
        m_BackgroundColor = color;
        if (!_isSelected)
        {
            _currentColor = color;
            if (m_Background != null)
            {
                m_Background.color = color;
            }
        }
    }

    public void ResetState()
    {
        Deselect();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            if (m_LogMessage == null)
            {
                m_LogMessage = GetComponent<LogMessageSetup>();
            }

            ConsoleContextMenu.Show(eventData.position, m_LogMessage);
            return;
        }

        OnSelectLogMessage?.Invoke(Hash);
    }

    private void OnLogMessageSelected(int hash)
    {
        if (hash == this.Hash)
        {
            if (_isSelected)
            {
                Deselect();
            }
            else
            {
                Select();
            }
        }
        else if (_isSelected)
        {
            Deselect();
        }
    }

    private void Select()
    {
        _isSelected = true;
        _currentSelectedHash = this.Hash;
        Color selectColor = m_Preferences != null ? m_Preferences.BackgroundSelectionColor : new Color(0.17f, 0.36f, 0.53f, 0.5f);
        _currentColor = selectColor;
        if (m_Background != null)
        {
            m_Background.color = selectColor;
        }
    }

    public void Deselect()
    {
        _isSelected = false;
        if (_currentSelectedHash == this.Hash)
        {
            _currentSelectedHash = 0;
        }
        _currentColor = m_BackgroundColor;
        if (m_Background != null)
        {
            m_Background.color = m_BackgroundColor;
        }
        if (m_LogMessage != null && m_LogMessage.AltTextGameObject != null && m_LogMessage.AltTextGameObject.activeSelf)
        {
            m_LogMessage.DisableAltText();
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (m_Background != null && m_Preferences != null)
        {
            m_Background.color = m_Preferences.BackgroundHoverTextColor;
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (m_Background != null)
        {
            m_Background.color = _currentColor;
        }
    }
}
}